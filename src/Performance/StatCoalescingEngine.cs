using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Coalesces multiple <see cref="EntityStats.Set"/> calls within a time window into a
/// single network sync. Useful for reducing packet spam when stats change rapidly
/// (equipment swaps, buffs, debuffs).
/// </summary>
public class StatCoalescingEngine : ModSystem
{
    private ICoreServerAPI? _sapi;

    /// <summary>
    /// Enables or disables coalescing at runtime. When disabled, queued stats are applied
    /// immediately.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Default category used when none is supplied to <see cref="QueueStatUpdate"/>.
    /// </summary>
    public static string DefaultCategory { get; set; } = "game";

    /// <summary>
    /// Optional watched attribute path to mark dirty after all coalesced stats are applied.
    /// Set this if the consuming mod uses a watched attribute to trigger stat syncing.
    /// </summary>
    public static string? MarkDirtyAttributePath { get; set; }

    /// <summary>
    /// Time window in milliseconds during which stat updates are coalesced.
    /// </summary>
    public static int CoalesceWindowMs { get; set; } = 200;

    /// <summary>
    /// Maximum delay in milliseconds before a forced flush.
    /// </summary>
    public static int MaxDelayMs { get; set; } = 1000;

    private class CoalescedUpdate
    {
        public Dictionary<string, float> Stats = new();
        public bool IsFlushing;
    }

    private static readonly Dictionary<long, CoalescedUpdate> PendingUpdates = new();
    private static readonly object _syncLock = new();

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        api.Event.PlayerDisconnect += OnPlayerDisconnect;
        api.Logger.Notification("[ArcanumLib] StatCoalescingEngine started.");
    }

    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;

            List<long> keys;
            lock (_syncLock)
            {
                keys = PendingUpdates.Keys.ToList();
            }
            foreach (var key in keys)
            {
                DeferredWork.Cancel(StatKey(key));
            }
            lock (_syncLock)
            {
                PendingUpdates.Clear();
            }
        }
        base.Dispose();
    }

    /// <summary>
    /// Queues a stat update for coalescing. It may not be applied immediately.
    /// </summary>
    public static void QueueStatUpdate(
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

        CoalescedUpdate update;
        bool needsSchedule;
        lock (_syncLock)
        {
            if (!PendingUpdates.TryGetValue(entityId, out var existing))
            {
                update = new CoalescedUpdate();
                PendingUpdates[entityId] = update;
            }
            else
            {
                update = existing;
            }

            needsSchedule = update.Stats.Count == 0 && !update.IsFlushing;
        }

        if (needsSchedule)
        {
            DeferredWork.Coalesce(
                StatKey(entityId),
                () => FlushUpdates(api, entityId),
                CoalesceWindowMs,
                MaxDelayMs);
        }

        string statKey = string.IsNullOrEmpty(category) ? stat : $"{category}:{stat}";
        lock (_syncLock)
        {
            update.Stats[statKey] = value;
        }
    }

    /// <summary>
    /// Queues several stat updates at once.
    /// </summary>
    public static void QueueStatUpdates(
        ICoreServerAPI api,
        EntityPlayer player,
        Dictionary<string, float> stats,
        string? category = null)
    {
        if (player?.EntityId == null) return;

        category ??= DefaultCategory;

        long entityId = player.EntityId;

        CoalescedUpdate update;
        bool needsSchedule;
        lock (_syncLock)
        {
            if (!PendingUpdates.TryGetValue(entityId, out var existing))
            {
                update = new CoalescedUpdate();
                PendingUpdates[entityId] = update;
            }
            else
            {
                update = existing;
            }

            needsSchedule = update.Stats.Count == 0 && !update.IsFlushing;
        }

        if (needsSchedule)
        {
            DeferredWork.Coalesce(
                StatKey(entityId),
                () => FlushUpdates(api, entityId),
                CoalesceWindowMs,
                MaxDelayMs);
        }

        lock (_syncLock)
        {
            foreach (var stat in stats)
            {
                string statKey = string.IsNullOrEmpty(category) ? stat.Key : $"{category}:{stat.Key}";
                update.Stats[statKey] = stat.Value;
            }
        }
    }

    /// <summary>
    /// Forces an immediate flush for the given player.
    /// </summary>
    public static void ForceFlush(ICoreServerAPI api, long entityId)
    {
        if (!IsEnabled) return;

        lock (_syncLock)
        {
            if (!PendingUpdates.TryGetValue(entityId, out var update)) return;
            if (update.IsFlushing) return;
        }

        DeferredWork.Cancel(StatKey(entityId));
        FlushUpdates(api, entityId);
    }

    /// <summary>
    /// Applies a single stat immediately, bypassing coalescing.
    /// </summary>
    public static void ApplyStatImmediate(
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
    public static bool HasPendingUpdates(long entityId)
    {
        lock (_syncLock)
        {
            return PendingUpdates.TryGetValue(entityId, out var update) && update.Stats.Count > 0;
        }
    }

    /// <summary>
    /// Total number of pending stat updates across all players.
    /// </summary>
    public static int GetPendingUpdateCount()
    {
        lock (_syncLock)
        {
            return PendingUpdates.Values.Sum(u => u.Stats.Count);
        }
    }

    /// <summary>
    /// Clears all pending updates and cancels scheduled flushes.
    /// </summary>
    public static void ClearAllPending(ICoreServerAPI api)
    {
        List<long> keys;
        lock (_syncLock)
        {
            keys = PendingUpdates.Keys.ToList();
        }
        foreach (var key in keys)
        {
            DeferredWork.Cancel(StatKey(key));
        }
        lock (_syncLock)
        {
            PendingUpdates.Clear();
        }
    }

    private static string StatKey(long entityId) => $"stat-coalesce-{entityId}";

    private static void FlushUpdates(ICoreServerAPI api, long entityId)
    {
        CoalescedUpdate? update;
        lock (_syncLock)
        {
            if (!PendingUpdates.TryGetValue(entityId, out update)) return;
            if (update.IsFlushing) return;
            update.IsFlushing = true;
        }

        var entity = api.World.GetEntityById(entityId) as EntityPlayer;
        if (entity?.EntityId == null)
        {
            lock (_syncLock)
            {
                PendingUpdates.Remove(entityId);
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
            PendingUpdates.Remove(entityId);
        }
    }

    private static (string category, string statName) ParseStatKey(string statKey)
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

        DeferredWork.Cancel(StatKey(entityId));
        lock (_syncLock)
        {
            PendingUpdates.Remove(entityId);
        }
    }
}
