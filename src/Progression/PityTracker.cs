using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Persistence;
using Vintagestory.API.Server;

namespace ArcanumLib.Progression
{
    /// <summary>
    /// Standalone pity tracker for any loot-quality system.
    /// Tracks per-player "opens since last quality drop" counters keyed by (definitionId, subKey).
    /// Persists via <see cref="ModDataStore"/> and can migrate legacy savegame data.
    /// </summary>
    public class PityTracker : IPityProvider
    {
        /// <summary>
        /// The current global tracker instance. Consumers may set this in their own ModSystem.
        /// </summary>
        public static PityTracker? Current { get; set; }

        private readonly ICoreServerAPI? _sapi;
        private readonly IModDataStore<Dictionary<string, PityPlayerData>> _store;
        private readonly Dictionary<string, PityDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _legacyFallbackKeys = new();

        /// <summary>
        /// Legacy save keys to check and import when no new-store data exists.
        /// Consumers (e.g. migrating mods) can add their old savegame keys here.
        /// </summary>
        public IReadOnlyList<string> LegacyFallbackKeys => _legacyFallbackKeys;

        /// <summary>
        /// Creates a new tracker, loads/migrates data, and attempts to import from any registered legacy keys.
        /// </summary>
        /// <param name="sapi">The server API. May be null in unit tests, in which case persistence is disabled.</param>
        /// <param name="legacyFallbackKeys">Optional legacy savegame keys to import from.</param>
        public PityTracker(ICoreServerAPI? sapi, params string[] legacyFallbackKeys)
        {
            _sapi = sapi;
            if (legacyFallbackKeys != null)
            {
                foreach (var key in legacyFallbackKeys)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                        _legacyFallbackKeys.Add(key);
                }
            }

            var factory = new Func<Dictionary<string, PityPlayerData>>(() =>
                new Dictionary<string, PityPlayerData>(StringComparer.OrdinalIgnoreCase));

            _store = sapi != null
                ? ModDataStore.GetOrCreate<Dictionary<string, PityPlayerData>>(
                    sapi, "arcanumlib", "pity", 1, factory)
                : new ModDataStoreInstance<Dictionary<string, PityPlayerData>>(
                    null, "arcanumlib", "pity", 1, factory);

            Initialize();
        }

