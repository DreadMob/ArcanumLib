using System.Collections.Generic;
using ProtoBuf;

namespace ArcanumLib.Boards
{
    /// <summary>What a client asks of a board in the ArcanumLib board window.</summary>
    public static class BoardActions
    {
        public const string Open = "open";
        public const string Accept = "accept";
        public const string TurnIn = "turnin";
        public const string Abandon = "abandon";
        public const string Cancel = "cancel";
        public const string Collect = "collect";
        public const string OpenPost = "openpost";
        public const string ClosePost = "closepost";
        public const string Confirm = "confirm";
        public const string Dispute = "dispute";
    }

    /// <summary>Client → server: an action on a board (contract id where the action needs one).</summary>
    [ProtoContract]
    public class BoardActionMessage
    {
        [ProtoMember(1)]
        public string BoardKey = "";
        [ProtoMember(2)]
        public string Action = "";
        [ProtoMember(3)]
        public string? ContractId;

        /// <summary>Free text of the action (a dispute's reason).</summary>
        [ProtoMember(4)]
        public string? Text;
    }

    /// <summary>Client → server: post a contract with the stacks put into the post window's slots.</summary>
    [ProtoContract]
    public class BoardPostMessage
    {
        [ProtoMember(1)]
        public string BoardKey = "";
        [ProtoMember(2)]
        public string Kind = "";
        [ProtoMember(3)]
        public string? TargetCode;
        [ProtoMember(4)]
        public int Demand;
        [ProtoMember(5)]
        public double ExpireDays;
        [ProtoMember(6)]
        public string? Title;
        [ProtoMember(7)]
        public string? Description;

        /// <summary>Amounts for the board's <see cref="IBoardPostableReward" />s.</summary>
        [ProtoMember(8)]
        public List<BoardRewardFieldValue> Rewards = new();
    }

    /// <summary>An amount field of a <see cref="IBoardPostableReward" /> in the post window.</summary>
    [ProtoContract]
    public class BoardRewardFieldInfo
    {
        [ProtoMember(1)]
        public string Source = "";

        /// <summary>Lang key of the label ("Оплата").</summary>
        [ProtoMember(2)]
        public string LabelKey = "";

        /// <summary>What the amount is in, one per choice ("крон со счёта").</summary>
        [ProtoMember(3)]
        public List<string> Choices = new();
        [ProtoMember(4)]
        public List<string> ChoiceNames = new();

        /// <summary>Most the author can give, per choice (their balance).</summary>
        [ProtoMember(5)]
        public List<long> Max = new();
    }

    /// <summary>The author's amount for one <see cref="IBoardPostableReward" />.</summary>
    [ProtoContract]
    public class BoardRewardFieldValue
    {
        [ProtoMember(1)]
        public string Source = "";
        [ProtoMember(2)]
        public string? Choice;
        [ProtoMember(3)]
        public long Amount;
    }

    /// <summary>A goal players can post, as the post window shows it.</summary>
    [ProtoContract]
    public class BoardGoalInfo
    {
        [ProtoMember(1)]
        public string Kind = "";
        [ProtoMember(2)]
        public string NameKey = "";

        /// <summary>Targets to pick from; empty = free text.</summary>
        [ProtoMember(3)]
        public List<string> Targets = new();

        /// <summary>The goal has a count (otherwise demand is 1).</summary>
        [ProtoMember(4)]
        public bool HasDemand = true;

        /// <summary>Days the author has to accept the work (0 = the goal checks itself).</summary>
        [ProtoMember(5)]
        public double ConfirmDays;

        /// <summary>The free-text target is an item code (the window offers the held item).</summary>
        [ProtoMember(6)]
        public bool TargetIsItem;
    }

    /// <summary>Server → client: the board's contracts.</summary>
    [ProtoContract]
    public class BoardPagePacket
    {
        [ProtoMember(1)]
        public string BoardKey = "";
        [ProtoMember(2)]
        public string TitleKey = "";
        [ProtoMember(3)]
        public List<BoardContract> Contracts = new();

        /// <summary>Players may post on this board.</summary>
        [ProtoMember(4)]
        public bool CanPost;

        /// <summary>Calendar total days on the server, for "days left".</summary>
        [ProtoMember(5)]
        public double NowTotalDays;

        /// <summary>Lang key prefix of the board's texts (goal names, errors).</summary>
        [ProtoMember(6)]
        public string LangPrefix = "";

        /// <summary>Names of the board's goals (no targets), for contract titles.</summary>
        [ProtoMember(7)]
        public List<BoardGoalInfo> Goals = new();
    }

    /// <summary>Server → client: open the post window over the server's escrow inventory.</summary>
    [ProtoContract]
    public class BoardOpenPostPacket
    {
        [ProtoMember(1)]
        public string BoardKey = "";
        [ProtoMember(2)]
        public string InventoryId = "";
        [ProtoMember(3)]
        public int SlotCount;
        [ProtoMember(4)]
        public List<BoardGoalInfo> Goals = new();
        [ProtoMember(5)]
        public int MaxDemand;
        [ProtoMember(6)]
        public double ExpireDaysMin;
        [ProtoMember(7)]
        public double ExpireDaysMax;
        [ProtoMember(8)]
        public double ExpireDaysDefault;
        [ProtoMember(9)]
        public double CooldownRemainingHours;
        [ProtoMember(10)]
        public int MaxTitleLength;
        [ProtoMember(11)]
        public int MaxDescriptionLength;

        /// <summary>Amount fields of the board's <see cref="IBoardPostableReward" />s for this author.</summary>
        [ProtoMember(12)]
        public List<BoardRewardFieldInfo> RewardFields = new();

        /// <summary>A post needs a reward (items or an amount).</summary>
        [ProtoMember(13)]
        public bool RequireReward;
    }

    /// <summary>Server → client: outcome of an action.</summary>
    [ProtoContract]
    public class BoardResultPacket
    {
        [ProtoMember(1)]
        public string BoardKey = "";
        [ProtoMember(2)]
        public bool Success;
        [ProtoMember(3)]
        public string? ErrorKey;
        [ProtoMember(4)]
        public string? InfoKey;
    }

    /// <summary>Server → client: a board changed; an open window asks for the page again.</summary>
    [ProtoContract]
    public class BoardChangedPacket
    {
        [ProtoMember(1)]
        public string BoardKey = "";
    }
}
