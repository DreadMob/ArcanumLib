using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ArcanumLib.Common
{
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
}
