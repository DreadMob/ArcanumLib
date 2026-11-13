using System.Collections.Generic;
using System.IO;
using ArcanumLib.Boards;
using ProtoBuf;

namespace ArcanumLib.Tests.Unit;

/// <summary>
/// The board model keeps the proto layout of the Quest Framework bounty contract it was moved from, so saved boards
/// and the bounty network packets read unchanged; new members only add numbers after the old ones.
/// </summary>
public class BoardModelsTests
{
    /// <summary>The bounty contract as the Quest Framework declared it before the move (proto numbers 1–28).</summary>
    [ProtoContract]
    public class LegacyContract
    {
        [ProtoMember(1)] public string Id = null!;
        [ProtoMember(2)] public string BoardKey = "global";
        [ProtoMember(3)] public string Source = "server";
        [ProtoMember(4)] public string? AuthorUid;
        [ProtoMember(5)] public string? AuthorName;
        [ProtoMember(6)] public string Rarity = "common";
        [ProtoMember(7)] public string? SeasonTag;
        [ProtoMember(8)] public string? Kind;
        [ProtoMember(9)] public string? TargetCode;
        [ProtoMember(10)] public string? TargetTag;
        [ProtoMember(11)] public int Demand;
        [ProtoMember(12)] public int Progress;
        [ProtoMember(13)] public string? TargetPlayerUid;
        [ProtoMember(14)] public string? TargetPlayerName;
        [ProtoMember(15)] public string? CustomTitle;
        [ProtoMember(16)] public string? CustomDescription;
        [ProtoMember(17)] public List<LegacyReward> Rewards = new();
        [ProtoMember(18)] public double CreatedTotalDays;
        [ProtoMember(19)] public double ExpireTotalDays;
        [ProtoMember(20)] public double ClaimedTotalDays;
        [ProtoMember(21)] public string State = "open";
        [ProtoMember(22)] public string? ClaimedByUid;
        [ProtoMember(23)] public string? ClaimedByName;
        [ProtoMember(24)] public string? TargetDifficulty;
        [ProtoMember(25)] public string? TargetName;
        [ProtoMember(26)] public string? TargetDifficultyName;
        [ProtoMember(27)] public double RewardMultiplier = 1.0;
        [ProtoMember(28)] public List<byte[]>? PendingItemStackBytes;
    }

    [ProtoContract]
    public class LegacyReward
    {
        [ProtoMember(1)] public string? ItemCode;
        [ProtoMember(2)] public int ItemAmount;
        [ProtoMember(3)] public string? CurrencyKey;
        [ProtoMember(4)] public int CurrencyAmount;
        [ProtoMember(5)] public string? ReputationKey;
        [ProtoMember(6)] public int ReputationAmount;
    }

