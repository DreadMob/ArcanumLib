using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcanumLib.Boards;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.AtlasTests;

/// <summary>
/// The contract board core on a live server: escrow in and out, every step of a contract's life, goals and reward
/// sources as extensions, offline returns, saving and the admin command.
/// </summary>
[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class ContractBoardAtlasTests : AtlasScenarioBase
{
    private ICoreServerAPI Sapi => (ICoreServerAPI)World.Api;
    private BoardRegistry Registry => Sapi.ModLoader.GetModSystem<BoardRegistry>();

    /// <summary>A goal the test switches done by hand.</summary>
    private sealed class FlagGoal : BoardGoalBase, IPostableBoardGoal
    {
        public bool Done;
        public bool Deliver;
        public readonly List<string> Log = new();
        public override string Kind => "test";
        public override bool DeliversToAuthor => Deliver;
        public string NameKey => "test-goal";
        public bool HasDemand => true;
        public IReadOnlyList<string> TargetOptions(ContractBoard board, IServerPlayer player) => new[] { "game:gear-rusty" };
        public override void OnAccepted(ContractBoard board, IServerPlayer player, BoardContract contract) => Log.Add("accepted");
        public override bool IsCompletable(ContractBoard board, IServerPlayer player, BoardContract contract) => Done;
        public override bool TryComplete(ContractBoard board, IServerPlayer player, BoardContract contract, bool force) { Log.Add("complete"); return Done || force; }
        public override void OnReleased(ContractBoard board, string playerUid, IServerPlayer? player, BoardContract contract) => Log.Add("released:" + (player == null ? "offline" : "online"));
        public override void OnFulfilled(ContractBoard board, IServerPlayer claimer, BoardContract contract)
        {
            if (!Deliver) return;
            var gear = board.Sapi.World.GetItem(new AssetLocation("game:gear-rusty"))!;
            contract.PendingItemStackBytes = new List<byte[]> { new ItemStack(gear, 2).ToBytes() };
        }
    }

    private sealed class CountingSource : IBoardRewardSource
    {
        public int Paid, Released;
        public void Pay(ContractBoard board, BoardContract contract, IServerPlayer claimer) => Paid++;
        public void Release(ContractBoard board, BoardContract contract) => Released++;
        public bool Holds(ContractBoard board, BoardContract contract) => false;
    }

    private (ContractBoard board, FlagGoal goal, CountingSource source) NewBoard(BoardOptions? options = null)
    {
        var board = Registry.Create("test:" + Guid.NewGuid().ToString("N"), options ?? new BoardOptions { PostRules = { MaxActivePostsPerPlayer = 0 } });
        var goal = new FlagGoal();
        var source = new CountingSource();
        board.RegisterGoal(goal);
        board.RewardSources.Add(source);
        return (board, goal, source);
    }

    private IInventory Escrow(IServerPlayer owner, int gears)
    {
        var inv = new InventoryGeneric(4, "testescrow-" + Guid.NewGuid().ToString("N"), Sapi);
        inv[0].Itemstack = new ItemStack(Sapi.World.GetItem(new AssetLocation("game:gear-rusty"))!, gears);
        return inv;
    }

    private static int CountGears(IServerPlayer player)
    {
        int n = 0;
        foreach (var inv in player.InventoryManager.Inventories.Values)
        {
            if (inv.ClassName == "creative") continue;
            foreach (var slot in inv)
                if (slot?.Itemstack?.Collectible?.Code?.ToString() == "game:gear-rusty") n += slot.Itemstack.StackSize;
        }
        return n;
    }

    private static BoardContract Draft(int demand = 3) => new() { Kind = "test", TargetCode = "game:gear-rusty", Demand = demand, CustomTitle = "Gears" };

    [AtlasScenario]
    public async Task Post_Accept_TurnIn_Collect_Pays_The_Escrow_To_The_Claimer()
    {
        var (board, goal, source) = NewBoard();
        var author = (await World.JoinPlayer("boardauthor1")).Player;
        var claimer = (await World.JoinPlayer("boardclaimer1")).Player;
        var events = new List<string>();
        board.Posted += e => events.Add("posted:" + e.EscrowSummary);
        board.Accepted += _ => events.Add("accepted");
        board.Completed += _ => events.Add("completed");
        board.Collected += _ => events.Add("collected");

        var inv = Escrow(author, 5);
        Assert.Null(board.Post(author, Draft(), inv, expireInDays: 3));
        var c = Assert.Single(board.State.Contracts);
        Assert.True(inv[0].Empty);
        Assert.True(board.Escrow.Holds(c));
        Assert.Equal(BoardSources.Player, c.Source);
        Assert.Equal(author.PlayerUID, c.AuthorUid);
        Assert.Equal(c.CreatedTotalDays + 3, c.ExpireTotalDays, 3);
        Assert.Equal(5, Assert.Single(c.Rewards).ItemAmount);
        Assert.True(board.State.LastPostTotalDays.ContainsKey(author.PlayerUID));

        Assert.Equal(board.LangKey("error-own-contract"), board.Accept(author, c.Id));
        Assert.Null(board.Accept(claimer, c.Id));
        Assert.Equal(BoardStates.Claimed, c.State);
        Assert.Equal(board.LangKey("error-not-open"), board.Accept(author, c.Id));

        Assert.Equal(board.LangKey("error-not-completable"), board.TurnIn(claimer, c.Id));
        goal.Done = true;
        Assert.Null(board.TurnIn(claimer, c.Id));
        Assert.Equal(BoardStates.Fulfilled, c.State);
        Assert.Equal(0, c.ExpireTotalDays);
        Assert.Equal(0, source.Paid);   // player contracts pay when the claimer collects

        Assert.Equal(board.LangKey("error-not-collectible"), board.Collect(author, c.Id));
        int before = CountGears(claimer);
        Assert.Null(board.Collect(claimer, c.Id));
        await World.Ticks(2);
        Assert.Equal(before + 5, CountGears(claimer));
        Assert.Equal(1, source.Paid);
        Assert.Empty(board.State.Contracts);
        Assert.Equal(new[] { "accepted", "completed", "collected" }, events.Skip(1));
        Assert.StartsWith("posted:", events[0]);
        Assert.EndsWith("×5", events[0]);
        Assert.Equal(new[] { "accepted", "complete" }, goal.Log);
    }

    [AtlasScenario]
    public async Task Delivered_Items_Keep_The_Contract_Until_The_Author_Collects()
    {
        var (board, goal, _) = NewBoard();
        goal.Deliver = true;
        goal.Done = true;
        var author = (await World.JoinPlayer("boardauthor2")).Player;
        var claimer = (await World.JoinPlayer("boardclaimer2")).Player;

        Assert.Null(board.Post(author, Draft(), Escrow(author, 1)));
        var c = board.State.Contracts.Single();
        Assert.Null(board.Accept(claimer, c.Id));
        Assert.Null(board.TurnIn(claimer, c.Id));

        Assert.Null(board.Collect(claimer, c.Id));
        Assert.NotNull(board.Find(c.Id));          // author has not collected yet
        int before = CountGears(author);
        Assert.Null(board.Collect(author, c.Id));
        await World.Ticks(2);
        Assert.Equal(before + 2, CountGears(author));
        Assert.Null(board.Find(c.Id));
    }

    [AtlasScenario]
    public async Task Cancel_And_Abandon_And_Rules()
    {
        var (board, goal, source) = NewBoard(new BoardOptions
        {
            PostRules = { MaxActivePostsPerPlayer = 1, MaxDemand = 10, RewardSlotCount = 1 }
        });
        var author = (await World.JoinPlayer("boardauthor3")).Player;
        var claimer = (await World.JoinPlayer("boardclaimer3")).Player;

        Assert.Equal(board.LangKey("error-demand"), board.Post(author, Draft(demand: 11), Escrow(author, 1)));
        Assert.Equal(board.LangKey("error-escrow-empty"), board.Post(author, Draft(), new InventoryGeneric(2, "empty-" + Guid.NewGuid(), Sapi)));
        var twoStacks = Escrow(author, 1);
        twoStacks[1].Itemstack = twoStacks[0].Itemstack.Clone();
        Assert.Equal(board.LangKey("error-reward"), board.Post(author, Draft(), twoStacks));
        Assert.Equal(board.LangKey("error-kind-not-allowed"), board.Post(author, new BoardContract { Kind = "nope", Demand = 1 }, Escrow(author, 1)));

        Assert.Null(board.Post(author, Draft(), Escrow(author, 4)));
        Assert.Equal(board.LangKey("error-max-posts"), board.Post(author, Draft(), Escrow(author, 1)));
        var c = board.State.Contracts.Single();

        Assert.Null(board.Accept(claimer, c.Id));
        Assert.Equal(board.LangKey("error-not-open"), board.Cancel(author, c.Id));   // taken: the author cannot pull it
        Assert.Null(board.Abandon(claimer, c.Id));
        Assert.Equal(BoardStates.Open, c.State);
        Assert.Null(c.ClaimedByUid);
        Assert.Contains("released:online", goal.Log);

        Assert.Equal(board.LangKey("error-not-author"), board.Cancel(claimer, c.Id));
        int before = CountGears(author);
        Assert.Null(board.Cancel(author, c.Id));
        await World.Ticks(2);
        Assert.Equal(before + 4, CountGears(author));
        Assert.Equal(1, source.Released);
        Assert.Empty(board.State.Contracts);
        Assert.Empty(board.State.EscrowStacksByContract);
    }

    [AtlasScenario]
    public async Task Expiry_Returns_Escrow_Of_An_Offline_Author_On_Join()
    {
        bool authorOnline = false;
        var (board, goal, _) = NewBoard(new BoardOptions
        {
            PostRules = { MaxActivePostsPerPlayer = 0 },
            FindOnlinePlayer = uid => authorOnline ? Sapi.World.PlayerByUid(uid) as IServerPlayer : null
        });
        var author = (await World.JoinPlayer("boardauthor4")).Player;
        var claimer = (await World.JoinPlayer("boardclaimer4")).Player;

        Assert.Null(board.Post(author, Draft(), Escrow(author, 6), expireInDays: 1));
        var c = board.State.Contracts.Single();
        Assert.Null(board.Accept(claimer, c.Id));
        c.ExpireTotalDays = Sapi.World.Calendar.TotalDays - 0.01;

        Assert.Equal(1, board.SweepExpiry());
        Assert.Empty(board.State.Contracts);
        Assert.Contains("released:offline", goal.Log);   // FindOnlinePlayer says nobody is online
        var state = (BoardState)board.State;
        Assert.Single(state.PendingReturnsByUid[author.PlayerUID]);

        authorOnline = true;
        int before = CountGears(author);
        Assert.Equal(1, board.DeliverPendingReturns(author));
        await World.Ticks(2);
        Assert.Equal(before + 6, CountGears(author));
        Assert.False(state.PendingReturnsByUid.ContainsKey(author.PlayerUID));
    }

    [AtlasScenario]
    public async Task Server_Contracts_Pay_On_Completion_And_Forced_Goals_Complete()
    {
        var (board, goal, source) = NewBoard();
        var claimer = (await World.JoinPlayer("boardclaimer5")).Player;
        var contract = new BoardContract { Kind = "test", TargetCode = "game:gear-rusty", Demand = 1 };
        board.AddServerContract(contract);
        Assert.Equal(BoardSources.Server, contract.Source);
        Assert.False(string.IsNullOrEmpty(contract.Id));

        Assert.Null(board.Accept(claimer, contract.Id));
        Assert.Null(board.Complete(claimer, contract, force: true));   // goal not done, forced by an event
        Assert.Equal(BoardStates.Completed, contract.State);
        Assert.Equal(1, source.Paid);
        Assert.Empty(board.State.Contracts);
    }

    [AtlasScenario]
    public async Task Board_State_Survives_A_Save_And_Admin_Cancel_Returns_Everything()
    {
        var (board, _, source) = NewBoard();
        var author = (await World.JoinPlayer("boardauthor6")).Player;
        Assert.Null(board.Post(author, Draft(), Escrow(author, 2)));
        var c = board.State.Contracts.Single();

        Sapi.WorldManager.SaveGame.StoreData("arcanumlib:board:" + board.Key, (BoardState)board.State);
        var loaded = Sapi.WorldManager.SaveGame.GetData<BoardState>("arcanumlib:board:" + board.Key);
        Assert.Equal(c.Id, loaded.Contracts.Single().Id);
        Assert.Single(loaded.EscrowStacksByContract[c.Id]);

        TextCommandResult? res = null;
        Sapi.ChatCommands.ExecuteUnparsed($"/arcanum boards show {board.Key}", new TextCommandCallingArgs
        {
            Caller = new Caller { Type = EnumCallerType.Console, CallerRole = "admin", CallerPrivileges = new[] { "*" }, FromChatGroupId = GlobalConstants.ConsoleGroup },
        }, r => res = r);
        await World.Until(() => res != null, timeoutTicks: 100);
        Assert.Contains(c.Id, res!.StatusMessage);

        res = null;
        Sapi.ChatCommands.ExecuteUnparsed($"/arcanum boards cancel {board.Key} {c.Id}", new TextCommandCallingArgs
        {
            Caller = new Caller { Type = EnumCallerType.Console, CallerRole = "admin", CallerPrivileges = new[] { "*" }, FromChatGroupId = GlobalConstants.ConsoleGroup },
        }, r => res = r);
        await World.Until(() => res != null, timeoutTicks: 100);
        Assert.Equal(EnumCommandStatus.Success, res!.Status);
        Assert.Empty(board.State.Contracts);
        Assert.Equal(1, source.Released);
    }
}
