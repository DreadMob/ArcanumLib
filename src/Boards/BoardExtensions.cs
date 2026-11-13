using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// A kind of goal a contract can have ("bring 64 iron ingots", "kill a boss", "a service the author confirms").
    /// Registered on a board under <see cref="Kind" />; the board asks it at every step of the contract.
    /// Derive from <see cref="BoardGoalBase" /> to override only what the goal needs.
    /// </summary>
    public interface IBoardGoal
    {
        /// <summary>The <see cref="BoardContract.Kind" /> this goal handles.</summary>
        string Kind { get; }

        /// <summary>
        /// The goal hands something to the author when it is fulfilled (<see cref="BoardContract.PendingItemStackBytes" />):
        /// the author is told "fulfilled, come and collect" and has to collect it before the contract leaves the board.
        /// </summary>
        bool DeliversToAuthor { get; }

        /// <summary>A post of this kind sets the author's post cooldown (<see cref="IBoardState.LastPostTotalDays" />).</summary>
        bool CountsForPostCooldown { get; }

        /// <summary>Checks a player's post before anything is escrowed. Returns an error lang key or null.</summary>
        string? ValidatePost(ContractBoard board, IServerPlayer author, BoardContract draft);

        /// <summary>The contract has just been posted (escrow taken, contract on the board).</summary>
        void OnPosted(ContractBoard board, IServerPlayer author, BoardContract contract);

        /// <summary>Checks whether this player may take the contract. Returns an error lang key or null.</summary>
        string? ValidateAccept(ContractBoard board, IServerPlayer player, BoardContract contract);

        /// <summary>The player took the contract: start tracking it for them.</summary>
        void OnAccepted(ContractBoard board, IServerPlayer player, BoardContract contract);

        /// <summary>A claimed contract was loaded (server start, reload): re-attach its tracking.</summary>
        void OnLoaded(ContractBoard board, BoardContract contract);

        /// <summary>The claimer can hand the contract in right now.</summary>
        bool IsCompletable(ContractBoard board, IServerPlayer player, BoardContract contract);

        /// <summary>
        /// Hands the goal in (takes delivered items and the like). <paramref name="force" /> completes it without
        /// the usual check (the goal was done elsewhere, e.g. a kill seen by an event). Returns false when nothing
        /// was handed in.
        /// </summary>
        bool TryComplete(ContractBoard board, IServerPlayer player, BoardContract contract, bool force);

        /// <summary>
        /// The claimer gave the contract up or it ran out: stop tracking it. <paramref name="player" /> is null when
        /// the claimer is offline.
        /// </summary>
        void OnReleased(ContractBoard board, string playerUid, IServerPlayer? player, BoardContract contract);

        /// <summary>A player contract is fulfilled: keep what it delivers for the author.</summary>
        void OnFulfilled(ContractBoard board, IServerPlayer? claimer, BoardContract contract);

        /// <summary>
        /// Days the author has to accept or dispute a player contract the claimer marked done; 0 = the goal checks
        /// itself and settles at once. Silence counts as accepted.
        /// </summary>
        double ConfirmDays { get; }
    }

    /// <summary>Do-nothing defaults for <see cref="IBoardGoal" />.</summary>
    public abstract class BoardGoalBase : IBoardGoal
    {
        public abstract string Kind { get; }
        public virtual bool DeliversToAuthor => false;
        public virtual bool CountsForPostCooldown => true;
        public virtual string? ValidatePost(ContractBoard board, IServerPlayer author, BoardContract draft) => null;
        public virtual void OnPosted(ContractBoard board, IServerPlayer author, BoardContract contract) { }
        public virtual string? ValidateAccept(ContractBoard board, IServerPlayer player, BoardContract contract) => null;
        public virtual void OnAccepted(ContractBoard board, IServerPlayer player, BoardContract contract) { }
        public virtual void OnLoaded(ContractBoard board, BoardContract contract) { }
        public abstract bool IsCompletable(ContractBoard board, IServerPlayer player, BoardContract contract);
        public abstract bool TryComplete(ContractBoard board, IServerPlayer player, BoardContract contract, bool force);
        public virtual void OnReleased(ContractBoard board, string playerUid, IServerPlayer? player, BoardContract contract) { }
        public virtual void OnFulfilled(ContractBoard board, IServerPlayer? claimer, BoardContract contract) { }
        public virtual double ConfirmDays => 0;
    }

    /// <summary>
    /// A reward besides the escrowed items: currency from an account, a fund, reputation… The board calls
    /// <see cref="Pay" /> when the claimer gets the reward (on completion for server contracts, on collect for player
    /// ones) and <see cref="Release" /> when the contract is cancelled or runs out. Reserving at post time is the
    /// source's own business (validation hook or the <see cref="ContractBoard.Posted" /> event).
    /// </summary>
    public interface IBoardRewardSource
    {
        /// <summary>Pays this source's part of the reward to the claimer.</summary>
        void Pay(ContractBoard board, BoardContract contract, IServerPlayer claimer);

        /// <summary>Gives back what was reserved for the contract (to the author, a fund…).</summary>
        void Release(ContractBoard board, BoardContract contract);

        /// <summary>Still holds an unpaid reward of this contract (keeps a fulfilled contract on the board).</summary>
        bool Holds(ContractBoard board, BoardContract contract);
    }

    /// <summary>What happened to a contract, for <see cref="ContractBoard" /> events.</summary>
    public sealed class BoardContractEvent
    {
        public ContractBoard Board { get; }
        public BoardContract Contract { get; }

        /// <summary>The acting player: author for posts and cancels, claimer otherwise; null for expiry.</summary>
        public IServerPlayer? Player { get; }

        /// <summary>For posts: the escrowed stacks as "name ×count, …", read before they were taken.</summary>
        public string? EscrowSummary { get; init; }

        public BoardContractEvent(ContractBoard board, BoardContract contract, IServerPlayer? player)
        {
            Board = board;
            Contract = contract;
            Player = player;
        }
    }

    /// <summary>How much of the post check <see cref="ContractBoard.Post" /> runs.</summary>
    public enum BoardPostCheck
    {
        /// <summary>The board's check (<see cref="BoardOptions.ValidatePost" /> or the default rules), then the goal's.</summary>
        Full,

        /// <summary>Only the goal's check: the caller has its own posting rules for this kind.</summary>
        GoalOnly
    }

    /// <summary>Limits of the default post check (<see cref="BoardOptions.ValidatePost" /> left null).</summary>
    public sealed class BoardPostRules
    {
        /// <summary>Goal kinds players may post; empty = every registered goal.</summary>
        public List<string> AllowedKinds { get; set; } = new();
        public int MaxDemand { get; set; } = 9999;
        public int MaxTitleLength { get; set; } = 60;
        public int MaxDescriptionLength { get; set; } = 400;
        public int MaxActivePostsPerPlayer { get; set; } = 5;
        public double PostCooldownHours { get; set; }
        public int RewardSlotCount { get; set; } = 4;

        /// <summary>A post must put at least one stack into escrow.</summary>
        public bool RequireEscrow { get; set; } = true;

        /// <summary>Players may post on the board at all (the ArcanumLib window shows its post button).</summary>
        public bool AllowPlayerPosts { get; set; } = true;

        /// <summary>Run-out choices of the post window, in days; 0 as chosen value = never.</summary>
        public double ExpireDaysMin { get; set; } = 1;
        public double ExpireDaysMax { get; set; } = 30;
        public double ExpireDaysDefault { get; set; } = 7;

        /// <summary>Clamps a chosen run-out to the limits (0 stays 0 = never).</summary>
        public double ClampExpireDays(double days)
        {
            if (days <= 0) return 0;
            double min = Math.Max(0, ExpireDaysMin);
            double max = Math.Max(min, ExpireDaysMax);
            return max > 0 ? Math.Max(min, Math.Min(max, days)) : days;
        }
    }

    /// <summary>Behaviour of a <see cref="ContractBoard" />. Everything has a default.</summary>
    public sealed class BoardOptions
    {
        /// <summary>
        /// Prefix of every message key the board uses ("error-not-open", "completed", …). The Quest Framework keeps
        /// its own texts with "alegacyvsquest:bounty-".
        /// </summary>
        public string LangPrefix { get; set; } = "arcanumlib:board-";

        /// <summary>Server-side text lookup (default <c>Lang.Get</c>).</summary>
        public System.Func<string, object[], string>? Localize { get; set; }

        /// <summary>Full post check; replaces the default rules. Returns an error lang key or null.</summary>
        public System.Func<IServerPlayer, BoardContract, IInventory?, string?>? ValidatePost { get; set; }

        /// <summary>Limits used when <see cref="ValidatePost" /> is null.</summary>
        public BoardPostRules PostRules { get; set; } = new();

        /// <summary>Extra check when taking a contract (e.g. active contract limits). Returns an error lang key or null.</summary>
        public System.Func<IServerPlayer, BoardContract, string?>? ValidateAccept { get; set; }

        /// <summary>
        /// Where an escrowed stack goes when it must be returned to an offline author. Boards of
        /// <see cref="BoardRegistry" /> keep it for the author until they join.
        /// </summary>
        public Action<IBoardState, BoardContract, byte[]>? OfflineRefund { get; set; }

        /// <summary>Finds an online player (default: the ArcanumLib online player cache).</summary>
        public System.Func<string, IServerPlayer?>? FindOnlinePlayer { get; set; }

        /// <summary>Chat group of the board's notices to players.</summary>
        public int NoticeChatGroup { get; set; } = Vintagestory.API.Config.GlobalConstants.InfoLogChatGroup;
    }
}