        /// <summary>
        /// Registers an additional legacy savegame key to check during initialization.
        /// </summary>
        public void AddLegacyFallbackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!_legacyFallbackKeys.Contains(key))
                _legacyFallbackKeys.Add(key);
        }

        /// <summary>
        /// Loads data and performs one-time migration from the registered legacy save keys if needed.
        /// Safe to call multiple times; only re-imports when the new store is empty.
        /// </summary>
        public void Initialize()
        {
            _store.Load();

            if (_store.Data.Count == 0)
            {
                TryImportLegacyData();
            }
        }

        /// <summary>
        /// Saves the tracker data.
        /// </summary>
        public void Save()
        {
            _store.Save();
        }

        /// <summary>
        /// Registers a pity definition. Existing definition with the same id is replaced.
        /// </summary>
        public void RegisterDefinition(PityDefinition def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.definitionId)) return;
            def.Validate();
            _definitions[def.definitionId] = def;
        }

        /// <summary>
        /// Convenience helper to register tiered pity definitions.
        /// </summary>
        public void RegisterPityDefinitions(string prefix, int radiantCap, int abyssalCap, string? radiantNameKey = null, string? abyssalNameKey = null)
        {
            if (radiantCap <= 0 && abyssalCap <= 0) return;
            if (string.IsNullOrWhiteSpace(prefix)) return;

            for (int tier = 1; tier <= 4; tier++)
            {
                RegisterDefinition(new PityDefinition
                {
                    definitionId = $"{prefix}{tier}",
                    rules = new List<PityTierRule>
                    {
                        new PityTierRule { qualityTierIndex = 3, opensUntilGuarantee = radiantCap, displayNameKey = radiantNameKey },
                        new PityTierRule { qualityTierIndex = 4, opensUntilGuarantee = abyssalCap, displayNameKey = abyssalNameKey }
                    }
                });
            }
        }

        /// <summary>
        /// Try to get a registered pity definition.
        /// </summary>
        public bool TryGetDefinition(string definitionId, out PityDefinition? definition)
        {
            return _definitions.TryGetValue(definitionId, out definition);
        }

        /// <summary>
        /// Records an open. Resets counters for all quality tiers &lt;= rolledQuality, increments for higher tiers.
        /// </summary>
        public void RecordOpen(string playerUid, string definitionId, int rolledQuality)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(definitionId)) return;
            if (!TryGetDefinition(definitionId, out var def) || def == null) return;

            var data = GetOrCreatePlayerData(playerUid);
            var key = MakeKey(playerUid, definitionId);
            if (!data.counters.TryGetValue(key, out var counters))
            {
                counters = new PityCounters();
                data.counters[key] = counters;
            }

            counters.totalOpens++;

            foreach (var rule in def.rules)
            {
                if (rule.qualityTierIndex <= rolledQuality)
                    counters.opensSinceQuality[rule.qualityTierIndex] = 0;
                else
                    counters.opensSinceQuality[rule.qualityTierIndex] = counters.opensSinceQuality.GetValueOrDefault(rule.qualityTierIndex, 0) + 1;
            }
        }

        /// <summary>
        /// Returns the guaranteed quality tier index, or 0 if none.
        /// </summary>
        public int GetGuaranteedQuality(string playerUid, string definitionId)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(definitionId)) return 0;
            if (!TryGetDefinition(definitionId, out var def) || def == null) return 0;

            var data = GetOrCreatePlayerData(playerUid);
            var key = MakeKey(playerUid, definitionId);
            if (!data.counters.TryGetValue(key, out var counters)) return 0;

            return def.GetGuaranteedQuality(counters.opensSinceQuality);
        }

        /// <summary>
        /// Get pity counters for a player/definition, or null if not tracked.
        /// </summary>
        public PityCounters? GetCounters(string playerUid, string definitionId)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(definitionId)) return null;

            var data = GetOrCreatePlayerData(playerUid);
            var key = MakeKey(playerUid, definitionId);
            return data.counters.TryGetValue(key, out var counters) ? counters : null;
        }

        /// <summary>
        /// Returns all registered definition IDs.
        /// </summary>
        public IEnumerable<string> GetDefinitionIds() => _definitions.Keys;

        /// <summary>
        /// Removes all pity counters for a player.
        /// </summary>
        public void ResetPlayerData(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;
            _store.Data.Remove(playerUid);
        }

        private PityPlayerData GetOrCreatePlayerData(string playerUid)
        {
            if (!_store.Data.TryGetValue(playerUid, out var data))
            {
                data = new PityPlayerData();
                _store.Data[playerUid] = data;
            }
            return data;
        }

        private void TryImportLegacyData()
        {
            if (_sapi?.WorldManager?.SaveGame == null || _legacyFallbackKeys.Count == 0) return;

            foreach (var legacyKey in _legacyFallbackKeys)
            {
                try
                {
                    var legacy = _sapi.WorldManager.SaveGame.GetData<Dictionary<string, PityPlayerData>>(legacyKey);
                    if (legacy == null || legacy.Count == 0) continue;

                    foreach (var kvp in legacy)
                    {
                        _store.Data[kvp.Key] = kvp.Value;
                    }

                    _store.Save();

                    _sapi.Logger.Notification("[ArcanumLib] [PityTracker] Migrated {0} legacy pity records from {1}.", legacy.Count, legacyKey);
                    return;
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning("[ArcanumLib] [PityTracker] Failed to import legacy pity data from {0}: {1}", legacyKey, ex.Message);
                }
            }
        }

        private static string MakeKey(string playerUid, string definitionId) => $"{playerUid}::{definitionId}";
    }
}
