using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Data
{
    /// <summary>
    /// Resolves a per-entity cooldown multiplier (e.g. from a difficulty or tier attribute).
    /// </summary>
    /// <param name="entity">Entity whose multiplier is being resolved.</param>
    /// <returns>The cooldown multiplier.</returns>
    public delegate double CooldownMultiplier(Entity entity);

    /// <summary>
    /// Tracks per-entity cooldowns in <see cref="Entity.WatchedAttributes" />.
    /// Cooldowns are persisted across chunk unloads and survive server restarts
    /// because they are stored as milliseconds in the entity's attribute tree.
    /// </summary>
    public static class CooldownTracker
    {
        private static readonly object _syncLock = new();
        /// <summary>
        /// Returns true if the cooldown has never started or if the given duration has passed.
        /// </summary>
        /// <param name="entity">Entity that owns the cooldown.</param>
        /// <param name="key">Unique attribute key, e.g. "mymod:ability:lastStartMs".</param>
        /// <param name="durationSeconds">Cooldown length in seconds.</param>
        /// <param name="multiplier">Optional multiplier applied to the duration. Defaults to 1.0.</param>
        /// <returns>true if ready; otherwise, false.</returns>
        public static bool IsReady(this Entity entity, string key, double durationSeconds, double multiplier = 1.0)
            => IsReady(entity, key, durationSeconds, null, multiplier);

        /// <summary>
        /// Returns true if the cooldown has never started or if the given duration has passed.
        /// The multiplier is resolved by <paramref name="multiplierFactory" /> when a value is needed.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="durationSeconds">The duration seconds value.</param>
        /// <param name="multiplierFactory">The multiplier factory value.</param>
        /// <returns>true if ready; otherwise, false.</returns>
        public static bool IsReady(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory)
            => IsReady(entity, key, durationSeconds, multiplierFactory, 1.0);

        private static bool IsReady(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory, double fallbackMultiplier)
        {
            if (entity?.Api?.World is null) return false;

            lock (_syncLock)
            {
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
        }

        /// <summary>
        /// Stores the current time as the cooldown start for the given key.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        public static void MarkCooldownStart(this Entity entity, string key)
        {
            if (entity?.Api?.World is null || entity.WatchedAttributes is null) return;

            lock (_syncLock)
            {
                long now = entity.Api.World.ElapsedMilliseconds;
                entity.WatchedAttributes.SetLong(key, now);
                entity.WatchedAttributes.MarkPathDirty(key);
            }
        }

        /// <summary>
        /// Returns the remaining cooldown time in milliseconds, or 0 if ready.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="durationSeconds">The duration seconds value.</param>
        /// <param name="multiplier">The multiplier value.</param>
        /// <returns>The remaining cooldown ms.</returns>
        public static long GetRemainingCooldownMs(this Entity entity, string key, double durationSeconds, double multiplier = 1.0)
            => GetRemainingCooldownMs(entity, key, durationSeconds, null, multiplier);

        /// <summary>
        /// Returns the remaining cooldown time in milliseconds using a multiplier factory.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="durationSeconds">The duration seconds value.</param>
        /// <param name="multiplierFactory">The multiplier factory value.</param>
        /// <returns>The remaining cooldown ms.</returns>
        public static long GetRemainingCooldownMs(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory)
            => GetRemainingCooldownMs(entity, key, durationSeconds, multiplierFactory, 1.0);

        private static long GetRemainingCooldownMs(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory, double fallbackMultiplier)
        {
            if (entity?.Api?.World is null) return 0;

            lock (_syncLock)
            {
                long lastStartMs = entity.WatchedAttributes?.GetLong(key, 0) ?? 0;
                if (lastStartMs == 0) return 0;

                long now = entity.Api.World.ElapsedMilliseconds;

                // Server restart: ElapsedMilliseconds resets to 0, but WatchedAttributes persist.
                if (lastStartMs > now && now >= 0)
                {
                    entity.WatchedAttributes?.SetLong(key, 0);
                    entity.WatchedAttributes?.MarkPathDirty(key);
                    return 0;
                }

                double multiplier = multiplierFactory?.Invoke(entity) ?? fallbackMultiplier;
                long cooldownMs = (long)(durationSeconds * 1000.0 * multiplier);
                long elapsed = now - lastStartMs;

                return Math.Max(0, cooldownMs - elapsed);
            }
        }

        /// <summary>
        /// Returns the cooldown progress as a fraction from 0.0 (just started) to 1.0 (ready).
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="durationSeconds">The duration seconds value.</param>
        /// <param name="multiplier">The multiplier value.</param>
        /// <returns>The cooldown progress.</returns>
        public static float GetCooldownProgress(this Entity entity, string key, double durationSeconds, double multiplier = 1.0)
            => GetCooldownProgress(entity, key, durationSeconds, null, multiplier);

        /// <summary>
        /// Returns the cooldown progress using a multiplier factory.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="durationSeconds">The duration seconds value.</param>
        /// <param name="multiplierFactory">The multiplier factory value.</param>
        /// <returns>The cooldown progress.</returns>
        public static float GetCooldownProgress(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory)
            => GetCooldownProgress(entity, key, durationSeconds, multiplierFactory, 1.0);

        private static float GetCooldownProgress(this Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory, double fallbackMultiplier)
        {
            if (entity?.Api?.World is null || durationSeconds <= 0) return 1f;

            lock (_syncLock)
            {
                double multiplier = multiplierFactory?.Invoke(entity) ?? fallbackMultiplier;
                long totalMs = (long)(durationSeconds * 1000.0 * multiplier);
                if (totalMs <= 0) return 1f;

                long remainingMs = GetRemainingCooldownMsCore(entity, key, durationSeconds, multiplierFactory, fallbackMultiplier);
                return 1f - (float)remainingMs / totalMs;
            }
        }

        private static long GetRemainingCooldownMsCore(Entity entity, string key, double durationSeconds, CooldownMultiplier? multiplierFactory, double fallbackMultiplier)
        {
            long lastStartMs = entity.WatchedAttributes?.GetLong(key, 0) ?? 0;
            if (lastStartMs == 0) return 0;

            long now = entity.Api.World.ElapsedMilliseconds;

            if (lastStartMs > now && now >= 0)
            {
                entity.WatchedAttributes?.SetLong(key, 0);
                entity.WatchedAttributes?.MarkPathDirty(key);
                return 0;
            }

            double multiplier = multiplierFactory?.Invoke(entity) ?? fallbackMultiplier;
            long cooldownMs = (long)(durationSeconds * 1000.0 * multiplier);
            long elapsed = now - lastStartMs;

            return Math.Max(0, cooldownMs - elapsed);
        }

        /// <summary>
        /// Resets the cooldown so that the next <see cref="IsReady(Vintagestory.API.Common.Entities.Entity, string, double, double)" /> call returns true.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        public static void ResetCooldown(this Entity entity, string key)
        {
            if (entity?.WatchedAttributes is null) return;

            lock (_syncLock)
            {
                entity.WatchedAttributes.SetLong(key, 0);
                entity.WatchedAttributes.MarkPathDirty(key);
            }
        }
    }
}
