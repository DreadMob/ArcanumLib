using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using Vintagestory.API.Server;

namespace ArcanumLib.Progression
{
    /// <summary>
    /// Interface for the full pity tracker service.
    /// </summary>
    public interface IPityTracker : IPityProvider
    {
        /// <summary>
        /// Legacy save keys to check and import when no new-store data exists.
        /// </summary>
        IReadOnlyList<string> LegacyFallbackKeys { get; }

        /// <summary>
        /// Registers an additional legacy savegame key to check during initialization.
        /// </summary>
        void AddLegacyFallbackKey(string key);

        /// <summary>
        /// Loads data and performs one-time migration from the registered legacy save keys if needed.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Saves the tracker data.
        /// </summary>
        void Save();

        /// <summary>
        /// Registers a pity definition. Existing definition with the same id is replaced.
        /// </summary>
        void RegisterDefinition(PityDefinition def);

        /// <summary>
        /// Convenience helper to register tiered pity definitions.
        /// </summary>
        void RegisterPityDefinitions(string prefix, int tier3Cap, int tier4Cap, string? tier3NameKey = null, string? tier4NameKey = null);

        /// <summary>
        /// Returns the number of opens remaining until the next guaranteed quality drop.
        /// </summary>
        int GetOpensUntilGuarantee(string playerUid, string definitionId, int qualityTierIndex = -1);

        /// <summary>
        /// Returns all registered definition IDs.
        /// </summary>
        IEnumerable<string> GetDefinitionIds();

        /// <summary>
        /// Removes all pity counters for a player.
        /// </summary>
        void ResetPlayerData(string playerUid);
    }

    /// <summary>
    /// Standalone pity tracker for any loot-quality system.
    /// Tracks per-player "opens since last quality drop" counters keyed by (definitionId, subKey).
    /// Persists via <see cref="ModDataStore" /> and can migrate legacy savegame data.
    /// </summary>
    public class PityTracker : IPityTracker
    {
        /// <summary>
        /// The current server-scoped tracker instance, if one has been registered in <see cref="ArcanumServices" />.
        /// Use <see cref="ArcanumServices.Register{T}(T, ArcanumServiceScope)" /> to publish a tracker explicitly.
        /// </summary>
        public static IPityTracker? Current
            => ArcanumServices.Get<IPityTracker>(ArcanumServiceScope.Server);

        private readonly ICoreServerAPI? _sapi;
        private readonly IModDataStore<Dictionary<string, PityPlayerData>> _store;
        private readonly Dictionary<string, PityDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _legacyFallbackKeys = new();
        private readonly object _syncLock = new();

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
        /// <param name="key">The key to look up.</param>
        public void AddLegacyFallbackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            lock (_syncLock)
            {
                if (!_legacyFallbackKeys.Contains(key))
                    _legacyFallbackKeys.Add(key);
            }
        }

        /// <summary>
        /// Loads data and performs one-time migration from the registered legacy save keys if needed.
        /// Safe to call multiple times; only re-imports when the new store is empty. Thread-safe.
        /// </summary>
        public void Initialize()
        {
            lock (_syncLock)
            {
                _store.Load();

                if (_store.Data.Count == 0)
                {
                    TryImportLegacyData();
                }
            }
        }

        /// <summary>
        /// Saves the tracker data.
        /// </summary>
        public void Save()
        {
            lock (_syncLock)
            {
                _store.Save();
            }
        }

