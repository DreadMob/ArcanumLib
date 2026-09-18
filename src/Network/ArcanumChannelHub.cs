using System;
using System.Collections.Concurrent;
using Vintagestory.API.Common;

namespace ArcanumLib.Network;

/// <summary>
/// Process-wide registry of <see cref="TypedNetworkChannel" /> instances keyed by
/// (game side, channel name). Two mods — or two call sites in one mod — that
/// request the same channel name on the same side receive the same
/// <see cref="TypedNetworkChannel" /> wrapper, so the underlying Vintage Story
/// channel is only registered once.
/// </summary>
/// <remarks>
/// <see cref="TypedNetworkChannel.Register" /> is idempotent and
/// <see cref="TypedNetworkChannel.AddMessageType{T}" /> deduplicates message types
/// via a <see cref="System.Collections.Generic.HashSet{T}" />, so sharing a wrapper
/// is safe: additional message-type registrations through the shared channel are
/// additive. Note however that Vintage Story keeps a single message handler per
/// packet type per channel — if two consumers attach handlers for the same packet
/// type via <see cref="TypedNetworkChannel.On{T}" /> or
/// <see cref="TypedNetworkChannel.OnServer{T}" />, the most recent registration
/// wins.
/// </remarks>
public static class ArcanumChannelHub
{
    private static readonly ConcurrentDictionary<(EnumAppSide, string), TypedNetworkChannel> _channels = new();

    /// <summary>
    /// Returns the shared <see cref="TypedNetworkChannel" /> for
    /// <paramref name="name" /> on the side of <paramref name="api" />, creating
    /// and registering it on first use.
    /// </summary>
    /// <param name="api">The core API, either client or server.</param>
    /// <param name="name">The channel name. Should be unique across mods, e.g. <c>"mymod-main"</c>.</param>
    /// <returns>The shared, already-registered channel wrapper.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name" /> is null or whitespace.</exception>
    public static TypedNetworkChannel GetOrCreate(ICoreAPI api, string name)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Channel name cannot be empty.", nameof(name));

        return _channels.GetOrAdd((api.Side, name), _ => new TypedNetworkChannel(api, name).Register());
    }

    /// <summary>
    /// Returns the shared channel for <paramref name="name" /> on the side of
    /// <paramref name="api" /> if it was already created via
    /// <see cref="GetOrCreate" />.
    /// </summary>
    /// <param name="api">The core API, either client or server.</param>
    /// <param name="name">The channel name.</param>
    /// <param name="channel">When this method returns, contains the shared channel, or <c>null</c>.</param>
    /// <returns><c>true</c> if a shared channel exists; otherwise <c>false</c>.</returns>
    public static bool TryGet(ICoreAPI api, string name, out TypedNetworkChannel? channel)
    {
        channel = null;
        if (api == null || string.IsNullOrWhiteSpace(name)) return false;
        return _channels.TryGetValue((api.Side, name), out channel);
    }

    /// <summary>
    /// Removes the shared wrapper for <paramref name="name" /> from the hub. The
    /// underlying Vintage Story channel registration cannot be undone — a later
    /// <see cref="GetOrCreate" /> re-registers the same channel name.
    /// </summary>
    /// <param name="api">The core API, either client or server.</param>
    /// <param name="name">The channel name.</param>
    /// <returns><c>true</c> if a wrapper was removed; otherwise <c>false</c>.</returns>
    public static bool Forget(ICoreAPI api, string name)
    {
        if (api == null || string.IsNullOrWhiteSpace(name)) return false;
        return _channels.TryRemove((api.Side, name), out _);
    }

    /// <summary>
    /// Drops all cached wrappers. The game-side channel registrations persist;
    /// subsequent <see cref="GetOrCreate" /> calls re-register the same names.
    /// Intended for test cleanup and world-unload resets.
    /// </summary>
    public static void Clear() => _channels.Clear();
}
