using System;
using System.Collections.Generic;

namespace ArcanumLib.Common
{
    /// <summary>
    /// General-purpose real-time cooldown, combat-state, and playtime-unlock tracker.
    /// Uses UTC timestamps so cooldowns survive server restarts.
    /// </summary>
    public class PlaytimeCooldownManager
    {
        private readonly IPlaytimeTracker? _playtimeTracker;

        // key format: "uid:category" (e.g. "player123:runes")
        private readonly Dictionary<string, long> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
        // Combat timestamps are stored in real-time seconds (UtcNow) instead of game elapsed ms,
        // because World.ElapsedMilliseconds resets on server restart and produces negative deltas.
        private readonly Dictionary<string, long> _lastCombatSeconds = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a cooldown manager backed by the global playtime tracker.
        /// </summary>
        public PlaytimeCooldownManager() : this(PlaytimeTracker.Current) { }

        /// <summary>
        /// Creates a cooldown manager backed by the given playtime tracker.
        /// </summary>
        /// <param name="playtimeTracker">The playtime tracker value.</param>
        public PlaytimeCooldownManager(IPlaytimeTracker? playtimeTracker)
        {
            _playtimeTracker = playtimeTracker;
        }

        // ========================================================================
        //  Real-time cooldowns
        // ========================================================================

        /// <summary>Records the current time as a cooldown start for the given key.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="category">The category value.</param>
        public void SetCooldown(string playerUid, string category)
        {
            _cooldowns[Key(playerUid, category)] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>Returns true if the player is still on cooldown for this category.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="category">The category value.</param>
        /// <param name="cooldownSeconds">The cooldown seconds value.</param>
        /// <returns>true if on cooldown; otherwise, false.</returns>
        public bool IsOnCooldown(string playerUid, string category, int cooldownSeconds)
        {
            if (cooldownSeconds <= 0) return false;
            if (_cooldowns.TryGetValue(Key(playerUid, category), out long last))
            {
                long elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last;
                return Math.Max(0, elapsed) < cooldownSeconds;
            }
            return false;
        }

        /// <summary>Seconds remaining on cooldown. 0 = ready.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="category">The category value.</param>
        /// <param name="cooldownSeconds">The cooldown seconds value.</param>
        /// <returns>The cooldown remaining.</returns>
        public int GetCooldownRemaining(string playerUid, string category, int cooldownSeconds)
        {
            if (cooldownSeconds <= 0) return 0;
            if (_cooldowns.TryGetValue(Key(playerUid, category), out long last))
            {
                long elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last;
                long remaining = cooldownSeconds - Math.Max(0, elapsed);
                return (int)Math.Max(0, remaining);
            }
            return 0;
        }

        /// <summary>Clears a cooldown immediately.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="category">The category value.</param>
        public void ClearCooldown(string playerUid, string category)
        {
            _cooldowns.Remove(Key(playerUid, category));
        }

        // ========================================================================
        //  Combat state
        // ========================================================================

        /// <summary>Marks the player as currently in combat (e.g. on damage dealt/received).</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        public void MarkCombat(string playerUid)
        {
            _lastCombatSeconds[playerUid] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>Returns true if the player was in combat within the combatCooldownSeconds window.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="combatCooldownSeconds">The combat cooldown seconds value.</param>
        /// <returns>true if in combat; otherwise, false.</returns>
        public bool IsInCombat(string playerUid, int combatCooldownSeconds)
        {
            if (combatCooldownSeconds <= 0) return false;
            if (_lastCombatSeconds.TryGetValue(playerUid, out long last))
            {
                long elapsedSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last;
                return Math.Max(0, elapsedSec) < combatCooldownSeconds;
            }
            return false;
        }

        /// <summary>Seconds remaining until out of combat. 0 = safe.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="combatCooldownSeconds">The combat cooldown seconds value.</param>
        /// <returns>The combat remaining.</returns>
        public int GetCombatRemaining(string playerUid, int combatCooldownSeconds)
        {
            if (combatCooldownSeconds <= 0) return 0;
            if (_lastCombatSeconds.TryGetValue(playerUid, out long last))
            {
                long elapsedSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last;
                long remaining = combatCooldownSeconds - Math.Max(0, elapsedSec);
                return (int)Math.Max(0, remaining);
            }
            return 0;
        }

        // ========================================================================
        //  Playtime unlocks
        // ========================================================================

        /// <summary>Returns true if the player has at least the required playtime hours.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="requiredHours">The required hours value.</param>
        /// <returns>true if the operation has required playtime; otherwise, false.</returns>
        public bool HasRequiredPlaytime(string playerUid, float requiredHours)
        {
            if (requiredHours <= 0f) return true;
            float hours = _playtimeTracker?.GetPlaytimeHours(playerUid) ?? 0f;
            return hours >= requiredHours;
        }

        /// <summary>Hours remaining until the requirement is met. 0 = unlocked.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="requiredHours">The required hours value.</param>
        /// <returns>The playtime remaining.</returns>
        public float GetPlaytimeRemaining(string playerUid, float requiredHours)
        {
            if (requiredHours <= 0f) return 0f;
            float hours = _playtimeTracker?.GetPlaytimeHours(playerUid) ?? 0f;
            return Math.Max(0f, requiredHours - hours);
        }

        /// <summary>Total playtime hours for the player.</summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <returns>The playtime hours.</returns>
        public float GetPlaytimeHours(string playerUid)
        {
            return _playtimeTracker?.GetPlaytimeHours(playerUid) ?? 0f;
        }

        // ========================================================================
        //  Combined check
        // ========================================================================

        /// <summary>
        /// Returns true if all conditions are met:
        /// - no real-time cooldown active
        /// - not in combat (if combatCooldown &gt; 0)
        /// - meets playtime requirement (if requiredHours &gt; 0)
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="cooldownCategory">The cooldown category value.</param>
        /// <param name="cooldownSeconds">The cooldown seconds value.</param>
        /// <param name="combatCooldownSeconds">The combat cooldown seconds value.</param>
        /// <param name="requiredPlaytimeHours">The required playtime hours value.</param>
        /// <returns>true if the operation can proceed; otherwise, false.</returns>
        public bool CanProceed(string playerUid, string cooldownCategory, int cooldownSeconds,
            int combatCooldownSeconds, float requiredPlaytimeHours)
        {
            if (IsOnCooldown(playerUid, cooldownCategory, cooldownSeconds)) return false;
            if (IsInCombat(playerUid, combatCooldownSeconds)) return false;
            if (!HasRequiredPlaytime(playerUid, requiredPlaytimeHours)) return false;
            return true;
        }

        private static string Key(string playerUid, string category) => $"{playerUid}:{category}";
    }
}
