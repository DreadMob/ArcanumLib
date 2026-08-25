using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using Vintagestory.API.Server;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Tracks total online time per player via PlayerJoin / PlayerLeave events.
    /// Also tracks first join date, last online date, and login streaks.
    /// Persists data through <see cref="ModDataStore" /> so it survives savegame copy/delete.
    /// </summary>
    public class PlaytimeTracker : IDisposable
    {
        private readonly ICoreServerAPI? _sapi;
        private readonly IModDataStore<PlaytimeData>? _store;
        private readonly Dictionary<string, long> _playerSessionStartMs = new(StringComparer.OrdinalIgnoreCase);
        private readonly EventScope? _events;
        private readonly string _legacyDataFileName;

        /// <summary>
        /// The current server-scoped tracker instance, if one has been registered in <see cref="ArcanumServices" />.
        /// Use <see cref="ArcanumServices.Register{T}(T, ArcanumServiceScope)" /> to publish a tracker explicitly.
        /// </summary>
        public static PlaytimeTracker? Current
            => ArcanumServices.Get<PlaytimeTracker>(ArcanumServiceScope.Server);

        /// <summary>Fired when a session is saved: (playerUid, totalMs).</summary>
        public event Action<string, long>? OnSessionSaved;

        /// <summary>
        /// Creates a new tracker backed by <see cref="ModDataStore" />.
        /// The optional <paramref name="legacyDataFileName" /> is used once to migrate data from the
        /// old flat-JSON persistence format. New data is always written through the data store.
        /// </summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="legacyDataFileName">The legacy data file name value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sapi" /> is <see langword="null" />.</exception>
        public PlaytimeTracker(ICoreServerAPI sapi, string legacyDataFileName = "playtime_tracker.json")
        {
            _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
            _legacyDataFileName = legacyDataFileName ?? "playtime_tracker.json";

            _store = ModDataStore.GetOrCreate<PlaytimeData>(sapi, "arcanumlib", "playtime", 1);
            _ = _store.Data; // force load
            MigrateFromLegacyFile();

            _events = sapi.CreateEventScope()
                .Add(() => sapi.Event.PlayerJoin += OnPlayerJoin, () => sapi.Event.PlayerJoin -= OnPlayerJoin)
                .Add(() => sapi.Event.PlayerLeave += OnPlayerLeave, () => sapi.Event.PlayerLeave -= OnPlayerLeave)
                .Add(() => sapi.Event.GameWorldSave += OnServerSave, () => sapi.Event.GameWorldSave -= OnServerSave);
        }

        /// <summary>
        /// Releases event subscriptions and saves pending data.
        /// </summary>
        public void Dispose()
        {
            _events?.Dispose();
            SaveData();
        }

        private Dictionary<string, PlayerPlaytimeData> PlayerData => _store?.Data.Players ?? new();

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            string uid = byPlayer.PlayerUID;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _playerSessionStartMs[uid] = now;

            var data = GetOrCreateData(uid);
            data.LastOnlineMs = now;

            // Update login streak
            long todayMs = ToDayStartMs(now);
            long lastLoginDayMs = data.LastLoginDayMs;
            if (lastLoginDayMs == 0)
            {
                data.LoginStreak = 1;
            }
            else
            {
                long dayDiff = (todayMs - lastLoginDayMs) / 86400000L;
                if (dayDiff == 1)
                    data.LoginStreak++;
                else if (dayDiff > 1)
                    data.LoginStreak = 1;
                // dayDiff == 0 → same day, keep streak
            }
            data.LastLoginDayMs = todayMs;

            if (data.FirstJoinMs == 0)
                data.FirstJoinMs = now;

            _store?.MarkDirty();
        }

        private void OnPlayerLeave(IServerPlayer byPlayer)
        {
            string uid = byPlayer.PlayerUID;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_playerSessionStartMs.TryGetValue(uid, out long startMs))
            {
                long sessionMs = now - startMs;
                var data = GetOrCreateData(uid);
                data.TotalMs += sessionMs;
                data.LastOnlineMs = now;
                _playerSessionStartMs.Remove(uid);
                SaveData();
                OnSessionSaved?.Invoke(uid, data.TotalMs);
            }
        }

        private void OnServerSave()
        {
            SaveData();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var kv in _playerSessionStartMs)
            {
                long sessionMs = now - kv.Value;
                long totalMs = (PlayerData.GetValueOrDefault(kv.Key)?.TotalMs ?? 0) + sessionMs;
                OnSessionSaved?.Invoke(kv.Key, totalMs);
            }
        }

        /// <summary>Total playtime in hours for the given player.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <returns>The playtime hours.</returns>
        public float GetPlaytimeHours(string playerUid)
        {
            return GetPlaytimeMs(playerUid) / 3600000f;
        }

        /// <summary>Total playtime in milliseconds for the given player.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <returns>The playtime ms.</returns>
        public long GetPlaytimeMs(string playerUid)
        {
            long totalMs = PlayerData.GetValueOrDefault(playerUid)?.TotalMs ?? 0;
            if (_playerSessionStartMs.TryGetValue(playerUid, out long startMs))
            {
                totalMs += DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMs;
            }
            return totalMs;
        }

        /// <summary>
        /// Returns all tracked player UIDs with their total playtime in hours.
        /// Includes offline players (e.g. imported historical data).
        /// </summary>
        /// <returns>A dictionary of all playtime hours.</returns>
        public Dictionary<string, float> GetAllPlaytimeHours()
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var kv in PlayerData)
            {
                long totalMs = kv.Value.TotalMs;
                if (_playerSessionStartMs.TryGetValue(kv.Key, out long startMs))
                    totalMs += now - startMs;
                result[kv.Key] = totalMs / 3600000f;
            }
            return result;
        }

        /// <summary>First join timestamp in UTC milliseconds, or null if unknown.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <returns>The first join ms, or null if none is found.</returns>
        public long? GetFirstJoinMs(string playerUid)
        {
            var data = PlayerData.GetValueOrDefault(playerUid);
            if (data == null || data.FirstJoinMs == 0) return null;
            return data.FirstJoinMs;
        }

        /// <summary>Last online timestamp in UTC milliseconds. Returns now if currently online.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <returns>The last online ms, or null if none is found.</returns>
        public long? GetLastOnlineMs(string playerUid)
        {
            if (_playerSessionStartMs.ContainsKey(playerUid))
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var data = PlayerData.GetValueOrDefault(playerUid);
            if (data == null || data.LastOnlineMs == 0) return null;
            return data.LastOnlineMs;
        }

        /// <summary>Current login streak (consecutive days).</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <returns>The login streak.</returns>
        public int GetLoginStreak(string playerUid)
        {
            return PlayerData.GetValueOrDefault(playerUid)?.LoginStreak ?? 0;
        }

        /// <summary>Sets the first join timestamp for a player.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="ms">The interval in milliseconds.</param>
        public void SetFirstJoinMs(string playerUid, long ms)
        {
            var data = GetOrCreateData(playerUid);
            data.FirstJoinMs = ms;
            SaveData();
        }

        /// <summary>
        /// Sets the total accumulated playtime for a player (useful for importing historical data).
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="totalMs">The total ms value.</param>
        public void SetTotalMs(string playerUid, long totalMs)
        {
            var data = GetOrCreateData(playerUid);
            data.TotalMs = Math.Max(0, totalMs);
            SaveData();
        }

        /// <summary>
        /// Bulk import playtime from a map of playerUid -&gt; totalMs.
        /// Returns number of entries imported.
        /// </summary>
        /// <param name="playtimes">The playtimes value.</param>
        /// <returns>The import from dictionary.</returns>
        public int ImportFromDictionary(Dictionary<string, long> playtimes)
        {
            int count = 0;
            foreach (var kv in playtimes)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                var data = GetOrCreateData(kv.Key);
                data.TotalMs = Math.Max(0, kv.Value);
                count++;
            }
            SaveData();
            return count;
        }

        private PlayerPlaytimeData GetOrCreateData(string playerUid)
        {
            if (PlayerData.TryGetValue(playerUid, out var data)) return data;
            data = new PlayerPlaytimeData();
            PlayerData[playerUid] = data;
            return data;
        }

        private static long ToDayStartMs(long ms)
        {
            return ms - (ms % 86400000L);
        }

        private void MigrateFromLegacyFile()
        {
            if (_sapi == null || _store == null) return;
            if (PlayerData.Count > 0) return;

            try
            {
                string path = Path.Combine(_sapi.GetOrCreateDataPath("ModData"), _legacyDataFileName);
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, PlayerPlaytimeData>>(json);
                if (data != null)
                {
                    foreach (var kv in data)
                        PlayerData[kv.Key] = kv.Value;
                    _store.MarkDirty();
                    _store.Save();
                    _sapi.Logger?.Notification("[PlaytimeTracker] Migrated legacy playtime data into ModDataStore.");
                    return;
                }

                // Backward compatibility: try old flat format (Dictionary<string, long> = totalMs)
                var oldData = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
                if (oldData != null)
                {
                    foreach (var kv in oldData)
                        PlayerData[kv.Key] = new PlayerPlaytimeData { TotalMs = kv.Value };
                    _store.MarkDirty();
                    _store.Save();
                    _sapi.Logger?.Notification("[PlaytimeTracker] Migrated old flat playtime data into ModDataStore.");
                }
            }
            catch (Exception ex)
            {
                _sapi?.Logger?.Warning("[PlaytimeTracker] Failed to migrate legacy data: {0}", ex.Message);
            }
        }

        private void SaveData()
        {
            if (_store == null) return;

            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var activeUids = new List<string>(_playerSessionStartMs.Keys);
                foreach (var uid in activeUids)
                {
                    if (PlayerData.TryGetValue(uid, out var data))
                        data.TotalMs += now - _playerSessionStartMs[uid];
                    _playerSessionStartMs[uid] = now;
                }

                _store.MarkDirty();
                _store.Save();
            }
            catch (Exception ex)
            {
                _sapi?.Logger?.Warning("[PlaytimeTracker] Failed to save data: {0}", ex.Message);
            }
        }
    }
}