        /// <summary>
        /// Registers a pity definition. Existing definition with the same id is replaced.
        /// </summary>
        /// <param name="def">The def value.</param>
        public void RegisterDefinition(PityDefinition def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.definitionId)) return;
            def.Validate();
            lock (_syncLock)
            {
                _definitions[def.definitionId] = def;
            }
        }

        /// <summary>
        /// Convenience helper to register tiered pity definitions.
        /// </summary>
        /// <param name="prefix">The prefix value.</param>
        /// <param name="tier3Cap">The tier 3 cap value.</param>
        /// <param name="tier4Cap">The tier 4 cap value.</param>
        /// <param name="tier3NameKey">The tier 3 name key value.</param>
        /// <param name="tier4NameKey">The tier 4 name key value.</param>
        public void RegisterPityDefinitions(string prefix, int tier3Cap, int tier4Cap, string? tier3NameKey = null, string? tier4NameKey = null)
        {
            if (tier3Cap <= 0 && tier4Cap <= 0) return;
            if (string.IsNullOrWhiteSpace(prefix)) return;

            for (int tier = 1; tier <= 4; tier++)
            {
                RegisterDefinition(new PityDefinition
                {
                    definitionId = $"{prefix}{tier}",
                    rules = new List<PityTierRule>
                    {
                        new PityTierRule { qualityTierIndex = 3, opensUntilGuarantee = tier3Cap, displayNameKey = tier3NameKey },
                        new PityTierRule { qualityTierIndex = 4, opensUntilGuarantee = tier4Cap, displayNameKey = tier4NameKey }
                    }
                });
            }
        }

        /// <summary>
        /// Try to get a registered pity definition.
        /// </summary>
        /// <param name="definitionId">The definition id value.</param>
        /// <param name="definition">When this method returns, contains the <paramref name="definition" /> value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public bool TryGetDefinition(string definitionId, out PityDefinition? definition)
        {
            lock (_syncLock)
            {
                return _definitions.TryGetValue(definitionId, out definition);
            }
        }

        /// <summary>
        /// Records an open. Resets counters for all quality tiers &lt;= rolledQuality, increments for higher tiers.
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="definitionId">The definition id value.</param>
        /// <param name="rolledQuality">The rolled quality value.</param>
        public void RecordOpen(string playerUid, string definitionId, int rolledQuality)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(definitionId)) return;

            lock (_syncLock)
            {
                if (!_definitions.TryGetValue(definitionId, out var def) || def == null) return;

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

                _store.MarkDirty();
            }
        }

        /// <summary>
        /// Returns the guaranteed quality tier index, or 0 if none.
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="definitionId">The definition id value.</param>
        /// <returns>The guaranteed quality.</returns>
        public int GetGuaranteedQuality(string playerUid, string definitionId)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(definitionId)) return 0;

            lock (_syncLock)
            {
                if (!_definitions.TryGetValue(definitionId, out var def) || def == null) return 0;

                var data = TryGetPlayerData(playerUid);
                if (data == null) return 0;

                var key = MakeKey(playerUid, definitionId);
                if (!data.counters.TryGetValue(key, out var counters)) return 0;

                return def.GetGuaranteedQuality(counters.opensSinceQuality);
            }
        }

        /// <summary>
        /// Get pity counters for a player/definition, or null if not tracked.
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="definitionId">The definition id value.</param>
        /// <returns>The counters, or null if none is found.</returns>
        public PityCounters? GetCounters(string playerUid, string definitionId)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(definitionId)) return null;

            lock (_syncLock)
            {
                var data = TryGetPlayerData(playerUid);
                if (data == null) return null;

                var key = MakeKey(playerUid, definitionId);
                return data.counters.TryGetValue(key, out var counters) ? counters : null;
            }
        }

        /// <summary>
        /// Returns the number of opens remaining until the next guaranteed quality drop
        /// for the given player and definition. Returns 0 if a guarantee is already due.
        /// Returns -1 if the definition or player is not found, or no rules are configured.
        /// </summary>
        /// <param name="playerUid">The player UID.</param>
        /// <param name="definitionId">The pity definition id.</param>
        /// <param name="qualityTierIndex">Optional: return opens until this specific tier. If omitted, returns the lowest remaining across all rules.</param>
        /// <returns>The opens until guarantee.</returns>
        public int GetOpensUntilGuarantee(string playerUid, string definitionId, int qualityTierIndex = -1)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(definitionId)) return -1;

            lock (_syncLock)
            {
                if (!_definitions.TryGetValue(definitionId, out var def) || def == null) return -1;

                var data = TryGetPlayerData(playerUid);
                var key = MakeKey(playerUid, definitionId);
                int currentOpens = 0;
                if (data != null && data.counters.TryGetValue(key, out var counters))
                {
                    currentOpens = counters.totalOpens;
                }

                int bestRemaining = int.MaxValue;
                bool anyRule = false;

                foreach (var rule in def.rules)
                {
                    if (rule.opensUntilGuarantee <= 0) continue;
                    if (qualityTierIndex >= 0 && rule.qualityTierIndex != qualityTierIndex) continue;

                    anyRule = true;
                    int opensSince = 0;
                    if (data != null && data.counters.TryGetValue(key, out var ruleCounters))
                    {
                        opensSince = ruleCounters.opensSinceQuality.GetValueOrDefault(rule.qualityTierIndex, 0);
                    }

                    int remaining = rule.opensUntilGuarantee - opensSince;
                    if (remaining < 0) remaining = 0;
                    if (remaining < bestRemaining) bestRemaining = remaining;
                }

                if (!anyRule) return -1;
                return bestRemaining == int.MaxValue ? 0 : bestRemaining;
            }
        }

        /// <summary>
        /// Returns all registered definition IDs.
        /// </summary>
        /// <returns>A collection of definition ids values.</returns>
        public IEnumerable<string> GetDefinitionIds()
        {
            lock (_syncLock)
            {
                return _definitions.Keys.ToList();
            }
        }

        /// <summary>
        /// Removes all pity counters for a player.
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        public void ResetPlayerData(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;
            lock (_syncLock)
            {
                _store.Data.Remove(playerUid);
                _store.MarkDirty();
            }
        }

        private PityPlayerData? TryGetPlayerData(string playerUid)
        {
            _store.Data.TryGetValue(playerUid, out var data);
            return data;
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

                    _store.MarkDirty();
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
