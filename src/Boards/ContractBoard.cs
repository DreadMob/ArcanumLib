using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Common;
using ArcanumLib.Core;
using ArcanumLib.Inventory;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// One contract board on the server: post / accept / turn in / abandon / cancel / collect / expire, escrow of
    /// player rewards and the events other mods listen to. Every decision is made here; windows only send intents.
    /// What a contract asks for is its goal (<see cref="IBoardGoal" />, registered by kind), what it pays besides the
    /// escrowed items comes from <see cref="RewardSources" />.
    /// </summary>
    /// <remarks>
    /// Methods return an error lang key (<see cref="BoardOptions.LangPrefix" /> + "error-…") or null on success.
    /// The owner keeps the state: <see cref="BoardRegistry.Create" /> loads and saves a <see cref="BoardState" />, a mod
    /// with its own save object assigns <see cref="State" /> itself.
    /// </remarks>
    public sealed class ContractBoard
    {
        private readonly Dictionary<string, IBoardGoal> goals = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> completionInFlight = new(StringComparer.OrdinalIgnoreCase);
        private IBoardState state = new BoardState();

        public string Key { get; }
        public ICoreServerAPI Sapi { get; }
        public BoardOptions Options { get; }
        public BoardEscrow Escrow { get; }

        /// <summary>Reward sources besides escrowed items, asked in order.</summary>
        public List<IBoardRewardSource> RewardSources { get; } = new();

        /// <summary>Opens this board for a player (block click). Default: the ArcanumLib board window.</summary>
        public Action<IServerPlayer>? Opener { get; set; }

        /// <summary>The board's data. Assign after loading it, before the first tick.</summary>
        public IBoardState State
        {
            get => state;
            set => state = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Any change of the board (for windows to refresh).</summary>
        public event Action? Changed;

        public event Action<BoardContractEvent>? Posted;
        public event Action<BoardContractEvent>? Accepted;
        public event Action<BoardContractEvent>? Abandoned;

        /// <summary>
        /// The goal was handed in. A player contract is now <see cref="BoardStates.Fulfilled" /> (reward waits to be
        /// collected), a server contract is <see cref="BoardStates.Completed" />, paid and off the board.
        /// </summary>
        public event Action<BoardContractEvent>? Completed;

        /// <summary>Someone collected from a fulfilled contract (Player = who collected).</summary>
        public event Action<BoardContractEvent>? Collected;

        public event Action<BoardContractEvent>? Cancelled;
        public event Action<BoardContractEvent>? Expired;

        /// <summary>The author disputed work marked done (Player = author, <see cref="BoardContractEvent.Reason" /> set).</summary>
        public event Action<BoardContractEvent>? Disputed;

        /// <summary>An admin settled a done or disputed contract (<see cref="BoardContractEvent.Paid" />: claimer paid or author refunded).</summary>
        public event Action<BoardContractEvent>? Resolved;

        public ContractBoard(ICoreServerAPI sapi, string key, BoardOptions? options = null)
        {
            Sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Options = options ?? new BoardOptions();
            Escrow = new BoardEscrow(this);
        }

        // --------------------------------------------------------------------
        //  Goals and texts
        // --------------------------------------------------------------------

        /// <summary>Registers (or replaces) the goal of its kind.</summary>
        public ContractBoard RegisterGoal(IBoardGoal goal)
        {
            if (goal == null) throw new ArgumentNullException(nameof(goal));
            goals[goal.Kind] = goal;
            return this;
        }

        public IBoardGoal? GetGoal(string? kind) => kind != null && goals.TryGetValue(kind, out var g) ? g : null;

        public IReadOnlyCollection<IBoardGoal> Goals => goals.Values;

        /// <summary>Full lang key of a board message (prefix + key).</summary>
        public string LangKey(string key) => Options.LangPrefix + key;

        /// <summary>Localized board message.</summary>
        public string Text(string key, params object[] args)
        {
            string full = LangKey(key);
            return Options.Localize != null ? Options.Localize(full, args) : Lang.Get(full, args);
        }

        public void NotifyChanged() => Changed?.Invoke();

        internal IServerPlayer? FindOnline(string uid)
        {
            if (Options.FindOnlinePlayer != null) return Options.FindOnlinePlayer(uid);
            return ArcanumServices.Get<OnlinePlayerCache>()?.GetByUid(uid);
        }

        internal void ReturnOffline(BoardContract contract, byte[] bytes)
        {
            if (Options.OfflineRefund != null)
            {
                Options.OfflineRefund(state, contract, bytes);
                return;
            }
            if (state is BoardState own && contract.AuthorUid != null)
            {
                if (!own.PendingReturnsByUid.TryGetValue(contract.AuthorUid, out var list))
                    own.PendingReturnsByUid[contract.AuthorUid] = list = new List<byte[]>();
                list.Add(bytes);
                return;
            }
            Sapi.Logger.Error("[Boards] Board {0} has no offline refund sink; an escrowed stack of contract {1} could not be returned.", Key, contract.Id);
        }

        internal ItemStack? DeserializeStack(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                var stack = new ItemStack(bytes);
                stack.ResolveBlockOrItem(Sapi.World);
                return stack;
            }
            catch (Exception ex)
            {
                Sapi.Logger.Warning("[Boards] Failed to read an escrowed stack: {0}", ex);
                return null;
            }
        }

        // --------------------------------------------------------------------
        //  Lookup
        // --------------------------------------------------------------------

        public BoardContract? Find(string? contractId) => contractId == null ? null : state.Contracts.Find(c => c.Id == contractId);

        public BoardContract? FindOpen(string? contractId) => state.Contracts.Find(c => c.Id == contractId && c.State == BoardStates.Open);

        public BoardContract? FindClaimedBy(string? contractId, string playerUid) =>
            state.Contracts.Find(c => c.Id == contractId && c.State == BoardStates.Claimed && c.ClaimedByUid == playerUid);

        /// <summary>Open, claimed or awaiting-confirmation player contracts of this author.</summary>
        public int CountActivePosts(string authorUid) =>
            state.Contracts.Count(c => c.Source == BoardSources.Player && c.AuthorUid == authorUid
                && (BoardStates.IsActive(c.State) || BoardStates.IsAwaiting(c.State)));

        /// <summary>Contracts this player has claimed, optionally filtered.</summary>
        public int CountClaimed(string playerUid, System.Func<BoardContract, bool>? filter = null) =>
            state.Contracts.Count(c => c.State == BoardStates.Claimed && c.ClaimedByUid == playerUid && (filter == null || filter(c)));

        /// <summary>Hours left of the player's post cooldown (0 when none).</summary>
        public double PostCooldownRemainingHours(string playerUid, double cooldownHours)
        {
            if (cooldownHours <= 0 || !state.LastPostTotalDays.TryGetValue(playerUid, out double last)) return 0;
            return Math.Max(0, cooldownHours - (Sapi.World.Calendar.TotalDays - last) * 24.0);
        }

        /// <summary>Re-attaches goal tracking of every claimed contract (after load or a reload of goals).</summary>
        public void ReattachClaimed()
        {
            foreach (var contract in state.Contracts)
            {
                if (contract.State == BoardStates.Claimed)
                    GetGoal(contract.Kind)?.OnLoaded(this, contract);
            }
        }

        // --------------------------------------------------------------------
        //  Post
        // --------------------------------------------------------------------

        /// <summary>
        /// A player posts a contract. <paramref name="draft" /> carries the goal (kind, target, demand) and texts; the
        /// board fills id, author, times and state, takes the escrow and puts it on the board.
        /// </summary>
        /// <param name="expireInDays">Days until it runs out, 0 = never.</param>
        /// <param name="check">
        /// <see cref="BoardPostCheck.GoalOnly" />: the caller already checked the post itself (a goal with its own
        /// posting rules); only the goal's <see cref="IBoardGoal.ValidatePost" /> runs.
        /// </param>
        /// <param name="rewards">Amounts for the board's <see cref="IBoardPostableReward" />s (the post window's fields).</param>
        public string? Post(IServerPlayer author, BoardContract draft, IInventory? escrowInv, double expireInDays = 0, BoardPostCheck check = BoardPostCheck.Full,
            IReadOnlyList<BoardRewardFieldValue>? rewards = null)
        {
            var reserved = new List<IBoardPostableReward>();
            var toReserve = (rewards ?? Array.Empty<BoardRewardFieldValue>())
                .Where(v => v != null && v.Amount > 0)
                .Select(v => (value: v, source: RewardSources.OfType<IBoardPostableReward>().FirstOrDefault(s => s.Id == v.Source)))
                .ToList();
            try
            {
                string? error = null;
                if (toReserve.Any(r => r.source == null)) return LangKey("error-reward");
                if (check == BoardPostCheck.Full)
                {
                    error = Options.ValidatePost != null
                        ? Options.ValidatePost(author, draft, escrowInv)
                        : ValidateByRules(author, draft, escrowInv, hasOtherReward: toReserve.Count > 0);
                    if (error != null) return error;
                }

                var goal = GetGoal(draft.Kind);
                if (goal == null) return LangKey("error-kind-not-allowed");

                error = goal.ValidatePost(this, author, draft);
                if (error != null) return error;

                draft.Id = Guid.NewGuid().ToString("N");
                draft.BoardKey = string.IsNullOrEmpty(draft.BoardKey) ? Key : draft.BoardKey;
                draft.Source = BoardSources.Player;
                draft.AuthorUid = author.PlayerUID;
                draft.AuthorName = author.PlayerName;
                draft.CreatedTotalDays = Sapi.World.Calendar.TotalDays;
                draft.State = BoardStates.Open;
                draft.ClaimedByUid = null;
                draft.ClaimedByName = null;
                draft.ExpireTotalDays = expireInDays > 0 ? draft.CreatedTotalDays + expireInDays : 0;

                foreach (var (value, source) in toReserve)
                {
                    error = source!.Reserve(this, author, draft, value.Choice, value.Amount);
                    if (error != null)
                    {
                        foreach (var done in reserved) done.Release(this, draft);
                        return error;
                    }
                    reserved.Add(source);
                }

                // Read the reward before Deposit empties the slots.
                string? escrowSummary = BoardEscrow.Summarize(escrowInv);
                if (!Escrow.Deposit(draft, escrowInv) && Options.PostRules.RequireEscrow && reserved.Count == 0)
                    return LangKey("error-escrow-empty");

                state.Contracts.Add(draft);
                if (goal.CountsForPostCooldown)
                    state.LastPostTotalDays[author.PlayerUID] = draft.CreatedTotalDays;

                goal.OnPosted(this, author, draft);
                Posted?.Invoke(new BoardContractEvent(this, draft, author) { EscrowSummary = escrowSummary });
                NotifyChanged();
                return null;
            }
            catch (Exception ex)
            {
                foreach (var done in reserved)
                {
                    try { done.Release(this, draft); }
                    catch (Exception releaseEx) { Sapi.Logger.Error("[Boards] Release after a failed post on {0}: {1}", Key, releaseEx); }
                }
                Sapi.Logger.Error("[Boards] Post on {0} failed for player {1}: {2}", Key, author?.PlayerName, ex);
                return LangKey("error-failed");
            }
        }

        private string? ValidateByRules(IServerPlayer author, BoardContract draft, IInventory? escrowInv, bool hasOtherReward)
        {
            var rules = Options.PostRules;
            if (rules.AllowedKinds.Count > 0 && !rules.AllowedKinds.Contains(draft.Kind ?? ""))
                return LangKey("error-kind-not-allowed");
            if (draft.Demand < 1 || (rules.MaxDemand > 0 && draft.Demand > rules.MaxDemand))
                return LangKey("error-demand");
            if (!string.IsNullOrWhiteSpace(draft.CustomTitle) && draft.CustomTitle!.Length > rules.MaxTitleLength)
                return LangKey("error-title");
            if (!string.IsNullOrWhiteSpace(draft.CustomDescription) && draft.CustomDescription!.Length > rules.MaxDescriptionLength)
                return LangKey("error-description");
            if (rules.MaxActivePostsPerPlayer > 0 && CountActivePosts(author.PlayerUID) >= rules.MaxActivePostsPerPlayer)
                return LangKey("error-max-posts");
            if (PostCooldownRemainingHours(author.PlayerUID, rules.PostCooldownHours) > 0)
                return LangKey("error-cooldown");

            int stacks = 0;
            if (escrowInv != null)
            {
                foreach (var slot in escrowInv)
                {
                    if (slot?.Empty != false) continue;
                    if (slot.Itemstack?.Collectible == null || slot.Itemstack.StackSize <= 0) return LangKey("error-reward");
                    stacks++;
                }
            }
            if (stacks == 0 && rules.RequireEscrow && !hasOtherReward) return LangKey("error-escrow-empty");
            if (stacks > rules.RewardSlotCount) return LangKey("error-reward");
            return null;
        }

        /// <summary>Puts a server-made contract on the board (generators, admins). Fills missing id, state and time.</summary>
        public void AddServerContract(BoardContract contract)
        {
            if (string.IsNullOrEmpty(contract.Id)) contract.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(contract.BoardKey)) contract.BoardKey = Key;
            contract.Source = BoardSources.Server;
            if (contract.CreatedTotalDays <= 0) contract.CreatedTotalDays = Sapi.World.Calendar.TotalDays;
            state.Contracts.Add(contract);
            NotifyChanged();
        }

        // --------------------------------------------------------------------
        //  Accept
        // --------------------------------------------------------------------

        public string? Accept(IServerPlayer player, string? contractId)
        {
            var contract = FindOpen(contractId);
            if (contract == null) return LangKey("error-not-open");
            if (contract.AuthorUid == player.PlayerUID) return LangKey("error-own-contract");

            var goal = GetGoal(contract.Kind);
            if (goal == null) return LangKey("error-failed");

            string? error = goal.ValidateAccept(this, player, contract) ?? Options.ValidateAccept?.Invoke(player, contract);
            if (error != null) return error;

            goal.OnAccepted(this, player, contract);

            contract.State = BoardStates.Claimed;
            contract.ClaimedByUid = player.PlayerUID;
            contract.ClaimedByName = player.PlayerName;
            contract.ClaimedTotalDays = Sapi.World.Calendar.TotalDays;

            Accepted?.Invoke(new BoardContractEvent(this, contract, player));
            NotifyChanged();
            return null;
        }

        // --------------------------------------------------------------------
        //  Turn in / complete
        // --------------------------------------------------------------------

        /// <summary>Hand-in from a window: checks the goal can be handed in first.</summary>
        public string? TurnIn(IServerPlayer player, string? contractId)
        {
            var contract = FindClaimedBy(contractId, player.PlayerUID);
            if (contract == null) return LangKey("error-not-claimed");

            var goal = GetGoal(contract.Kind);
            if (goal == null || !goal.IsCompletable(this, player, contract)) return LangKey("error-not-completable");

            return Complete(player, contract, force: false);
        }

        /// <summary>
        /// Hands the goal in and settles the contract. <paramref name="force" />: the goal was done elsewhere (a kill
        /// event and the like), skip the goal's own check.
        /// </summary>
        public string? Complete(IServerPlayer player, BoardContract contract, bool force)
        {
            if (contract.State != BoardStates.Claimed || contract.ClaimedByUid != player.PlayerUID)
                return LangKey("error-not-claimed");

            var goal = GetGoal(contract.Kind);
            if (goal == null) return LangKey("error-not-completable");

            completionInFlight.Add(contract.Id);
            bool completed;
            try
            {
                completed = goal.TryComplete(this, player, contract, force);
            }
            finally
            {
                completionInFlight.Remove(contract.Id);
            }
            if (!completed) return LangKey("error-not-completable");

            if (contract.Source == BoardSources.Player && goal.ConfirmDays > 0) MarkDone(player, contract, goal);
            else Settle(player, contract, goal);
            return null;
        }

        /// <summary>
        /// The goal reports it was completed outside <see cref="Complete" /> (an admin finished the tracked task):
        /// settle the contract so it does not stay claimed forever. Ignored while <see cref="Complete" /> runs for it.
        /// </summary>
        public void OnGoalCompletedElsewhere(BoardContract contract)
        {
            if (contract.State != BoardStates.Claimed || contract.ClaimedByUid == null) return;
            if (completionInFlight.Contains(contract.Id)) return;
            var goal = GetGoal(contract.Kind);
            if (goal == null) return;
            Settle(FindOnline(contract.ClaimedByUid), contract, goal);
        }

        /// <summary>As <see cref="OnGoalCompletedElsewhere(BoardContract)" />, for a claimer known to be online.</summary>
        public void OnGoalCompletedElsewhere(IServerPlayer player, BoardContract contract)
        {
            if (contract.State != BoardStates.Claimed || contract.ClaimedByUid != player.PlayerUID) return;
            if (completionInFlight.Contains(contract.Id)) return;
            var goal = GetGoal(contract.Kind);
            if (goal == null) return;
            Settle(player, contract, goal);
        }

        private void Settle(IServerPlayer? player, BoardContract contract, IBoardGoal goal)
        {
            if (contract.Source == BoardSources.Player)
            {
                goal.OnFulfilled(this, player, contract);
                NotifyAuthor(player, contract, goal);
                contract.ExpireTotalDays = 0;
                contract.State = BoardStates.Fulfilled;
            }
            else
            {
                if (player != null) PayClaimer(contract, player);
                contract.State = BoardStates.Completed;
                state.Contracts.Remove(contract);
            }

            if (player != null) Sapi.SendMessage(player, Options.NoticeChatGroup, Text("completed"), EnumChatType.Notification);
            Completed?.Invoke(new BoardContractEvent(this, contract, player));
            NotifyChanged();
        }

        private void NotifyAuthor(IServerPlayer? claimer, BoardContract contract, IBoardGoal goal)
        {
            if (claimer == null) return;
            var author = contract.AuthorUid != null ? FindOnline(contract.AuthorUid) : null;
            if (author == null) return;
            string key = goal.DeliversToAuthor ? "your-contract-fulfilled" : "your-contract-completed";
            Sapi.SendMessage(author, Options.NoticeChatGroup, Text(key, claimer.PlayerName), EnumChatType.Notification);
        }

        // --------------------------------------------------------------------
        //  Author confirmation (goals with ConfirmDays > 0)
        // --------------------------------------------------------------------

        /// <summary>Total days by which the author must answer a done contract (silence = accepted).</summary>
        public const string ConfirmDueKey = "arcanumlib:confirmdue";

        /// <summary>The author's reason for a dispute.</summary>
        public const string DisputeKey = "arcanumlib:dispute";

        private void MarkDone(IServerPlayer claimer, BoardContract contract, IBoardGoal goal)
        {
            double due = Sapi.World.Calendar.TotalDays + goal.ConfirmDays;
            contract.State = BoardStates.Done;
            contract.ExpireTotalDays = 0;
            contract.SetData(ConfirmDueKey, due.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

            Sapi.SendMessage(claimer, Options.NoticeChatGroup, Text("marked-done"), EnumChatType.Notification);
            var author = contract.AuthorUid != null ? FindOnline(contract.AuthorUid) : null;
            if (author != null)
                Sapi.SendMessage(author, Options.NoticeChatGroup, Text("your-contract-done", claimer.PlayerName), EnumChatType.Notification);
            NotifyChanged();
        }

        /// <summary>Total days the author's answer is due by, or 0 when the contract is not waiting for it.</summary>
        public static double ConfirmDue(BoardContract contract) =>
            double.TryParse(contract.GetData(ConfirmDueKey), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double due) ? due : 0;

        /// <summary>The author accepts the work: the claimer may collect the reward.</summary>
        public string? Confirm(IServerPlayer author, string? contractId)
        {
            var contract = Find(contractId);
            if (contract == null || !BoardStates.IsAwaiting(contract.State)) return LangKey("error-not-done");
            if (contract.AuthorUid != author.PlayerUID) return LangKey("error-not-author");
            AcceptWork(contract);
            return null;
        }

        private void AcceptWork(BoardContract contract)
        {
            var goal = GetGoal(contract.Kind);
            var claimer = contract.ClaimedByUid != null ? FindOnline(contract.ClaimedByUid) : null;
            contract.SetData(ConfirmDueKey, null);
            contract.SetData(DisputeKey, null);
            if (goal == null)
            {
                contract.State = BoardStates.Fulfilled;
                NotifyChanged();
                return;
            }
            if (claimer != null)
                Sapi.SendMessage(claimer, Options.NoticeChatGroup, Text("your-work-accepted"), EnumChatType.Notification);
            Settle(claimer, contract, goal);
        }

        /// <summary>
        /// The author disputes the work: the reward stays reserved until an admin decides
        /// (<see cref="Resolve" />). The claimer is told the reason.
        /// </summary>
        public string? Dispute(IServerPlayer author, string? contractId, string? reason)
        {
            var contract = Find(contractId);
            if (contract == null || contract.State != BoardStates.Done) return LangKey("error-not-done");
            if (contract.AuthorUid != author.PlayerUID) return LangKey("error-not-author");
            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason)) return LangKey("error-dispute-reason");
            if (reason.Length > 200) reason = reason.Substring(0, 200);

            contract.State = BoardStates.Disputed;
            contract.SetData(ConfirmDueKey, null);
            contract.SetData(DisputeKey, reason);

            var claimer = contract.ClaimedByUid != null ? FindOnline(contract.ClaimedByUid) : null;
            if (claimer != null)
                Sapi.SendMessage(claimer, Options.NoticeChatGroup, Text("your-work-disputed", author.PlayerName, reason), EnumChatType.Notification);
            Sapi.Logger.Notification("[ArcanumLib] Board {0}: contract {1} disputed by {2}: {3}", Key, contract.Id, author.PlayerName, reason);
            Disputed?.Invoke(new BoardContractEvent(this, contract, author) { Reason = reason });
            NotifyChanged();
            return null;
        }

        /// <summary>Admin decision on a done or disputed contract: pay the claimer, or refund the author.</summary>
        public bool Resolve(string contractId, bool pay)
        {
            var contract = Find(contractId);
            if (contract == null || !BoardStates.IsAwaiting(contract.State)) return false;
            string? reason = contract.GetData(DisputeKey);
            if (pay) AcceptWork(contract);
            else ForceCancel(contractId);
            Resolved?.Invoke(new BoardContractEvent(this, contract, null) { Reason = reason, Paid = pay });
            return true;
        }

        /// <summary>Pays the whole reward (escrow + reward sources) to the claimer.</summary>
        public void PayClaimer(BoardContract contract, IServerPlayer claimer)
        {
            Escrow.Payout(contract, claimer);
            foreach (var source in RewardSources) source.Pay(this, contract, claimer);
        }

        /// <summary>Gives back every reserved reward (escrow to the author, reward sources release theirs).</summary>
        public void ReleaseRewards(BoardContract contract)
        {
            Escrow.Refund(contract);
            foreach (var source in RewardSources) source.Release(this, contract);
        }

        // --------------------------------------------------------------------
        //  Collect
        // --------------------------------------------------------------------

        private const string RewardPaidKey = "arcanumlib:rewardpaid";

        /// <summary>
        /// From a fulfilled player contract: the author collects what the goal delivered, the claimer collects the
        /// reward. The contract leaves the board when both are done.
        /// </summary>
        public string? Collect(IServerPlayer player, string? contractId)
        {
            var contract = Find(contractId);
            if (contract == null) return LangKey("error-not-open");
            if (contract.State != BoardStates.Fulfilled) return LangKey("error-not-fulfilled");

            var goal = GetGoal(contract.Kind);
            bool delivers = goal?.DeliversToAuthor == true;

            if (delivers && contract.AuthorUid == player.PlayerUID)
            {
                if (contract.PendingItemStackBytes != null)
                {
                    foreach (var bytes in contract.PendingItemStackBytes)
                    {
                        var stack = DeserializeStack(bytes);
                        if (stack != null) player.TryGiveOrDrop(stack);
                    }
                    contract.PendingItemStackBytes = null;
                }
            }
            else if (contract.Source == BoardSources.Player && contract.ClaimedByUid == player.PlayerUID)
            {
                PayClaimer(contract, player);
                contract.SetData(RewardPaidKey, "1");
            }
            else
            {
                return LangKey("error-not-collectible");
            }

            bool authorDone = !delivers || contract.PendingItemStackBytes == null;
            bool rewardDone = contract.GetData(RewardPaidKey) != null
                || (!Escrow.Holds(contract) && !RewardSources.Any(s => s.Holds(this, contract)));
            if (authorDone && rewardDone)
            {
                contract.State = BoardStates.Completed;
                state.Contracts.Remove(contract);
            }

            Collected?.Invoke(new BoardContractEvent(this, contract, player));
            NotifyChanged();
            return null;
        }

        // --------------------------------------------------------------------
        //  Abandon / cancel / expire
        // --------------------------------------------------------------------

        /// <summary>The claimer gives the contract up; it is open again.</summary>
        public string? Abandon(IServerPlayer player, string? contractId)
        {
            var contract = FindClaimedBy(contractId, player.PlayerUID);
            if (contract == null) return LangKey("error-not-claimed");

            GetGoal(contract.Kind)?.OnReleased(this, player.PlayerUID, player, contract);

            contract.State = BoardStates.Open;
            contract.ClaimedByUid = null;
            contract.ClaimedByName = null;
            contract.ClaimedTotalDays = 0;

            Abandoned?.Invoke(new BoardContractEvent(this, contract, player));
            NotifyChanged();
            return null;
        }

        /// <summary>The author takes back an open contract of theirs; the escrow is refunded.</summary>
        public string? Cancel(IServerPlayer player, string? contractId)
        {
            var contract = Find(contractId);
            if (contract == null || contract.Source != BoardSources.Player) return LangKey("error-not-open");
            if (contract.AuthorUid != player.PlayerUID) return LangKey("error-not-author");
            if (contract.State != BoardStates.Open) return LangKey("error-not-open");

            ReleaseRewards(contract);
            contract.State = BoardStates.Cancelled;
            state.Contracts.Remove(contract);

            Cancelled?.Invoke(new BoardContractEvent(this, contract, player));
            NotifyChanged();
            return null;
        }

        /// <summary>
        /// Admin removal of any contract still on the board: the claimer's tracking is dropped, reserved rewards and
        /// undelivered author items are returned. Returns false when there is no such contract.
        /// </summary>
        public bool ForceCancel(string contractId)
        {
            var contract = Find(contractId);
            if (contract == null) return false;

            if (contract.State == BoardStates.Claimed && contract.ClaimedByUid != null)
                GetGoal(contract.Kind)?.OnReleased(this, contract.ClaimedByUid, FindOnline(contract.ClaimedByUid), contract);

            ReleaseRewards(contract);
            if (contract.PendingItemStackBytes != null)
            {
                foreach (var bytes in contract.PendingItemStackBytes) ReturnToAuthor(contract, bytes);
                contract.PendingItemStackBytes = null;
            }

            contract.State = BoardStates.Cancelled;
            state.Contracts.Remove(contract);
            Cancelled?.Invoke(new BoardContractEvent(this, contract, null));
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// Gives a stack a goal kept for the contract back to its author: into the inventory when online, otherwise kept
        /// for them like an escrow refund.
        /// </summary>
        public void ReturnToAuthor(BoardContract contract, ItemStack stack) => ReturnToAuthor(contract, stack.ToBytes());

        private void ReturnToAuthor(BoardContract contract, byte[] bytes)
        {
            var author = contract.AuthorUid != null ? FindOnline(contract.AuthorUid) : null;
            var stack = author != null ? DeserializeStack(bytes) : null;
            if (author != null && stack != null) author.TryGiveOrDrop(stack);
            else if (author == null) ReturnOffline(contract, bytes);
        }

        /// <summary>
        /// Removes open and claimed contracts whose time ran out, returning their rewards; done contracts the author
        /// did not answer in time count as accepted.
        /// </summary>
        public int SweepExpiry()
        {
            double now = Sapi.World.Calendar.TotalDays;
            var unanswered = state.Contracts.FindAll(c => c.State == BoardStates.Done && ConfirmDue(c) is > 0 and var due && due <= now);
            foreach (var contract in unanswered) AcceptWork(contract);

            var expired = state.Contracts.FindAll(c => c.ExpireTotalDays > 0 && c.ExpireTotalDays <= now && BoardStates.IsActive(c.State));

            foreach (var contract in expired)
            {
                if (contract.State == BoardStates.Claimed && contract.ClaimedByUid != null)
                {
                    // Also for offline claimers: their tracked task would otherwise stay as a dead entry.
                    GetGoal(contract.Kind)?.OnReleased(this, contract.ClaimedByUid, FindOnline(contract.ClaimedByUid), contract);
                }

                ReleaseRewards(contract);
                contract.State = BoardStates.Expired;
                state.Contracts.Remove(contract);
                Expired?.Invoke(new BoardContractEvent(this, contract, null));
            }

            if (expired.Count > 0) NotifyChanged();
            return expired.Count + unanswered.Count;
        }

        /// <summary>Hands out refunds and deliveries kept for this player while they were offline (own state only).</summary>
        public int DeliverPendingReturns(IServerPlayer player)
        {
            if (state is not BoardState own || !own.PendingReturnsByUid.TryGetValue(player.PlayerUID, out var list)) return 0;
            own.PendingReturnsByUid.Remove(player.PlayerUID);
            int given = 0;
            foreach (var bytes in list)
            {
                var stack = DeserializeStack(bytes);
                if (stack == null) continue;
                player.TryGiveOrDrop(stack);
                given++;
            }
            return given;
        }
    }
}
