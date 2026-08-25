using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Coalesces multiple <see cref="EntityStats.Set" /> calls within a time window into a
/// single network sync. Useful for reducing packet spam when stats change rapidly
/// (equipment swaps, buffs, debuffs).
/// </summary>
/// <remarks>
/// Register an instance in <see cref="ArcanumServices" /> during server startup and
/// dispose it on world unload. The static members delegate to the registered instance
/// for backward compatibility.
/// </remarks>
public class StatCoalescingEngine : IDisposable
{
    private ICoreServerAPI? _sapi;

    /// <summary>
    /// Enables or disables coalescing at runtime. When disabled, queued stats are applied
    /// immediately.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Default category used when none is supplied to <see cref="QueueStatUpdate" />.
    /// </summary>
    public string DefaultCategory { get; set; } = "game";

    /// <summary>
    /// Optional watched attribute path to mark dirty after all coalesced stats are applied.
    /// Set this if the consuming mod uses a watched attribute to trigger stat syncing.
    /// </summary>
    public string? MarkDirtyAttributePath { get; set; }

    /// <summary>
    /// Time window in milliseconds during which stat updates are coalesced.
    /// </summary>
    public int CoalesceWindowMs { get; set; } = 200;

    /// <summary>
    /// Maximum delay in milliseconds before a forced flush.
    /// </summary>
    public int MaxDelayMs { get; set; } = 1000;

    private class CoalescedUpdate
    {
        public Dictionary<string, float> Stats = new();
        public bool IsFlushing;
    }

    private readonly Dictionary<long, CoalescedUpdate> _pendingUpdates = new();
    private readonly object _syncLock = new();

