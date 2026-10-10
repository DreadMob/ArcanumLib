using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace ArcanumLib.PlayerTransfer
{
    /// <summary>
    /// One account-to-account progress transfer (/avq datatransfer). Participants move their own
    /// per-player data from <see cref="FromUid" /> to <see cref="ToUid" />, merging into whatever the
    /// target already has (counters add up, flags OR, lists union, times/levels take the max) and
    /// leaving the source empty.
    /// </summary>
    public sealed class PlayerTransferContext
    {
        public ICoreServerAPI Sapi { get; }
        public string FromUid { get; }
        public string ToUid { get; }
        public string FromName { get; }
        public string ToName { get; }

        /// <summary>Human-readable lines for the admin report ("reputation: 4 factions").</summary>
        public List<string> Report { get; } = new();

        /// <summary>Participants that threw; the transfer continues with the rest.</summary>
        public List<string> Failures { get; } = new();

        public PlayerTransferContext(ICoreServerAPI sapi, string fromUid, string toUid, string fromName, string toName)
        {
            Sapi = sapi;
            FromUid = fromUid;
            ToUid = toUid;
            FromName = fromName;
            ToName = toName;
        }

        public void Note(string participantId, string what) => Report.Add($"{participantId}: {what}");
    }

    /// <summary>A mod's handler for moving its per-player data during an account transfer.</summary>
    public interface IPlayerDataTransfer
    {
        /// <summary>Stable id, also used for logging ("quests", "reputation").</summary>
        string Id { get; }

        /// <summary>
        /// Moves the data immediately. Runs on the server thread when the command executes, so both
        /// accounts may be offline; data that only exists on a live player entity belongs in
        /// <see cref="PlayerDataTransfer.EntityAttributePrefixes" /> instead (applied when each account joins).
        /// </summary>
        void Transfer(PlayerTransferContext ctx);
    }

    /// <summary>
    /// Registry of transfer participants. Mods register once at server start; the order of
    /// registration is the order of execution.
    /// </summary>
    public static class PlayerDataTransfer
    {
        private static readonly object Gate = new();
        private static readonly List<IPlayerDataTransfer> Participants = new();
        private static readonly List<Action<IServerPlayer>> AfterEntityMergeHooks = new();
        private static readonly Dictionary<string, bool> Prefixes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Func<string, string, string>> CustomDataMergers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<Func<Vintagestory.API.Datastructures.ITreeAttribute, IEnumerable<string>>> KeySelectors = new();
        private static readonly HashSet<string> WorldModdataKeys = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> CustomDataPrefixes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ExcludedPrefixes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Registers (or replaces, by id) a transfer participant.</summary>
        public static void Register(IPlayerDataTransfer participant)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.Id)) return;
            lock (Gate)
            {
                Participants.RemoveAll(p => string.Equals(p.Id, participant.Id, StringComparison.OrdinalIgnoreCase));
                Participants.Add(participant);
            }
        }

        /// <summary>Registers a participant from a delegate.</summary>
        public static void Register(string id, Action<PlayerTransferContext> transfer)
            => Register(new DelegateTransfer(id, transfer));

        /// <summary>
        /// Player WatchedAttributes whose key starts with one of these prefixes are progress and move
        /// with the account (e.g. "vsquest:", "alegacyvsquest:"). They live on the entity, so they are
        /// lifted off the source when it next joins and merged into the target when it joins.
        /// </summary>
        /// <param name="prefix">Key prefix, e.g. "vsquest:ach".</param>
        /// <param name="fillOnly">Copy only when the target has no such key (pending rewards, per-quest slots), never merge.</param>
        public static void RegisterEntityAttributePrefix(string prefix, bool fillOnly = false)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return;
            lock (Gate) Prefixes[prefix] = fillOnly;
        }

        /// <summary>
        /// Adds keys computed from the source's attributes (e.g. the keys a quest recorded it wrote),
        /// for progress whose key names are data-driven rather than under a fixed prefix.
        /// </summary>
        public static void RegisterEntityAttributeSelector(Func<Vintagestory.API.Datastructures.ITreeAttribute, IEnumerable<string>> selector)
        {
            if (selector == null) return;
            lock (Gate) KeySelectors.Add(selector);
        }

        internal static IReadOnlyList<Func<Vintagestory.API.Datastructures.ITreeAttribute, IEnumerable<string>>> Selectors { get { lock (Gate) return KeySelectors.ToList(); } }

        /// <summary>Keys under a registered prefix that are transient state, not progress (combat tags, bindings).</summary>
        public static void ExcludeEntityAttributePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return;
            lock (Gate) ExcludedPrefixes.Add(prefix);
        }

        /// <summary>
        /// IServerPlayer.WorldData mod-data keys (only reachable while the player is online) that move
        /// with the account. Byte blobs cannot be merged: the target keeps its own value if it has one.
        /// </summary>
        public static void RegisterWorldModdataKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            lock (Gate) WorldModdataKeys.Add(key);
        }

        internal static IReadOnlyCollection<string> WorldModdata { get { lock (Gate) return WorldModdataKeys.ToList(); } }

        /// <summary>
        /// ServerData.CustomPlayerData keys (server-side, readable while offline) starting with this
        /// prefix move with the account immediately (e.g. "alegacyvsquest:").
        /// </summary>
        public static void RegisterCustomDataPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return;
            lock (Gate) CustomDataPrefixes.Add(prefix);
        }

        internal static bool IsTransferredCustomDataKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (Gate) return CustomDataPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Custom merge for one ServerData.CustomPlayerData key whose string value is structured
        /// (e.g. "a=1;b=2"): (target, source) -> merged. The key must also fall under a registered custom-data prefix.
        /// </summary>
        public static void RegisterCustomDataMerger(string key, Func<string, string, string> merge)
        {
            if (string.IsNullOrWhiteSpace(key) || merge == null) return;
            lock (Gate) CustomDataMergers[key] = merge;
        }

        internal static Func<string, string, string>? GetCustomDataMerger(string key)
        {
            lock (Gate) return CustomDataMergers.TryGetValue(key, out var m) ? m : null;
        }

        /// <summary>Called on the target after entity attributes were merged in (e.g. to recount derived totals).</summary>
        public static void OnAfterEntityMerge(Action<IServerPlayer> hook)
        {
            if (hook == null) return;
            lock (Gate) AfterEntityMergeHooks.Add(hook);
        }

        public static IReadOnlyList<IPlayerDataTransfer> All { get { lock (Gate) return Participants.ToList(); } }
        public static IReadOnlyCollection<string> EntityAttributePrefixes { get { lock (Gate) return Prefixes.Keys.ToList(); } }
        internal static IReadOnlyCollection<string> ExcludedEntityAttributePrefixes { get { lock (Gate) return ExcludedPrefixes.ToList(); } }
        internal static IReadOnlyList<Action<IServerPlayer>> AfterMergeHooks { get { lock (Gate) return AfterEntityMergeHooks.ToList(); } }

        internal static bool IsTransferredKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (Gate)
            {
                if (!Prefixes.Keys.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return false;
                return !ExcludedPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            }
        }

        internal static bool IsFillOnlyKey(string key)
        {
            lock (Gate)
                return Prefixes.Any(p => p.Value && key.StartsWith(p.Key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Test hook.</summary>
        internal static void ResetForTests()
        {
            lock (Gate)
            {
                Participants.Clear();
                AfterEntityMergeHooks.Clear();
                CustomDataMergers.Clear();
                CustomDataPrefixes.Clear();
                WorldModdataKeys.Clear();
                KeySelectors.Clear();
                Prefixes.Clear();
                ExcludedPrefixes.Clear();
            }
        }

        private sealed class DelegateTransfer : IPlayerDataTransfer
        {
            private readonly Action<PlayerTransferContext> transfer;
            public string Id { get; }
            public DelegateTransfer(string id, Action<PlayerTransferContext> transfer) { Id = id; this.transfer = transfer; }
            public void Transfer(PlayerTransferContext ctx) => transfer?.Invoke(ctx);
        }
    }
}
