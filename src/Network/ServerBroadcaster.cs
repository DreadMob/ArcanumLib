using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Network;

/// <summary>
/// Helper for sending packets to all online players with optional filtering.
/// Works with any <see cref="IServerNetworkChannel"/> and supports per-player
/// predicates, radius-based filtering, and exclusion lists.
/// </summary>
public static class ServerBroadcaster
{
    /// <summary>
    /// Sends a packet to all online players.
    /// </summary>
    /// <typeparam name="T">The packet type.</typeparam>
    /// <param name="sapi">The server API.</param>
    /// <param name="channel">The network channel to send on.</param>
    /// <param name="packet">The packet instance to send.</param>
    public static void BroadcastPacket<T>(
        ICoreServerAPI sapi,
        IServerNetworkChannel channel,
        T packet)
    {
        if (sapi == null || channel == null) return;

        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            if (player is IServerPlayer sp)
                channel.SendPacket(packet, sp);
        }
    }

    /// <summary>
    /// Sends a packet to all online players matching the given predicate.
    /// </summary>
    public static void BroadcastPacket<T>(
        ICoreServerAPI sapi,
        IServerNetworkChannel channel,
        T packet,
        System.Func<IServerPlayer, bool> predicate)
    {
        if (sapi == null || channel == null || predicate == null) return;

        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            if (player is IServerPlayer sp && predicate(sp))
                channel.SendPacket(packet, sp);
        }
    }

    /// <summary>
    /// Sends a packet to all online players within the given radius of a position.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    /// <param name="channel">The network channel.</param>
    /// <param name="packet">The packet to send.</param>
    /// <param name="centerX">Center X in world coordinates.</param>
    /// <param name="centerY">Center Y in world coordinates.</param>
    /// <param name="centerZ">Center Z in world coordinates.</param>
    /// <param name="radius">Radius in blocks.</param>
    public static void BroadcastPacketInRange<T>(
        ICoreServerAPI sapi,
        IServerNetworkChannel channel,
        T packet,
        double centerX,
        double centerY,
        double centerZ,
        double radius)
    {
        if (sapi == null || channel == null || radius <= 0) return;
        double radiusSq = radius * radius;

        bool Predicate(IServerPlayer sp)
        {
            var pos = sp.Entity?.Pos;
            if (pos == null) return false;
            double dx = pos.X - centerX;
            double dy = pos.Y - centerY;
            double dz = pos.Z - centerZ;
            return dx * dx + dy * dy + dz * dz <= radiusSq;
        }

        BroadcastPacket(sapi, channel, packet, Predicate);
    }

    /// <summary>
    /// Sends a packet to all online players except those in the exclusion list.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    /// <param name="channel">The network channel.</param>
    /// <param name="packet">The packet to send.</param>
    /// <param name="excludePlayerUids">Player UIDs to exclude.</param>
    public static void BroadcastPacketExcept<T>(
        ICoreServerAPI sapi,
        IServerNetworkChannel channel,
        T packet,
        IEnumerable<string> excludePlayerUids)
    {
        if (sapi == null || channel == null) return;
        var exclude = excludePlayerUids != null
            ? new HashSet<string>(excludePlayerUids, StringComparer.Ordinal)
            : s_emptySet;

        bool Predicate(IServerPlayer sp)
            => !exclude.Contains(sp.PlayerUID);

        BroadcastPacket(sapi, channel, packet, Predicate);
    }

    /// <summary>
    /// Sends a packet to all online players in the given group (by role or custom grouping).
    /// </summary>
    /// <param name="sapi">The server API.</param>
    /// <param name="channel">The network channel.</param>
    /// <param name="packet">The packet to send.</param>
    /// <param name="groupPredicate">Predicate that defines group membership.</param>
    public static void BroadcastPacketToGroup<T>(
        ICoreServerAPI sapi,
        IServerNetworkChannel channel,
        T packet,
        System.Func<IServerPlayer, bool> groupPredicate)
        => BroadcastPacket(sapi, channel, packet, groupPredicate);

    private static readonly HashSet<string> s_emptySet = new(0, StringComparer.Ordinal);
}
