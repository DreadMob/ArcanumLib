using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Data
{
    /// <summary>
    /// Resolves a per-entity cooldown multiplier (e.g. from a difficulty or tier attribute).
    /// </summary>
    /// <param name="entity">Entity whose multiplier is being resolved.</param>
    public delegate double CooldownMultiplier(Entity entity);

    /// <summary>
    /// Tracks per-entity cooldowns in <see cref="Entity.WatchedAttributes"/>.
    /// Cooldowns are persisted across chunk unloads and survive server restarts
    /// because they are stored as milliseconds in the entity's attribute tree.
    /// </summary>
    public static class CooldownTracker
    {
        /// <summary>
        /// Returns true if the cooldown has never started or if the given duration has passed.
        /// </summary>
        /// <param name="entity">Entity that owns the cooldown.</param>
        /// <param name="key">Unique attribute key, e.g. "mymod:ability:lastStartMs".</param>
        /// <param name="durationSeconds">Cooldown length in seconds.</param>
        /// <param name="multiplier">Optional multiplier applied to the duration. Defaults to 1.0.</param>
        public static bool IsReady(this Entity entity, string key, double durationSeconds, double multiplier = 1.0)
            => IsReady(entity, key, durationSeconds, null, multiplier);

        /// <summary>
        /// Returns true if the cooldown has never started or if the given duration has passed.
        /// The multiplier is resolved by <paramref name="multiplierFactory"/> when a value is needed.
        /// </summary>
        public static bool IsReady(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory)
            => IsReady(entity, key, durationSeconds, multiplierFactory, 1.0);

        private static bool IsReady(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory, double fallbackMultiplier)
        {
            if (entity?.Api?.World is null) return false;

            long lastStartMs = entity.WatchedAttributes?.GetLong(key, 0) ?? 0;
            if (lastStartMs == 0) return true;

            double multiplier = multiplierFactory?.Invoke(entity) ?? fallbackMultiplier;
            long now = entity.Api.World.ElapsedMilliseconds;
            long cooldownMs = (long)(durationSeconds * 1000.0 * multiplier);

            // Server restart: ElapsedMilliseconds resets to 0, but WatchedAttributes persist.
            // If lastStartMs is in the future, the stored cooldown is stale.
            if (lastStartMs > now && now >= 0)
            {
                entity.WatchedAttributes?.SetLong(key, 0);
                entity.WatchedAttributes?.MarkPathDirty(key);
                return true;
            }

            return now - lastStartMs >= cooldownMs;
        }

        /// <summary>
        /// Stores the current time as the cooldown start for the given key.
        /// </summary>
        public static void MarkCooldownStart(this Entity entity, string key)
        {
            if (entity?.Api?.World is null || entity.WatchedAttributes is null) return;

            long now = entity.Api.World.ElapsedMilliseconds;
            entity.WatchedAttributes.SetLong(key, now);
            entity.WatchedAttributes.MarkPathDirty(key);
        }

        /// <summary>
        /// Returns the remaining cooldown time in milliseconds, or 0 if ready.
        /// </summary>
        public static long GetRemainingCooldownMs(this Entity entity, string key, double durationSeconds, double multiplier = 1.0)
            => GetRemainingCooldownMs(entity, key, durationSeconds, null, multiplier);

        /// <summary>
        /// Returns the remaining cooldown time in milliseconds using a multiplier factory.
        /// </summary>
        public static long GetRemainingCooldownMs(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory)
            => GetRemainingCooldownMs(entity, key, durationSeconds, multiplierFactory, 1.0);

        private static long GetRemainingCooldownMs(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory, double fallbackMultiplier)
        {
            if (entity?.Api?.World is null) return 0;

            long lastStartMs = entity.WatchedAttributes?.GetLong(key, 0) ?? 0;
            if (lastStartMs == 0) return 0;

            double multiplier = multiplierFactory?.Invoke(entity) ?? fallbackMultiplier;
            long now = entity.Api.World.ElapsedMilliseconds;
            long cooldownMs = (long)(durationSeconds * 1000.0 * multiplier);
            long elapsed = now - lastStartMs;

            return Math.Max(0, cooldownMs - elapsed);
        }

        /// <summary>
        /// Returns the cooldown progress as a fraction from 0.0 (just started) to 1.0 (ready).
        /// </summary>
        public static float GetCooldownProgress(this Entity entity, string key, double durationSeconds, double multiplier = 1.0)
            => GetCooldownProgress(entity, key, durationSeconds, null, multiplier);

        /// <summary>
        /// Returns the cooldown progress using a multiplier factory.
        /// </summary>
        public static float GetCooldownProgress(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory)
            => GetCooldownProgress(entity, key, durationSeconds, multiplierFactory, 1.0);

        private static float GetCooldownProgress(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory, double fallbackMultiplier)
        {
            if (durationSeconds <= 0) return 1f;

            double multiplier = multiplierFactory?.Invoke(entity) ?? fallbackMultiplier;
            long totalMs = (long)(durationSeconds * 1000.0 * multiplier);
            if (totalMs <= 0) return 1f;

            long remainingMs = GetRemainingCooldownMs(entity, key, durationSeconds, multiplierFactory, fallbackMultiplier);
            return 1f - (float)remainingMs / totalMs;
        }

        /// <summary>
        /// Resets the cooldown so that the next <see cref="IsReady"/> call returns true.
        /// </summary>
        public static void ResetCooldown(this Entity entity, string key)
        {
            if (entity?.WatchedAttributes is null) return;

            entity.WatchedAttributes.SetLong(key, 0);
            entity.WatchedAttributes.MarkPathDirty(key);
        }
    }
}
