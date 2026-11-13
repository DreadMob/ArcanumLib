using System;
using System.Collections.Generic;
using ProtoBuf;

namespace ArcanumLib.Boards
{
    /// <summary>Who put a contract on the board.</summary>
    public static class BoardSources
    {
        /// <summary>Made by the server (generated, admin); its reward is paid on completion.</summary>
        public const string Server = "server";

        /// <summary>Posted by a player; the reward sits in escrow until the claimer collects it.</summary>
        public const string Player = "player";
    }

    /// <summary>Contract states. <see cref="Completed" />, <see cref="Expired" /> and <see cref="Cancelled" /> leave the board.</summary>
    public static class BoardStates
    {
        public const string Open = "open";
        public const string Claimed = "claimed";

        /// <summary>The goal is done, the reward (and anything delivered for the author) waits to be collected.</summary>
        public const string Fulfilled = "fulfilled";

        public const string Completed = "completed";
        public const string Expired = "expired";
        public const string Cancelled = "cancelled";

        /// <summary>Open or claimed: still counts as an active post of its author.</summary>
        public static bool IsActive(string? state) => state == Open || state == Claimed;
    }

    /// <summary>
    /// A contract on a board, posted by a player or made by the server. What has to be done is the goal
    /// (<see cref="Kind" /> names an <see cref="IBoardGoal" />, the target fields are its parameters); the reward is the
    /// escrowed stacks plus any <see cref="IBoardRewardSource" /> of the board.
    /// </summary>
    /// <remarks>
    /// The proto numbers 1–28 are those of the Alegacy Quest Framework bounty contract, so boards saved by it read
    /// unchanged. New members go after the last one; goals keep their own extra data in <see cref="Data" />.
    /// </remarks>
    [ProtoContract]
    public class BoardContract
    {
        [ProtoMember(1)]
        public string Id = null!;
        [ProtoMember(2)]
        public string BoardKey = "global";

        /// <summary><see cref="BoardSources.Server" /> or <see cref="BoardSources.Player" />.</summary>
        [ProtoMember(3)]
        public string Source = BoardSources.Server;

        [ProtoMember(4)]
        public string? AuthorUid;
        [ProtoMember(5)]
        public string? AuthorName;

        /// <summary>Tier of a server contract (the board's own ids, e.g. rarities).</summary>
        [ProtoMember(6)]
        public string Rarity = "common";

        /// <summary>Optional tag of the generator that made the contract (e.g. a season).</summary>
        [ProtoMember(7)]
        public string? SeasonTag;

        /// <summary>Goal kind: the id an <see cref="IBoardGoal" /> is registered under.</summary>
        [ProtoMember(8)]
        public string? Kind;

        /// <summary>Main target of the goal: entity, item or block code, or another id the goal understands.</summary>
        [ProtoMember(9)]
        public string? TargetCode;

        /// <summary>Optional tag alternative to <see cref="TargetCode" />.</summary>
        [ProtoMember(10)]
        public string? TargetTag;

        /// <summary>How many times / how much of the target.</summary>
        [ProtoMember(11)]
        public int Demand;

        /// <summary>Progress a goal may keep on the contract itself.</summary>
        [ProtoMember(12)]
        public int Progress;

        /// <summary>Player the goal is about (for goals aimed at a player).</summary>
        [ProtoMember(13)]
        public string? TargetPlayerUid;
        [ProtoMember(14)]
        public string? TargetPlayerName;

        /// <summary>Player-written title and description.</summary>
        [ProtoMember(15)]
        public string? CustomTitle;
        [ProtoMember(16)]
        public string? CustomDescription;

        /// <summary>What the reward is, for display (escrowed items are summed here on post).</summary>
        [ProtoMember(17)]
        public List<BoardRewardEntry> Rewards = new();

        /// <summary>Calendar total days when the contract was made / runs out (0 = never) / was claimed.</summary>
        [ProtoMember(18)]
        public double CreatedTotalDays;
        [ProtoMember(19)]
        public double ExpireTotalDays;
        [ProtoMember(20)]
        public double ClaimedTotalDays;

        [ProtoMember(21)]
        public string State = BoardStates.Open;
        [ProtoMember(22)]
        public string? ClaimedByUid;
        [ProtoMember(23)]
        public string? ClaimedByName;

        /// <summary>Variant of the target (difficulty, tier) when the goal has one.</summary>
        [ProtoMember(24)]
        public string? TargetDifficulty;

        /// <summary>Display name of the target, resolved when it cannot be derived from the code.</summary>
        [ProtoMember(25)]
        public string? TargetName;