    private static TOut Convert<TIn, TOut>(TIn value)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, value);
        ms.Position = 0;
        return Serializer.Deserialize<TOut>(ms);
    }

    private static LegacyContract FullLegacy() => new()
    {
        Id = "c1", BoardKey = "global", Source = "player", AuthorUid = "uidA", AuthorName = "Alice", Rarity = "rare",
        SeasonTag = "winter", Kind = "gather_item", TargetCode = "game:flower-*", TargetTag = "flowers", Demand = 12,
        Progress = 3, TargetPlayerUid = "uidT", TargetPlayerName = "Tom", CustomTitle = "Flowers", CustomDescription = "Bring them",
        Rewards = { new LegacyReward { ItemCode = "game:gear-rusty", ItemAmount = 5 }, new LegacyReward { CurrencyKey = "silver", CurrencyAmount = 7, ReputationKey = "board", ReputationAmount = 2 } },
        CreatedTotalDays = 10.5, ExpireTotalDays = 17.25, ClaimedTotalDays = 11, State = "fulfilled", ClaimedByUid = "uidB",
        ClaimedByName = "Bob", TargetDifficulty = "sand", TargetName = "Flower", TargetDifficultyName = "Sand", RewardMultiplier = 1.75,
        PendingItemStackBytes = new List<byte[]> { new byte[] { 1, 2, 3 } }
    };

    [Fact]
    public void Legacy_Bounty_Contract_Reads_As_Board_Contract()
    {
        var c = Convert<LegacyContract, BoardContract>(FullLegacy());

        Assert.Equal("c1", c.Id);
        Assert.Equal("global", c.BoardKey);
        Assert.Equal(BoardSources.Player, c.Source);
        Assert.Equal("uidA", c.AuthorUid);
        Assert.Equal("Alice", c.AuthorName);
        Assert.Equal("rare", c.Rarity);
        Assert.Equal("winter", c.SeasonTag);
        Assert.Equal("gather_item", c.Kind);
        Assert.Equal("game:flower-*", c.TargetCode);
        Assert.Equal("flowers", c.TargetTag);
        Assert.Equal(12, c.Demand);
        Assert.Equal(3, c.Progress);
        Assert.Equal("uidT", c.TargetPlayerUid);
        Assert.Equal("Tom", c.TargetPlayerName);
        Assert.Equal("Flowers", c.CustomTitle);
        Assert.Equal("Bring them", c.CustomDescription);
        Assert.Equal(2, c.Rewards.Count);
        Assert.Equal("game:gear-rusty", c.Rewards[0].ItemCode);
        Assert.Equal(5, c.Rewards[0].ItemAmount);
        Assert.Equal("silver", c.Rewards[1].CurrencyKey);
        Assert.Equal(7, c.Rewards[1].CurrencyAmount);
        Assert.Equal("board", c.Rewards[1].ReputationKey);
        Assert.Equal(2, c.Rewards[1].ReputationAmount);
        Assert.Equal(10.5, c.CreatedTotalDays);
        Assert.Equal(17.25, c.ExpireTotalDays);
        Assert.Equal(11, c.ClaimedTotalDays);
        Assert.Equal(BoardStates.Fulfilled, c.State);
        Assert.Equal("uidB", c.ClaimedByUid);
        Assert.Equal("Bob", c.ClaimedByName);
        Assert.Equal("sand", c.TargetDifficulty);
        Assert.Equal("Flower", c.TargetName);
        Assert.Equal("Sand", c.TargetDifficultyName);
        Assert.Equal(1.75, c.RewardMultiplier);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(c.PendingItemStackBytes!));
        Assert.Null(c.Data);
    }

    [Fact]
    public void Board_Contract_Reads_Back_In_The_Legacy_Layout()
    {
        var board = Convert<LegacyContract, BoardContract>(FullLegacy());
        board.SetData("mod:key", "v");
        board.Rewards.Add(new BoardRewardEntry { Kind = "mod:account", Code = "crown", Amount = 50 });

        var legacy = Convert<BoardContract, LegacyContract>(board);

        Assert.Equal("c1", legacy.Id);
        Assert.Equal("fulfilled", legacy.State);
        Assert.Equal(1.75, legacy.RewardMultiplier);
        Assert.Equal(3, legacy.Rewards.Count);       // the new reward line is still a (blank) legacy entry
        Assert.Equal("game:gear-rusty", legacy.Rewards[0].ItemCode);
    }

    [Fact]
    public void Data_And_New_Reward_Kinds_Round_Trip()
    {
        var c = new BoardContract { Id = "x", Kind = "delivery" };
        c.SetData("alegacyeconomy:price", "120");
        c.Rewards.Add(new BoardRewardEntry { Kind = "alegacyeconomy:account", Code = "crown", Amount = 120 });

        var back = Convert<BoardContract, BoardContract>(c);

        Assert.Equal("120", back.GetData("alegacyeconomy:price"));
        var reward = Assert.Single(back.Rewards);
        Assert.Equal("alegacyeconomy:account", reward.Kind);
        Assert.Equal("crown", reward.Code);
        Assert.Equal(120, reward.Amount);

        back.SetData("alegacyeconomy:price", null);
        Assert.Null(back.GetData("alegacyeconomy:price"));
    }

    [Fact]
    public void Board_State_Round_Trips_And_Normalizes()
    {
        var state = new BoardState { BoardKey = "mod:orders" };
        state.Contracts.Add(new BoardContract { Id = "a", State = BoardStates.Claimed, ClaimedByUid = "u" });
        state.EscrowStacksByContract["a"] = new List<byte[]> { new byte[] { 9 } };
        state.LastPostTotalDays["u"] = 4;
        state.PendingReturnsByUid["v"] = new List<byte[]> { new byte[] { 8 }, new byte[] { 7 } };

        var back = Convert<BoardState, BoardState>(state).Normalize();

        Assert.Equal("mod:orders", back.BoardKey);
        Assert.Equal("a", Assert.Single(back.Contracts).Id);
        Assert.Equal(new byte[] { 9 }, Assert.Single(back.EscrowStacksByContract["a"]));
        Assert.Equal(4, back.LastPostTotalDays["u"]);
        Assert.Equal(2, back.PendingReturnsByUid["v"].Count);

        var empty = Convert<BoardState, BoardState>(new BoardState()).Normalize();
        Assert.NotNull(empty.Contracts);
        Assert.NotNull(empty.EscrowStacksByContract);
        Assert.NotNull(empty.PendingReturnsByUid);
    }

    [Theory]
    [InlineData(BoardStates.Open, true)]
    [InlineData(BoardStates.Claimed, true)]
    [InlineData(BoardStates.Fulfilled, false)]
    [InlineData(BoardStates.Completed, false)]
    [InlineData(BoardStates.Expired, false)]
    [InlineData(BoardStates.Cancelled, false)]
    [InlineData(null, false)]
    public void Only_Open_And_Claimed_Are_Active(string? state, bool active)
    {
        Assert.Equal(active, BoardStates.IsActive(state));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    [InlineData(0.5, 1)]
    [InlineData(7, 7)]
    [InlineData(99, 30)]
    public void Expire_Days_Are_Clamped_But_Never_Stays_Never(double chosen, double expected)
    {
        var rules = new BoardPostRules { ExpireDaysMin = 1, ExpireDaysMax = 30 };
        Assert.Equal(expected, rules.ClampExpireDays(chosen));
    }

    [Fact]
    public void Contract_Roles()
    {
        var c = new BoardContract { AuthorUid = "a", State = BoardStates.Claimed, ClaimedByUid = "b" };
        Assert.True(c.IsAuthor("a"));
        Assert.False(c.IsAuthor(null));
        Assert.True(c.IsClaimedBy("b"));
        c.State = BoardStates.Fulfilled;
        Assert.False(c.IsClaimedBy("b"));
    }
}
