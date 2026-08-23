using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ArcanumLib.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Tracks total online time per player via PlayerJoin / PlayerLeave events.
    /// Also tracks first join date, last online date, and login streaks.
    /// Persists data to a JSON file in the server's ModData directory.
    /// </summary>
    public class PlaytimeTracker
    {
        private readonly Dictionary<string, PlayerPlaytimeData> _playerData = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _playerSessionStartMs = new(StringComparer.OrdinalIgnoreCase);
        private readonly ICoreServerAPI _sapi;
        private readonly EventScope _events;
        private readonly string _dataFileName;

        /// <summary>Fired when a session is saved: (playerUid, totalMs).</summary>
        public event Action<string, long> OnSessionSaved;

        /// <summary>
        /// Creates a new tracker. The <paramref name="dataFileName"/> is the JSON file name
        /// (without path) used for persistence inside the mod's ModData directory.
        /// </summary>
        public PlaytimeTracker(ICoreServerAPI sapi, string dataFileName = "playtime_tracker.json")
        {
            _sapi = sapi;
            _dataFileName = dataFileName ?? "playtime_tracker.json";
            LoadData();
            _events = sapi.CreateEventScope()
                .Add(() => sapi.Event.PlayerJoin += OnPlayerJoin, () => sapi.Event.PlayerJoin -= OnPlayerJoin)
                .Add(() => sapi.Event.PlayerLeave += OnPlayerLeave, () => sapi.Event.PlayerLeave -= OnPlayerLeave)
                .Add(() => sapi.Event.GameWorldSave += OnServerSave, () => sapi.Event.GameWorldSave -= OnServerSave);
        }

        public void Dispose()
        {
            _events.Dispose();
            SaveData();
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            string uid = byPlayer.PlayerUID;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _playerSessionStartMs[uid] = now;

            if (!_playerData.TryGetValue(uid, out var data))
            {
                data = new PlayerPlaytimeData { FirstJoinMs = now };
                _playerData[uid] = data;
            }
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
        }

        private void OnPlayerLeave(IServerPlayer byPlayer)
        {
            string uid = byPlayer.PlayerUID;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_playerSessionStartMs.TryGetValue(uid, out long startMs))
            {
                long sessionMs = now - startMs;
                if (!_playerData.TryGetValue(uid, out var data))
                {
                    data = new PlayerPlaytimeData();
                    _playerData[uid] = data;
                }
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
                long totalMs = (_playerData.GetValueOrDefault(kv.Key)?.TotalMs ?? 0) + sessionMs;
                OnSessionSaved?.Invoke(kv.Key, totalMs);
            }
        }

        /// <summary>Total playtime in hours for the given player.</summary>
        public float GetPlaytimeHours(string playerUid)
        {
            return GetPlaytimeMs(playerUid) / 3600000f;
        }

        /// <summary>Total playtime in milliseconds for the given player.</summary>
        public long GetPlaytimeMs(string playerUid)
        {
            long totalMs = _playerData.GetValueOrDefault(playerUid)?.TotalMs ?? 0;
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
        public Dictionary<string, float> GetAllPlaytimeHours()
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var kv in _playerData)
            {
                long totalMs = kv.Value.TotalMs;
                if (_playerSessionStartMs.TryGetValue(kv.Key, out long startMs))
                    totalMs += now - startMs;
                result[kv.Key] = totalMs / 3600000f;
            }
            return result;
        }

        /// <summary>First join timestamp in UTC milliseconds, or null if unknown.</summary>
        public long? GetFirstJoinMs(string playerUid)
        {
            return _playerData.GetValueOrDefault(playerUid)?.FirstJoinMs;
        }

        /// <summary>Last online timestamp in UTC milliseconds. Returns now if currently online.</summary>
        public long? GetLastOnlineMs(string playerUid)
        {
            if (_playerSessionStartMs.ContainsKey(playerUid))
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return _playerData.GetValueOrDefault(playerUid)?.LastOnlineMs;
        }

        /// <summary>Current login streak (consecutive days).</summary>
        public int GetLoginStreak(string playerUid)
        {
            return _playerData.GetValueOrDefault(playerUid)?.LoginStreak ?? 0;
        }

        /// <summary>Sets the first join timestamp for a player.</summary>
        public void SetFirstJoinMs(string playerUid, long ms)
        {
            if (!_playerData.TryGetValue(playerUid, out var data))
            {
                data = new PlayerPlaytimeData();
                _playerData[playerUid] = data;
            }
            data.FirstJoinMs = ms;
            SaveData();
        }

        /// <summary>
        /// Sets the total accumulated playtime for a player (useful for importing historical data).
        /// </summary>
        public void SetTotalMs(string playerUid, long totalMs)
        {
            if (!_playerData.TryGetValue(playerUid, out var data))
            {
                data = new PlayerPlaytimeData();
                _playerData[playerUid] = data;
            }
            data.TotalMs = Math.Max(0, totalMs);
            SaveData();
        }

        /// <summary>
        /// Bulk import playtime from a map of playerUid -> totalMs.
        /// Returns number of entries imported.
        /// </summary>
        public int ImportFromDictionary(Dictionary<string, long> playtimes)
        {
            int count = 0;
            foreach (var kv in playtimes)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                if (!_playerData.TryGetValue(kv.Key, out var data))
                {
                    data = new PlayerPlaytimeData();
                    _playerData[kv.Key] = data;
                }
                data.TotalMs = Math.Max(0, kv.Value);
                count++;
            }
            SaveData();
            return count;
        }

        private static long ToDayStartMs(long ms)
        {
            return ms - (ms % 86400000L);
        }

        private string DataFilePath
        {
            get
            {
                if (_sapi == null) return string.Empty;
                return Path.Combine(_sapi.GetOrCreateDataPath("ModData"), _dataFileName);
            }
        }

        private void LoadData()
        {
            try
            {
                string path = DataFilePath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                string json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, PlayerPlaytimeData>>(json);
                if (data != null)
                {
                    _playerData.Clear();
                    foreach (var kv in data)
                        _playerData[kv.Key] = kv.Value;
                    return;
                }

                // Backward compatibility: try old flat format (Dictionary<string, long> = totalMs)
                var oldData = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
                if (oldData != null)
                {
                    _playerData.Clear();
                    foreach (var kv in oldData)
                    {
                        _playerData[kv.Key] = new PlayerPlaytimeData { TotalMs = kv.Value };
                    }
                    _sapi?.Logger?.Notification("[PlaytimeTracker] Migrated old playtime data format.");
                }
            }
            catch (Exception ex)
            {
                _sapi?.Logger?.Warning("[PlaytimeTracker] Failed to load data: {0}", ex.Message);
            }
        }

        private void SaveData()
        {
            try
            {
                string path = DataFilePath;
                if (string.IsNullOrEmpty(path)) return;

                string dir = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Fold active sessions into TotalMs and reset their start to now.
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var activeUids = new List<string>(_playerSessionStartMs.Keys);
                foreach (var uid in activeUids)
                {
                    if (_playerData.TryGetValue(uid, out var data))
                        data.TotalMs += now - _playerSessionStartMs[uid];
                    _playerSessionStartMs[uid] = now;
                }

                string json = JsonSerializer.Serialize(_playerData);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _sapi?.Logger?.Warning("[PlaytimeTracker] Failed to save data: {0}", ex.Message);
            }
        }

        private class PlayerPlaytimeData
        {
            public long TotalMs { get; set; }
            public long FirstJoinMs { get; set; }
            public long LastOnlineMs { get; set; }
            public int LoginStreak { get; set; }
            public long LastLoginDayMs { get; set; }
        }
    }
}