    /// <summary>
    /// Starts the coalescing engine on the server and hooks player disconnect cleanup.
    /// </summary>
    /// <param name="api">The server API instance.</param>
    public void Start(ICoreServerAPI api)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        _sapi = api;
        api.Event.PlayerDisconnect += OnPlayerDisconnect;
        api.Logger.Notification("[ArcanumLib] StatCoalescingEngine started.");
    }

    /// <summary>
    /// Stops the coalescing engine, cancels pending deferred work and clears the queue.
    /// </summary>
    public void Stop()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;

            List<long> keys;
            lock (_syncLock)
            {
                keys = _pendingUpdates.Keys.ToList();
            }
            foreach (var key in keys)
            {
                ArcanumServices.Get<DeferredWorkService>()?.Cancel(StatKey(key));
            }
            lock (_syncLock)
            {
                _pendingUpdates.Clear();
            }
        }
        _sapi = null;
    }

    /// <summary>
    /// Queues a stat update for coalescing. It may not be applied immediately.
    /// </summary>
    /// <param name="api">The server API instance.</param>
    /// <param name="player">The player.</param>
    /// <param name="stat">The stat value.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <param name="category">The category value.</param>
    public void QueueStatUpdate(
        ICoreServerAPI api,
        EntityPlayer player,
        string stat,
        float value,
        string? category = null)
    {
        category ??= DefaultCategory;

        if (!IsEnabled)
        {
            player?.Stats.Set(stat, category, value, true);
            return;
        }

        if (player?.EntityId == null) return;

        long entityId = player.EntityId;
        string statKey = string.IsNullOrEmpty(category) ? stat : $"{category}:{stat}";

        bool needsSchedule;
        lock (_syncLock)
        {
            if (!_pendingUpdates.TryGetValue(entityId, out var update))
            {
                update = new CoalescedUpdate();
                _pendingUpdates[entityId] = update;
            }

            update.Stats[statKey] = value;
            needsSchedule = update.Stats.Count == 1 && !update.IsFlushing;
        }

        if (needsSchedule)
        {
            ArcanumServices.Get<DeferredWorkService>()?.Coalesce(
                StatKey(entityId),
                () => FlushUpdates(api, entityId),
                CoalesceWindowMs,
                MaxDelayMs);
        }
    }

    /// <summary>
    /// Queues several stat updates at once.
    /// </summary>
    /// <param name="api">The server API instance.</param>
    /// <param name="player">The player.</param>
    /// <param name="stats">The stats value.</param>
    /// <param name="category">The category value.</param>
    public void QueueStatUpdates(
        ICoreServerAPI api,
        EntityPlayer player,
        Dictionary<string, float> stats,
        string? category = null)
    {
        if (player?.EntityId == null) return;

        category ??= DefaultCategory;

        long entityId = player.EntityId;
        int addedCount = stats?.Count ?? 0;

        bool needsSchedule;
        lock (_syncLock)
        {
            if (!_pendingUpdates.TryGetValue(entityId, out var update))
            {
                update = new CoalescedUpdate();
                _pendingUpdates[entityId] = update;
            }

            if (stats != null)
            {
                foreach (var stat in stats)
                {
                    string statKey = string.IsNullOrEmpty(category) ? stat.Key : $"{category}:{stat.Key}";
                    update.Stats[statKey] = stat.Value;
                }
            }

            needsSchedule = update.Stats.Count == addedCount && !update.IsFlushing;
        }

        if (needsSchedule)
        {
            ArcanumServices.Get<DeferredWorkService>()?.Coalesce(
                StatKey(entityId),
                () => FlushUpdates(api, entityId),
                CoalesceWindowMs,
                MaxDelayMs);
        }
    }

    /// <summary>
    /// Forces an immediate flush for the given player.
    /// </summary>
    /// <param name="api">The server API instance.</param>
    /// <param name="entityId">The entity id value.</param>
    public void ForceFlush(ICoreServerAPI api, long entityId)
    {
        if (!IsEnabled) return;

        lock (_syncLock)
        {
            if (!_pendingUpdates.TryGetValue(entityId, out var update)) return;
            if (update.IsFlushing) return;
        }

        ArcanumServices.Get<DeferredWorkService>()?.Cancel(StatKey(entityId));
        FlushUpdates(api, entityId);
    }

    /// <summary>
    /// Applies a single stat immediately, bypassing coalescing.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="stat">The stat value.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <param name="category">The category value.</param>
    public void ApplyStatImmediate(
        EntityPlayer player,
        string stat,
        float value,
        string? category = null)
    {
        if (player?.EntityId == null) return;
        player.Stats.Set(stat, category ?? DefaultCategory, value, true);
    }

    /// <summary>
    /// Returns true if the player has pending stat updates.
    /// </summary>
    /// <param name="entityId">The entity id value.</param>
    /// <returns>true if the operation has pending updates; otherwise, false.</returns>
    public bool HasPendingUpdates(long entityId)
    {
        lock (_syncLock)
        {
            return _pendingUpdates.TryGetValue(entityId, out var update) && update.Stats.Count > 0;
        }
    }

    /// <summary>
    /// Total number of pending stat updates across all players.
    /// </summary>
    /// <returns>The pending update count.</returns>
    public int GetPendingUpdateCount()
    {
        lock (_syncLock)
        {
            return _pendingUpdates.Values.Sum(u => u.Stats.Count);
        }
    }

    /// <summary>
    /// Clears all pending updates and cancels scheduled flushes.
    /// </summary>
    /// <param name="api">The server API instance.</param>
    public void ClearAllPending(ICoreServerAPI api)
    {
        List<long> keys;
        lock (_syncLock)
        {
            keys = _pendingUpdates.Keys.ToList();
        }
        foreach (var key in keys)
        {
            ArcanumServices.Get<DeferredWorkService>()?.Cancel(StatKey(key));
        }
        lock (_syncLock)
        {
            _pendingUpdates.Clear();
        }
    }

    /// <summary>
    /// Disposes the engine, stopping it and clearing all pending updates.
    /// </summary>
    public void Dispose()
    {
        Stop();
    }

    private string StatKey(long entityId) => $"stat-coalesce-{entityId}";

    private void FlushUpdates(ICoreServerAPI api, long entityId)
    {
        CoalescedUpdate? update;
        lock (_syncLock)
        {
            if (!_pendingUpdates.TryGetValue(entityId, out update)) return;
            if (update.IsFlushing) return;
            update.IsFlushing = true;
        }

        var entity = api.World.GetEntityById(entityId) as EntityPlayer;
        if (entity?.EntityId == null)
        {
            lock (_syncLock)
            {
                _pendingUpdates.Remove(entityId);
            }
            return;
        }

        List<KeyValuePair<string, float>> stats;
        lock (_syncLock)
        {
            stats = update.Stats.ToList();
        }

        for (int i = 0; i < stats.Count; i++)
        {
            var kv = stats[i];
            (string category, string statName) = ParseStatKey(kv.Key);
            bool isLast = i == stats.Count - 1;
            entity.Stats.Set(statName, category, kv.Value, isLast);
        }

        if (!string.IsNullOrEmpty(MarkDirtyAttributePath))
        {
            entity.WatchedAttributes.MarkPathDirty(MarkDirtyAttributePath);
        }

        lock (_syncLock)
        {
            _pendingUpdates.Remove(entityId);
        }
    }

    private (string category, string statName) ParseStatKey(string statKey)
    {
        int colonIndex = statKey.IndexOf(':');
        if (colonIndex > 0)
        {
            return (statKey.Substring(0, colonIndex), statKey.Substring(colonIndex + 1));
        }

        return (DefaultCategory, statKey);
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        long entityId = player.Entity.EntityId;

        ArcanumServices.Get<DeferredWorkService>()?.Cancel(StatKey(entityId));
        lock (_syncLock)
        {
            _pendingUpdates.Remove(entityId);
        }
    }

    // ── Static convenience facade ────────────────────────────────────────

    private static StatCoalescingEngine? Instance => ArcanumServices.Get<StatCoalescingEngine>();

    /// <summary>
    /// Starts the registered engine instance. Delegates to the instance in <see cref="ArcanumServices"/>.
    /// </summary>
    public static void StartStatic(ICoreServerAPI api) => Instance?.Start(api);

    /// <summary>
    /// Stops the registered engine instance. Delegates to the instance in <see cref="ArcanumServices"/>.
    /// </summary>
    public static void StopStatic() => Instance?.Stop();
}
