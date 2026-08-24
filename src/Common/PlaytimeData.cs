using System.Collections.Generic;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Persistent per-player playtime data used by <see cref="PlaytimeTracker" />.
    /// </summary>
    public class PlaytimeData
    {
        /// <summary>
        /// Total playtime, first join, streak and last-online metadata per player UID.
        /// </summary>
        public Dictionary<string, PlayerPlaytimeData> Players { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Persisted playtime metadata for a single player.
    /// </summary>
    public class PlayerPlaytimeData
    {
        /// <summary>Total accumulated playtime in milliseconds.</summary>
        public long TotalMs { get; set; }

        /// <summary>First join timestamp in UTC milliseconds, or 0 if unknown.</summary>
        public long FirstJoinMs { get; set; }

        /// <summary>Last online timestamp in UTC milliseconds, or 0 if unknown.</summary>
        public long LastOnlineMs { get; set; }

        /// <summary>Current consecutive-days login streak.</summary>
        public int LoginStreak { get; set; }

        /// <summary>Start of the last login day in UTC milliseconds.</summary>
        public long LastLoginDayMs { get; set; }
    }
}
