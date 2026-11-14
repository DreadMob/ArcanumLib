using System;
using System.Collections.Generic;
using ArcanumLib.Common;
using ArcanumLib.Core;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// Finds the player a contract is aimed at by name. The default looks at online players, then at everyone who ever
    /// joined the world; a server mod may require more (e.g. a profile on the website).
    /// </summary>
    public interface IBoardPlayerLookup
    {
        /// <summary>The player's uid, or null when the name is not a valid target.</summary>
        string? ResolveUidByName(string playerName);
    }

    /// <summary>Online players first, then the world's player data by last known name.</summary>
    public sealed class KnownPlayerLookup : IBoardPlayerLookup
    {
        private readonly ICoreServerAPI sapi;

        public KnownPlayerLookup(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public string? ResolveUidByName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return null;
            var online = ArcanumServices.Get<OnlinePlayerCache>()?.All;
            if (online != null)
            {
                foreach (var p in online)
                {
                    if (string.Equals(p.PlayerName, playerName, StringComparison.OrdinalIgnoreCase)) return p.PlayerUID;
                }
            }
            return sapi.PlayerData.GetPlayerDataByLastKnownName(playerName)?.PlayerUID;
        }
    }

    /// <summary>Posting limits of a goal aimed at a player (bounty on a player's head and the like).</summary>
    public sealed class BoardPlayerTargetRules
    {
        /// <summary>Open or claimed contracts of the kind allowed on one target; 0 = no limit.</summary>
        public int MaxActivePerTarget { get; set; } = 1;

        /// <summary>Hours between two posts of the kind by one author.</summary>
        public double PostCooldownHours { get; set; }

        /// <summary>Hours between two posts of the kind on one target, by anyone.</summary>
        public double TargetCooldownHours { get; set; }

        /// <summary>Hours between two posts of the kind by one author on one target.</summary>
        public double PerAuthorTargetCooldownHours { get; set; }

        /// <summary>How the target's name is resolved; null = <see cref="KnownPlayerLookup" />.</summary>
        public IBoardPlayerLookup? Lookup { get; set; }
    }

    /// <summary>
    /// Shared rules of goals aimed at a player: the post names the target (<see cref="BoardContract.TargetPlayerName" />),
    /// the check resolves its uid, refuses the author themself, limits contracts per target and keeps three cooldowns
    /// in <see cref="IBoardState.Cooldowns" />. A goal calls <see cref="Check" /> from its
    /// <see cref="IBoardGoal.ValidatePost" />, <see cref="Record" /> from <see cref="IBoardGoal.OnPosted" /> and
    /// <see cref="ValidateAccept" /> from <see cref="IBoardGoal.ValidateAccept" />.
    /// </summary>
    /// <remarks>Error keys (board prefix +): error-pvp-unknown-target, -pvp-self, -pvp-target-limit, error-cooldown, -pvp-target-cooldown, -pvp-author-target-cooldown, error-target-self.</remarks>
    public static class BoardPlayerTargets
    {
        public static string AuthorKey(string kind, string authorUid) => kind + "|author|" + authorUid;

        public static string TargetKey(string kind, string targetUid) => kind + "|target|" + targetUid;

        public static string PairKey(string kind, string authorUid, string targetUid) => kind + "|pair|" + authorUid + "|" + targetUid;

        /// <summary>Resolves the target (sets <see cref="BoardContract.TargetPlayerUid" />) and checks the limits. Returns an error lang key or null.</summary>
        public static string? Check(ContractBoard board, IServerPlayer author, BoardContract draft, BoardPlayerTargetRules rules)
        {
            string kind = draft.Kind ?? "";
            var lookup = rules.Lookup ?? new KnownPlayerLookup(board.Sapi);
            string? targetUid = string.IsNullOrWhiteSpace(draft.TargetPlayerName) ? null : lookup.ResolveUidByName(draft.TargetPlayerName!);
            if (string.IsNullOrWhiteSpace(targetUid)) return board.LangKey("error-pvp-unknown-target");
            if (targetUid == author.PlayerUID) return board.LangKey("error-pvp-self");

            if (rules.MaxActivePerTarget > 0)
            {
                int active = board.State.Contracts.FindAll(c =>
                    string.Equals(c.Kind, kind, StringComparison.OrdinalIgnoreCase)
                    && c.TargetPlayerUid == targetUid
                    && BoardStates.IsActive(c.State)).Count;
                if (active >= rules.MaxActivePerTarget) return board.LangKey("error-pvp-target-limit");
            }

            double now = board.Sapi.World.Calendar.TotalDays;
            var cooldowns = board.State.Cooldowns;
            if (Running(cooldowns, AuthorKey(kind, author.PlayerUID), rules.PostCooldownHours, now)) return board.LangKey("error-cooldown");
            if (Running(cooldowns, TargetKey(kind, targetUid!), rules.TargetCooldownHours, now)) return board.LangKey("error-pvp-target-cooldown");
            if (Running(cooldowns, PairKey(kind, author.PlayerUID, targetUid!), rules.PerAuthorTargetCooldownHours, now))
                return board.LangKey("error-pvp-author-target-cooldown");

            draft.TargetPlayerUid = targetUid;
            return null;
        }

        /// <summary>Starts the three cooldowns of a posted contract.</summary>
        public static void Record(ContractBoard board, string authorUid, BoardContract contract)
        {
            if (contract.TargetPlayerUid == null) return;
            string kind = contract.Kind ?? "";
            double now = contract.CreatedTotalDays;
            var cooldowns = board.State.Cooldowns;
            cooldowns[AuthorKey(kind, authorUid)] = now;
            cooldowns[TargetKey(kind, contract.TargetPlayerUid)] = now;
            cooldowns[PairKey(kind, authorUid, contract.TargetPlayerUid)] = now;
        }

        /// <summary>The target may not take a contract on themself.</summary>
        public static string? ValidateAccept(ContractBoard board, IServerPlayer player, BoardContract contract) =>
            contract.TargetPlayerUid == player.PlayerUID ? board.LangKey("error-target-self") : null;

        /// <summary>Claimed contracts of the kind on <paramref name="targetUid" /> held by <paramref name="claimerUid" /> (complete them on a kill).</summary>
        public static List<BoardContract> ClaimedOn(ContractBoard board, string kind, string targetUid, string claimerUid) =>
            board.State.Contracts.FindAll(c =>
                string.Equals(c.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && c.State == BoardStates.Claimed
                && c.TargetPlayerUid == targetUid
                && c.ClaimedByUid == claimerUid);

        private static bool Running(Dictionary<string, double> cooldowns, string key, double hours, double now) =>
            hours > 0 && cooldowns.TryGetValue(key, out double since) && now - since < hours / 24.0;
    }
}
