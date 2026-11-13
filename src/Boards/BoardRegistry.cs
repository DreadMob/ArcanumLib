using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Boards.Gui;
using ArcanumLib.Inventory;
using ArcanumLib.PlayerTransfer;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// The contract boards of the world. A mod either creates a board here (<see cref="Create" />: its state is
    /// stored in the world save, runs out on its own and moves with <c>/arcanum transfer</c>) or registers a board
    /// it keeps itself (<see cref="Register" />). Board blocks find their board by key; the ArcanumLib board window
    /// talks to any registered board over the <c>arcanumlib:boards</c> channel.
    /// </summary>
    public class BoardRegistry : ModSystem
    {
        public const string ChannelId = "arcanumlib:boards";
        private const string SaveKeyPrefix = "arcanumlib:board:";
        private const string PostInventoryPrefix = "arcanumboardpost-";

        private sealed class OwnedBoard
        {
            public ContractBoard Board = null!;
            public string SaveKey = "";
            public string TitleKey = "";
        }

        private ICoreServerAPI? sapi;
        private ICoreClientAPI? capi;
        private IServerNetworkChannel? serverChannel;
        private IClientNetworkChannel? clientChannel;
        private readonly Dictionary<string, ContractBoard> boards = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, OwnedBoard> owned = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> titleKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> openPostBoards = new(StringComparer.OrdinalIgnoreCase);
        private bool loaded;
        private long tickId;

        public override double ExecuteOrder() => 0.2;
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        /// <summary>Registered boards by key.</summary>
        public IReadOnlyDictionary<string, ContractBoard> Boards => boards;

        public ContractBoard? Get(string? key) => key != null && boards.TryGetValue(key, out var b) ? b : null;

        // --------------------------------------------------------------------
        //  Server
        // --------------------------------------------------------------------

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.RegisterChannel(ChannelId)
                .RegisterMessageType<BoardActionMessage>()
                .RegisterMessageType<BoardPostMessage>()
                .RegisterMessageType<BoardPagePacket>()
                .RegisterMessageType<BoardOpenPostPacket>()
                .RegisterMessageType<BoardResultPacket>()
                .RegisterMessageType<BoardChangedPacket>()
                .SetMessageHandler<BoardActionMessage>(OnAction)
                .SetMessageHandler<BoardPostMessage>(OnPost);

            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += SaveOwned;
            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.PlayerLeave += OnPlayerLeave;
            tickId = api.Event.RegisterGameTickListener(_ => { foreach (var o in owned.Values) o.Board.SweepExpiry(); }, 30000);

            BoardCommands.Register(api, this);
            PlayerDataTransfer.Register("arcanumlib:boards", TransferOwned);
        }

        /// <summary>
        /// Creates a board stored by ArcanumLib (a <see cref="BoardState" /> under "arcanumlib:board:{key}" in the world
        /// save). Call it in <c>StartServerSide</c>; register goals and reward sources on the result.
        /// </summary>
        /// <param name="titleKey">Lang key of the board window's title.</param>
        public ContractBoard Create(string key, BoardOptions? options = null, string? titleKey = null)
        {
            if (sapi == null) throw new InvalidOperationException("Boards are created on the server.");
            if (boards.ContainsKey(key)) throw new InvalidOperationException($"Board '{key}' already exists.");

            var board = new ContractBoard(sapi, key, options);
            var entry = new OwnedBoard { Board = board, SaveKey = SaveKeyPrefix + key, TitleKey = titleKey ?? "arcanumlib:board-title" };
            owned[key] = entry;
            Register(board, entry.TitleKey);
            if (loaded) Load(entry);
            return board;
        }

        /// <summary>Registers a board a mod keeps itself (state, saving, expiry ticks are the mod's).</summary>
        public void Register(ContractBoard board, string? titleKey = null)
        {
            boards[board.Key] = board;
            titleKeys[board.Key] = titleKey ?? "arcanumlib:board-title";
            board.Changed += () => BroadcastChanged(board.Key);
        }

        /// <summary>Opens the board for a player: its own <see cref="ContractBoard.Opener" />, else the ArcanumLib window.</summary>
        public bool Open(IServerPlayer player, string boardKey)
        {
            var board = Get(boardKey);
            if (board == null) return false;
            if (board.Opener != null) board.Opener(player);
            else SendPage(player, board);
            return true;
        }

        private void OnSaveGameLoaded()
        {
            loaded = true;
            foreach (var entry in owned.Values) Load(entry);
        }

        private void Load(OwnedBoard entry)
        {
            BoardState? state = null;
            try
            {
                state = sapi!.WorldManager.SaveGame.GetData<BoardState>(entry.SaveKey);
            }
            catch (Exception ex)
            {
                sapi!.Logger.Error("[Boards] Failed to read board {0} ({1}); starting empty.", entry.Board.Key, ex.Message);
            }
            state ??= new BoardState { BoardKey = entry.Board.Key };
            entry.Board.State = state.Normalize();
            entry.Board.ReattachClaimed();
        }

        private void SaveOwned()
        {
            if (sapi == null || !loaded) return;
            foreach (var entry in owned.Values)
                sapi.WorldManager.SaveGame.StoreData(entry.SaveKey, (BoardState)entry.Board.State);
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            foreach (var entry in owned.Values) entry.Board.DeliverPendingReturns(player);
        }

        private void OnPlayerLeave(IServerPlayer player)
        {
            // Items left in the post window's slots would otherwise be lost.
            string invId = PostInventoryPrefix + player.PlayerUID;
            openPostBoards.Remove(invId);
            var inv = player.InventoryManager?.GetInventory(invId);
            if (inv != null) ReturnItems(player, inv, close: false);
        }

        private void TransferOwned(PlayerTransferContext ctx)
        {
            int moved = 0;
            foreach (var entry in owned.Values)
            {
                if (entry.Board.State is not BoardState st) continue;
                foreach (var c in st.Contracts)
                {
                    if (c.AuthorUid == ctx.FromUid) { c.AuthorUid = ctx.ToUid; c.AuthorName = ctx.ToName; moved++; }
                    if (c.ClaimedByUid == ctx.FromUid) { c.ClaimedByUid = ctx.ToUid; c.ClaimedByName = ctx.ToName; moved++; }
                    if (c.TargetPlayerUid == ctx.FromUid) { c.TargetPlayerUid = ctx.ToUid; c.TargetPlayerName = ctx.ToName; }
                }
                if (st.LastPostTotalDays.Remove(ctx.FromUid, out double last))
                    st.LastPostTotalDays[ctx.ToUid] = Math.Max(last, st.LastPostTotalDays.TryGetValue(ctx.ToUid, out var t) ? t : 0);
                if (st.PendingReturnsByUid.Remove(ctx.FromUid, out var returns))
                {
                    if (!st.PendingReturnsByUid.TryGetValue(ctx.ToUid, out var to)) st.PendingReturnsByUid[ctx.ToUid] = to = new List<byte[]>();
                    to.AddRange(returns);
                }
            }
            if (moved > 0) ctx.Note("arcanumlib:boards", $"{moved} contract role(s) moved");
        }

        // ---- server network ----

        private void OnAction(IServerPlayer player, BoardActionMessage msg)
        {
            var board = Get(msg?.BoardKey);
            if (board == null || msg == null) return;

            string? error;
            string? info = null;
            switch (msg.Action)
            {
                case BoardActions.Open: SendPage(player, board); return;
                case BoardActions.Accept: error = board.Accept(player, msg.ContractId); break;
                case BoardActions.TurnIn: error = board.TurnIn(player, msg.ContractId); break;
                case BoardActions.Abandon: error = board.Abandon(player, msg.ContractId); break;
                case BoardActions.Cancel: error = board.Cancel(player, msg.ContractId); break;
                case BoardActions.Collect: error = board.Collect(player, msg.ContractId); info = "arcanumlib:board-collected"; break;
                case BoardActions.OpenPost: OpenPost(player, board); return;
                case BoardActions.ClosePost: ClosePost(player); return;
                case BoardActions.Confirm: error = board.Confirm(player, msg.ContractId); info = "arcanumlib:board-confirmed"; break;
                case BoardActions.Dispute: error = board.Dispute(player, msg.ContractId, msg.Text); info = "arcanumlib:board-disputed"; break;
                default: return;
            }
            SendResult(player, board, error, info);
        }

        private void SendPage(IServerPlayer player, ContractBoard board)
        {
            var packet = new BoardPagePacket
            {
                BoardKey = board.Key,
                TitleKey = titleKeys.TryGetValue(board.Key, out var t) ? t : "arcanumlib:board-title",
                Contracts = board.State.Contracts.ToList(),
                CanPost = board.Options.PostRules.AllowPlayerPosts && board.Goals.OfType<IPostableBoardGoal>().Any(),
                NowTotalDays = sapi!.World.Calendar.TotalDays,
                LangPrefix = board.Options.LangPrefix,
                Goals = board.Goals.OfType<IPostableBoardGoal>()
                    .Select(g => new BoardGoalInfo { Kind = g.Kind, NameKey = g.NameKey, HasDemand = g.HasDemand, ConfirmDays = g.ConfirmDays, TargetIsItem = g.TargetIsItem }).ToList()
            };
            serverChannel!.SendPacket(packet, player);
        }

        private void OpenPost(IServerPlayer player, ContractBoard board)
        {
            var rules = board.Options.PostRules;
            if (!rules.AllowPlayerPosts) { SendResult(player, board, board.LangKey("error-posts-disabled"), null); return; }

            string invId = PostInventoryPrefix + player.PlayerUID;
            var existing = player.InventoryManager.GetInventory(invId);
            if (existing != null)
            {
                ReturnItems(player, existing, close: false);
                player.InventoryManager.CloseInventory(existing);
            }

            int slots = Math.Max(1, rules.RewardSlotCount);
            var inv = new InventoryGeneric(slots, invId, sapi, (_, owner) => new BoardSlot(owner));
            player.InventoryManager.OpenInventory(inv);
            openPostBoards[invId] = board.Key;

            var packet = new BoardOpenPostPacket
            {
                BoardKey = board.Key,
                InventoryId = invId,
                SlotCount = slots,
                MaxDemand = rules.MaxDemand,
                ExpireDaysMin = rules.ExpireDaysMin,
                ExpireDaysMax = rules.ExpireDaysMax,
                ExpireDaysDefault = rules.ExpireDaysDefault,
                CooldownRemainingHours = board.PostCooldownRemainingHours(player.PlayerUID, rules.PostCooldownHours),
                MaxTitleLength = rules.MaxTitleLength,
                MaxDescriptionLength = rules.MaxDescriptionLength
            };
            foreach (var goal in board.Goals.OfType<IPostableBoardGoal>())
            {
                if (rules.AllowedKinds.Count > 0 && !rules.AllowedKinds.Contains(goal.Kind)) continue;
                packet.Goals.Add(new BoardGoalInfo
                {
                    Kind = goal.Kind,
                    NameKey = goal.NameKey,
                    Targets = goal.TargetOptions(board, player).ToList(),
                    HasDemand = goal.HasDemand,
                    ConfirmDays = goal.ConfirmDays,
                    TargetIsItem = goal.TargetIsItem
                });
            }
            serverChannel!.SendPacket(packet, player);
        }

        private void OnPost(IServerPlayer player, BoardPostMessage msg)
        {
            var board = Get(msg?.BoardKey);
            if (board == null || msg == null) return;

            string invId = PostInventoryPrefix + player.PlayerUID;
            var inv = player.InventoryManager.GetInventory(invId);
            if (inv == null || !openPostBoards.TryGetValue(invId, out var key) || !string.Equals(key, board.Key, StringComparison.OrdinalIgnoreCase))
            {
                SendResult(player, board, board.LangKey("error-failed"), null);
                return;
            }

            var goal = board.GetGoal(msg.Kind) as IPostableBoardGoal;
            var draft = new BoardContract
            {
                BoardKey = board.Key,
                Kind = msg.Kind,
                TargetCode = msg.TargetCode?.Trim(),
                Demand = goal != null && !goal.HasDemand ? 1 : msg.Demand,
                CustomTitle = string.IsNullOrWhiteSpace(msg.Title) ? null : msg.Title.Trim(),
                CustomDescription = string.IsNullOrWhiteSpace(msg.Description) ? null : msg.Description.Trim()
            };

            string? error = goal == null
                ? board.LangKey("error-kind-not-allowed")
                : board.Post(player, draft, inv, board.Options.PostRules.ClampExpireDays(msg.ExpireDays));
            if (error == null)
            {
                openPostBoards.Remove(invId);
                player.InventoryManager.CloseInventory(inv);
            }
            SendResult(player, board, error, "arcanumlib:board-posted");
        }

        private void ClosePost(IServerPlayer player)
        {
            string invId = PostInventoryPrefix + player.PlayerUID;
            if (!openPostBoards.Remove(invId)) return;
            var inv = player.InventoryManager.GetInventory(invId);
            if (inv != null) ReturnItems(player, inv, close: true);
        }

        private static void ReturnItems(IServerPlayer player, IInventory inv, bool close)
        {
            foreach (var slot in inv)
            {
                if (slot?.Empty != false) continue;
                var stack = slot.Itemstack!;
                slot.Itemstack = null;
                slot.MarkDirty();
                player.TryGiveOrDrop(stack);
            }
            if (close) player.InventoryManager.CloseInventory(inv);
        }

        private void SendResult(IServerPlayer player, ContractBoard board, string? error, string? info)
        {
            serverChannel?.SendPacket(new BoardResultPacket
            {
                BoardKey = board.Key,
                Success = error == null,
                ErrorKey = error,
                InfoKey = error == null ? info : null
            }, player);
        }

        private void BroadcastChanged(string key)
        {
            serverChannel?.BroadcastPacket(new BoardChangedPacket { BoardKey = key });
        }

        // --------------------------------------------------------------------
        //  Client
        // --------------------------------------------------------------------

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.RegisterChannel(ChannelId)
                .RegisterMessageType<BoardActionMessage>()
                .RegisterMessageType<BoardPostMessage>()
                .RegisterMessageType<BoardPagePacket>()
                .RegisterMessageType<BoardOpenPostPacket>()
                .RegisterMessageType<BoardResultPacket>()
                .RegisterMessageType<BoardChangedPacket>()
                .SetMessageHandler<BoardPagePacket>(p => GuiDialogContractBoard.Show(api, p))
                .SetMessageHandler<BoardOpenPostPacket>(p => GuiDialogBoardPost.Show(api, p))
                .SetMessageHandler<BoardResultPacket>(OnResult)
                .SetMessageHandler<BoardChangedPacket>(p =>
                {
                    if (GuiDialogContractBoard.Current?.BoardKey == p.BoardKey) SendAction(p.BoardKey, BoardActions.Open);
                });
        }

        /// <summary>Client: sends a board action.</summary>
        public void SendAction(string boardKey, string action, string? contractId = null, string? text = null)
        {
            clientChannel?.SendPacket(new BoardActionMessage { BoardKey = boardKey, Action = action, ContractId = contractId, Text = text });
        }

        /// <summary>Client: sends a post.</summary>
        public void SendPost(BoardPostMessage message) => clientChannel?.SendPacket(message);

        private void OnResult(BoardResultPacket packet)
        {
            if (capi == null) return;
            if (packet.Success)
            {
                GuiDialogBoardPost.Current?.OnPosted();
                if (!string.IsNullOrEmpty(packet.InfoKey)) capi.ShowChatMessage(Lang.Get(packet.InfoKey));
            }
            else if (!string.IsNullOrEmpty(packet.ErrorKey))
            {
                capi.TriggerIngameError(this, "board-error", Lang.Get(packet.ErrorKey));
            }
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                sapi.Event.SaveGameLoaded -= OnSaveGameLoaded;
                sapi.Event.GameWorldSave -= SaveOwned;
                sapi.Event.PlayerJoin -= OnPlayerJoin;
                sapi.Event.PlayerLeave -= OnPlayerLeave;
                if (tickId != 0) sapi.Event.UnregisterGameTickListener(tickId);
            }
            boards.Clear();
            owned.Clear();
            base.Dispose();
        }
    }

    /// <summary>Slot of the post window: takes any stack; the server checks the post.</summary>
    public class BoardSlot : ItemSlot
    {
        public BoardSlot(InventoryBase inventory) : base(inventory) { }

        public override bool CanHold(ItemSlot sourceSlot) => sourceSlot?.Itemstack != null;
    }
}
