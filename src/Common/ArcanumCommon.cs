
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Extension methods for <see cref="ICoreAPI" />, <see cref="ICoreClientAPI" />,
    /// <see cref="ICoreServerAPI" />, and <see cref="IWorldAccessor" />.
    /// </summary>
    public static class ApiExtensions
    {
        /// <summary>
        /// Returns true if the API is running on the client side.
        /// </summary>
        /// <param name="api">The core API instance.</param>
        /// <returns>true if client; otherwise, false.</returns>
        public static bool IsClient(this ICoreAPI api) => api?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the API is running on the server side.
        /// </summary>
        /// <param name="api">The core API instance.</param>
        /// <returns>true if server; otherwise, false.</returns>
        public static bool IsServer(this ICoreAPI api) => api?.Side == EnumAppSide.Server;

        /// <summary>
        /// Returns true if the client API is running on the client side.
        /// </summary>
        /// <param name="capi">The client API instance.</param>
        /// <returns>true if client; otherwise, false.</returns>
        public static bool IsClient(this ICoreClientAPI capi) => capi?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the client API is running on the server side.
        /// </summary>
        /// <param name="capi">The client API instance.</param>
        /// <returns>true if server; otherwise, false.</returns>
        public static bool IsServer(this ICoreClientAPI capi) => capi?.Side == EnumAppSide.Server;

        /// <summary>
        /// Returns true if the world accessor is running on the client side.
        /// </summary>
        /// <param name="world">The world accessor.</param>
        /// <returns>true if client; otherwise, false.</returns>
        public static bool IsClient(this IWorldAccessor? world) => world?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the world accessor is running on the server side.
        /// </summary>
        /// <param name="world">The world accessor.</param>
        /// <returns>true if server; otherwise, false.</returns>
        public static bool IsServer(this IWorldAccessor? world) => world?.Side == EnumAppSide.Server;
    }
    /// <summary>
    /// Extension methods for <see cref="IPlayer" /> / <see cref="IServerPlayer" />.
    /// </summary>
    public static class PlayerExtensions
    {
        /// <summary>
        /// Returns true if the player has a spawned entity with a valid position.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <returns>true if the operation has valid position; otherwise, false.</returns>
        public static bool HasValidPosition(this IPlayer player)
            => player?.Entity?.Pos != null;

        /// <summary>
        /// Iterates over the supplied players and yields only those with a living entity and a valid position.
        /// </summary>
        /// <param name="players">The player.</param>
        /// <returns>A collection of alive entities values.</returns>
        public static IEnumerable<(IPlayer Player, Entity Entity)> GetAliveEntities(this IEnumerable<IPlayer> players)
        {
            if (players == null) yield break;

            foreach (var player in players)
            {
                if (player == null) continue;
                var entity = player.Entity;
                if (entity == null || !entity.Alive || entity.Pos == null) continue;
                yield return (player, entity);
            }
        }

        /// <summary>
        /// Iterates over the supplied players and yields only <see cref="IServerPlayer" /> instances with a living entity and a valid position.
        /// </summary>
        /// <param name="players">The player.</param>
        /// <returns>A collection of alive server entities values.</returns>
        public static IEnumerable<(IServerPlayer Player, Entity Entity)> GetAliveServerEntities(this IEnumerable<IPlayer> players)
        {
            if (players == null) yield break;

            foreach (var player in players)
            {
                if (player is not IServerPlayer sp) continue;
                var entity = sp.Entity;
                if (entity == null || !entity.Alive || entity.Pos == null) continue;
                yield return (sp, entity);
            }
        }
    }
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
    /// <summary>
    /// Helpers for formatting chat and HUD text with Vintage Story font color tags.
    /// </summary>
    public static class ChatFormatUtil
    {
        /// <summary>
        /// Wraps <paramref name="text" /> in a <c>&lt;font color="..."&gt;</c> tag.
        /// Returns empty string for null/whitespace text, and the original text if no color is supplied.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <param name="hexColor">The hex color value.</param>
        /// <returns>The font string, or null if none is found.</returns>
        public static string Font(string text, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (string.IsNullOrWhiteSpace(hexColor)) return text;

            return $"<font color=\"{hexColor}\">{text}</font>";
        }

        /// <summary>
        /// Builds an alert-prefixed message with default styling: red <c>[!]</c> prefix and white text.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <returns>The prefix alert string, or null if none is found.</returns>
        public static string PrefixAlert(string text)
        {
            return PrefixAlert(text, "[!] ", "#ff5555", "#ffffff");
        }

        /// <summary>
        /// Builds an alert-prefixed message with custom colors and the default <c>[!] </c> prefix.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <param name="prefixColor">The prefix color value.</param>
        /// <param name="textColor">The text color value.</param>
        /// <returns>The prefix alert string, or null if none is found.</returns>
        public static string PrefixAlert(string text, string prefixColor, string textColor)
        {
            return PrefixAlert(text, "[!] ", prefixColor, textColor);
        }

        /// <summary>
        /// Builds an alert-prefixed message with a custom prefix string and colors.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <param name="prefix">The prefix value.</param>
        /// <param name="prefixColor">The prefix color value.</param>
        /// <param name="textColor">The text color value.</param>
        /// <returns>The prefix alert string, or null if none is found.</returns>
        public static string PrefixAlert(string text, string prefix, string prefixColor, string textColor)
        {
            return $"{Font(prefix, prefixColor)}{Font(text, textColor)}";
        }
    }
}