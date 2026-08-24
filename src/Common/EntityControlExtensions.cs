using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Generic extension methods for entity watched-attribute helpers and player walk-speed syncing.
    /// AI control methods that depend on game-content assemblies remain in the consumer mod.
    /// </summary>
    public static class EntityControlExtensions
    {
        /// <summary>
        /// Sets a boolean on the entity's watched attributes and marks the path dirty,
        /// but only if the value actually changed. Reduces unnecessary network sync.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value to set or compare.</param>
        public static void SetWatchedBoolDirty(this Entity entity, string key, bool value)
        {
            var wa = entity?.WatchedAttributes;
            if (wa == null) return;

            bool prev = wa.GetBool(key, false);
            if (prev == value) return;

            wa.SetBool(key, value);
            wa.MarkPathDirty(key);
        }

        /// <summary>
        /// Updates a player's walkSpeed only if the blended value has changed by more than
        /// <paramref name="epsilon" />. Reduces network sync spam from frequent walkSpeed updates.
        /// Returns true if walkSpeed was updated.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="epsilon">The epsilon value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        public static bool UpdatePlayerWalkSpeed(this EntityPlayer player, float epsilon = 0.001f)
        {
            if (player?.Stats == null) return false;

            float targetSpeed = player.Stats.GetBlended("walkspeed");
            if (float.IsNaN(targetSpeed)) targetSpeed = 0f;

            if (Math.Abs(player.walkSpeed - targetSpeed) > epsilon)
            {
                player.walkSpeed = targetSpeed;
                return true;
            }

            return false;
        }
    }
}
