using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Server-side snapshot synchronization for a data-driven HUD.
/// Manages dirty players, rate-limited sends, and cleanup on disposal.
/// Specific mod syncs can inherit and customize message type registration or logging.
/// </summary>
/// <typeparam name="TSnapshot">The type of the tsnapshot value.</typeparam>
public class HudSnapshotSync<TSnapshot> : IDisposable
    where TSnapshot : class, IHudSnapshot, new()
{
    /// <summary>Server API used to resolve players and send packets.</summary>
    protected readonly ICoreServerAPI sapi;

    /// <summary>Network channel the snapshots are sent over.</summary>
    protected readonly IServerNetworkChannel channel;

    /// <summary>Factory that builds the per-player snapshot. May return null to skip sending.</summary>
    protected readonly Func<IServerPlayer, TSnapshot> buildSnapshot;

    /// <summary>Players whose snapshot needs to be rebuilt and sent.</summary>
    protected readonly HashSet<string> dirtyPlayers;

    /// <summary>Last send time in ms for each active player.</summary>
    protected readonly Dictionary<string, long> lastSentMs;

    /// <summary>Minimum interval between sends for a single player.</summary>
    protected readonly int rateLimitMs;

    /// <summary>True after <see cref="Dispose" /> has been called.</summary>
    protected bool disposed;

    /// <summary>
    /// Creates a snapshot sync for an already registered network channel.
    /// The caller is responsible for registering <typeparamref name="TSnapshot" /> on the channel.
    /// </summary>
    /// <param name="sapi">The server API instance.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="buildSnapshot">The server player.</param>
    /// <param name="rateLimitMs">The rate limit ms value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sapi" /> is <see langword="null" />.</exception>
    public HudSnapshotSync(
        ICoreServerAPI sapi,
        IServerNetworkChannel channel,
        Func<IServerPlayer, TSnapshot> buildSnapshot,
        int rateLimitMs = 500)
    {
        this.sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
        this.buildSnapshot = buildSnapshot ?? throw new ArgumentNullException(nameof(buildSnapshot));
        this.rateLimitMs = rateLimitMs > 0 ? rateLimitMs : 500;

        dirtyPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        lastSentMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Marks a player's snapshot as dirty so it is rebuilt on the next process tick.</summary>
    /// <param name="playerUid">The unique player identifier.</param>
    public virtual void MarkDirty(string playerUid)
    {
        if (!string.IsNullOrWhiteSpace(playerUid))
            dirtyPlayers.Add(playerUid);
    }

    /// <summary>Sends a removal snapshot to the player and clears their rate-limit state.</summary>
    /// <param name="playerUid">The unique player identifier.</param>
    public virtual void SendRemoval(string playerUid)
    {
        if (string.IsNullOrWhiteSpace(playerUid)) return;

        try
        {
            var player = sapi.World.PlayerByUid(playerUid) as IServerPlayer;
            if (player?.ConnectionState == EnumClientState.Playing)
            {
                var snapshot = new TSnapshot();
                snapshot.MarkRemoved();
                channel.SendPacket(snapshot, player);
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning("[ArcanumLib] HudSnapshotSync SendRemoval failed for '{0}': {1}", playerUid, ex.Message);
        }
        finally
        {
            lastSentMs.Remove(playerUid);
        }
    }

    /// <summary>
    /// Processes all dirty players and sends updated snapshots while respecting the rate limit.
    /// </summary>
    /// <param name="nowMs">The now ms value.</param>
    /// <param name="inRangeUids">The collection of in range uids values.</param>
    public virtual void ProcessUpdates(long nowMs, IEnumerable<string> inRangeUids)
    {
        if (disposed) return;

        var activeSet = BuildActiveSet(inRangeUids);
        bool changed = RemoveDepartedPlayers(activeSet);
        changed |= MarkNewArrivals(activeSet);

        if (changed)
        {
            foreach (var uid in activeSet)
                dirtyPlayers.Add(uid);
        }

        if (dirtyPlayers.Count == 0) return;

        var toProcess = new List<string>(dirtyPlayers);
        dirtyPlayers.Clear();

        foreach (var uid in toProcess)
        {
            if (!activeSet.Contains(uid)) continue;

            if (lastSentMs.TryGetValue(uid, out long lastSent) && nowMs - lastSent < rateLimitMs)
            {
                dirtyPlayers.Add(uid);
                continue;
            }

            var player = sapi.World.PlayerByUid(uid) as IServerPlayer;
            if (player?.ConnectionState != EnumClientState.Playing) continue;

            TSnapshot snapshot;
            try
            {
                snapshot = buildSnapshot(player);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[ArcanumLib] HudSnapshotSync buildSnapshot failed for player '{0}': {1}", uid, ex.Message);
                dirtyPlayers.Add(uid);
                continue;
            }

            if (snapshot == null)
            {
                lastSentMs.Remove(uid);
                continue;
            }

            if (!snapshot.IsRemoved() && lastSentMs.TryGetValue(uid, out lastSent) && nowMs - lastSent < rateLimitMs)
            {
                dirtyPlayers.Add(uid);
                continue;
            }

            SendPacket(uid, snapshot);

            if (snapshot.IsRemoved())
                lastSentMs.Remove(uid);
            else
                lastSentMs[uid] = nowMs;
        }
    }

    /// <summary>Sends removal snapshots to all active players and marks the sync as disposed.</summary>
    public virtual void Dispose()
    {
        if (disposed) return;
        var uids = new List<string>(lastSentMs.Keys);
        foreach (var uid in uids)
            SendRemoval(uid);
        disposed = true;
        dirtyPlayers.Clear();
        lastSentMs.Clear();
    }

    /// <summary>Builds a case-insensitive set of active player UIDs.</summary>
    /// <param name="inRangeUids">The collection of in range uids values.</param>
    /// <returns>The active set.</returns>
    protected HashSet<string> BuildActiveSet(IEnumerable<string> inRangeUids)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (inRangeUids == null) return set;

        foreach (var uid in inRangeUids)
        {
            if (!string.IsNullOrWhiteSpace(uid))
                set.Add(uid);
        }
        return set;
    }

    /// <summary>Sends removal snapshots to players that are no longer in range.</summary>
    /// <param name="activeSet">The active set value.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    protected bool RemoveDepartedPlayers(HashSet<string> activeSet)
    {
        bool any = false;
        var departed = new List<string>();
        foreach (var uid in lastSentMs.Keys)
        {
            if (!activeSet.Contains(uid))
                departed.Add(uid);
        }

        foreach (var uid in departed)
        {
            any = true;
            SendRemoval(uid);
        }
        return any;
    }

    /// <summary>Marks newly arrived players as dirty so they get a snapshot immediately.</summary>
    /// <param name="activeSet">The active set value.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    protected bool MarkNewArrivals(HashSet<string> activeSet)
    {
        bool any = false;
        foreach (var uid in activeSet)
        {
            if (!lastSentMs.ContainsKey(uid))
            {
                any = true;
                dirtyPlayers.Add(uid);
            }
        }
        return any;
    }

    /// <summary>Sends a snapshot to a single player. Logs warnings on failure.</summary>
    /// <param name="playerUid">The unique player identifier.</param>
    /// <param name="snapshot">The snapshot value.</param>
    protected virtual void SendPacket(string playerUid, TSnapshot snapshot)
    {
        try
        {
            var player = sapi.World.PlayerByUid(playerUid) as IServerPlayer;
            if (player?.ConnectionState == EnumClientState.Playing)
                channel.SendPacket(snapshot, player);
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning("[ArcanumLib] HudSnapshotSync SendPacket failed for '{0}': {1}", playerUid, ex.Message);
        }
    }
}