        /// <summary>Display name of <see cref="TargetDifficulty" />.</summary>
        [ProtoMember(26)]
        public string? TargetDifficultyName;

        /// <summary>Reward multiplier of the target (generators: hard targets of one tier pay more).</summary>
        [ProtoMember(27)]
        public double RewardMultiplier = 1.0;

        /// <summary>
        /// Stacks the goal delivered for the author (e.g. gathered items), waiting until the author collects them.
        /// </summary>
        [ProtoMember(28)]
        public List<byte[]>? PendingItemStackBytes;

        /// <summary>Free-form parameters of goals and reward sources from other mods.</summary>
        [ProtoMember(29)]
        public Dictionary<string, string>? Data;

        /// <summary>Reads a <see cref="Data" /> value.</summary>
        public string? GetData(string key) => Data != null && Data.TryGetValue(key, out var v) ? v : null;

        /// <summary>Sets (or with null removes) a <see cref="Data" /> value.</summary>
        public void SetData(string key, string? value)
        {
            if (value == null) { Data?.Remove(key); return; }
            (Data ??= new Dictionary<string, string>(StringComparer.Ordinal))[key] = value;
        }

        /// <summary>The author is this player.</summary>
        public bool IsAuthor(string? playerUid) => playerUid != null && AuthorUid == playerUid;

        /// <summary>Claimed by this player.</summary>
        public bool IsClaimedBy(string? playerUid) => playerUid != null && State == BoardStates.Claimed && ClaimedByUid == playerUid;
    }

    /// <summary>
    /// One line of a contract's reward. Items and the Quest Framework's currency/reputation keep their old fields;
    /// other reward sources use <see cref="Kind" />, <see cref="Code" /> and <see cref="Amount" />.
    /// </summary>
    [ProtoContract]
    public class BoardRewardEntry
    {
        [ProtoMember(1)]
        public string? ItemCode;
        [ProtoMember(2)]
        public int ItemAmount;

        [ProtoMember(3)]
        public string? CurrencyKey;
        [ProtoMember(4)]
        public int CurrencyAmount;

        [ProtoMember(5)]
        public string? ReputationKey;
        [ProtoMember(6)]
        public int ReputationAmount;

        /// <summary>Reward kind of an <see cref="IBoardRewardSource" /> (e.g. "alegacyeconomy:account").</summary>
        [ProtoMember(7)]
        public string? Kind;
        [ProtoMember(8)]
        public string? Code;
        [ProtoMember(9)]
        public long Amount;
    }

    /// <summary>
    /// What a <see cref="ContractBoard" /> keeps. A mod may keep it inside its own saved object (the Quest Framework
    /// does); otherwise <see cref="BoardState" /> is stored by <see cref="BoardRegistry" />.
    /// </summary>
    public interface IBoardState
    {
        List<BoardContract> Contracts { get; }

        /// <summary>contractId → escrowed reward stacks (<c>ItemStack.ToBytes()</c>).</summary>
        Dictionary<string, List<byte[]>> EscrowStacksByContract { get; }

        /// <summary>playerUid → calendar total days of their last post (post cooldown).</summary>
        Dictionary<string, double> LastPostTotalDays { get; }
    }

    /// <summary>The ready-made board state for boards owned by <see cref="BoardRegistry" />.</summary>
    [ProtoContract]
    public class BoardState : IBoardState
    {
        [ProtoMember(1)]
        public string BoardKey = "";
        [ProtoMember(2)]
        public List<BoardContract> Contracts { get; set; } = new();
        [ProtoMember(3)]
        public Dictionary<string, List<byte[]>> EscrowStacksByContract { get; set; } = new();
        [ProtoMember(4)]
        public Dictionary<string, double> LastPostTotalDays { get; set; } = new();

        /// <summary>playerUid → refunds and deliveries that found the player offline; handed out on join.</summary>
        [ProtoMember(5)]
        public Dictionary<string, List<byte[]>> PendingReturnsByUid { get; set; } = new();

        /// <summary>Serialization version for the owning mod's migrations.</summary>
        [ProtoMember(6)]
        public int Version = 1;

        /// <summary>Fills collections a save from an older version left null.</summary>
        public BoardState Normalize()
        {
            Contracts ??= new();
            EscrowStacksByContract ??= new();
            LastPostTotalDays ??= new();
            PendingReturnsByUid ??= new();
            foreach (var c in Contracts) c.Rewards ??= new();
            return this;
        }
    }
}
