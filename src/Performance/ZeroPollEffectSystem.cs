using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Schedules timed effects using <see cref="IGameCalendar.RegisterCallback"/> instead of
/// tick-based polling. This removes CPU overhead while an effect is running and only runs
/// the expiration logic when the duration elapses.
/// </summary>
public class ZeroPollEffectSystem : ModSystem
{
    private ICoreServerAPI? _sapi;
    private long _cleanupTickListenerId;

    /// <summary>
    /// Enables or disables the zero-poll scheduler at runtime.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Interval in milliseconds between periodic cleanups of orphaned callbacks.
    /// </summary>
    public static int CleanupIntervalMs { get; set; } = 30000;

    private static readonly Dictionary<long, Dictionary<string, long>> ActiveCallbacks = new();
    private static readonly HashSet<long> PendingCleanup = new();

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        api.Event.PlayerDisconnect += OnPlayerDisconnect;
        _cleanupTickListenerId = api.Event.RegisterGameTickListener(OnPeriodicCleanup, CleanupIntervalMs);
        api.Logger.Notification("[ArcanumLib] ZeroPollEffectSystem started.");
    }

    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;

            if (_cleanupTickListenerId != 0)
            {
                _sapi.Event.UnregisterGameTickListener(_cleanupTickListenerId);
                _cleanupTickListenerId = 0;
            }

            foreach (var callbacks in ActiveCallbacks.Values)
            {
                foreach (var callbackId in callbacks.Values)
                {
                    _sapi.Event.UnregisterCallback(callbackId);
                }
            }

            ActiveCallbacks.Clear();
            PendingCleanup.Clear();
        }

        base.Dispose();
    }

    /// <summary>
    /// Applies a timed effect and schedules <paramref name="onExpire"/> to run after
    /// <paramref name="durationMs"/>. No polling occurs while the effect is active.
    /// </summary>
    public static void ApplyTimedEffect(
        ICoreServerAPI api,
        EntityPlayer player,
        string effectType,
        int durationMs,
        Action<EntityPlayer>? onApply,
        Action<EntityPlayer>? onExpire)
    {
        if (!IsEnabled)
        {
            try { onApply?.Invoke(player); }
            catch (Exception ex) { api.Logger?.Warning("[ZeroPollEffectSystem] Disabled fallback apply failed: {0}", ex); }
            return;
        }

        if (player?.EntityId == null) return;

        long entityId = player.EntityId;

        if (!ActiveCallbacks.TryGetValue(entityId, out var callbacks))
        {
            callbacks = new Dictionary<string, long>();
            ActiveCallbacks[entityId] = callbacks;
        }

        if (callbacks.TryGetValue(effectType, out var existingId))
        {
            api.Event.UnregisterCallback(existingId);
        }

        try
        {
            onApply?.Invoke(player);
        }
        catch (Exception ex)
        {
            api.Logger?.Error("[ZeroPollEffectSystem] Error applying effect '{0}' for player {1}: {2}", effectType, entityId, ex);
        }

        long callbackId = api.Event.RegisterCallback((_) =>
        {
            try
            {
                onExpire?.Invoke(player);
            }
            catch (Exception ex)
            {
                api.Logger?.Error("[ZeroPollEffectSystem] Error expiring effect '{0}' for player {1}: {2}", effectType, entityId, ex);
            }

            if (ActiveCallbacks.TryGetValue(entityId, out var cb))
            {
                cb.Remove(effectType);
                if (cb.Count == 0)
                {
                    ActiveCallbacks.Remove(entityId);
                }
            }
        }, durationMs);

        callbacks[effectType] = callbackId;
    }

    /// <summary>
    /// Cancels an active timed effect before it expires.
    /// </summary>
    public static void CancelEffect(ICoreServerAPI api, long entityId, string effectType)
    {
        if (!ActiveCallbacks.TryGetValue(entityId, out var callbacks)) return;
        if (!callbacks.TryGetValue(effectType, out var callbackId)) return;

        api.Event.UnregisterCallback(callbackId);
        callbacks.Remove(effectType);

        if (callbacks.Count == 0)
        {
            ActiveCallbacks.Remove(entityId);
        }
    }

    /// <summary>
    /// Returns true if the player has an active effect of the given type.
    /// </summary>
    public static bool HasActiveEffect(long entityId, string effectType)
    {
        return ActiveCallbacks.TryGetValue(entityId, out var callbacks) && callbacks.ContainsKey(effectType);
    }

    /// <summary>
    /// Cancels an existing effect and schedules a new one with the given additional duration.
    /// </summary>
    public static void ExtendEffect(
        ICoreServerAPI api,
        EntityPlayer player,
        string effectType,
        int additionalDurationMs,
        Action<EntityPlayer>? onApply,
        Action<EntityPlayer>? onExpire)
    {
        CancelEffect(api, player.EntityId, effectType);
        ApplyTimedEffect(api, player, effectType, additionalDurationMs, onApply, onExpire);
    }

    /// <summary>
    /// Cancels all effects for the player.
    /// </summary>
    public static void ClearAllEffects(ICoreServerAPI api, long entityId)
    {
        if (!ActiveCallbacks.TryGetValue(entityId, out var callbacks)) return;

        foreach (var callbackId in callbacks.Values)
        {
            api.Event.UnregisterCallback(callbackId);
        }

        ActiveCallbacks.Remove(entityId);
    }

    /// <summary>
    /// Total number of active scheduled effects.
    /// </summary>
    public static int GetActiveEffectCount()
    {
        int count = 0;
        foreach (var callbacks in ActiveCallbacks.Values)
        {
            count += callbacks.Count;
        }
        return count;
    }

    /// <summary>
    /// Number of players that have at least one active scheduled effect.
    /// </summary>
    public static int GetActivePlayerCount()
    {
        return ActiveCallbacks.Count;
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        PendingCleanup.Add(player.Entity.EntityId);
    }

    private void OnPeriodicCleanup(float dt)
    {
        if (PendingCleanup.Count == 0) return;

        foreach (var entityId in PendingCleanup)
        {
            if (_sapi != null)
            {
                ClearAllEffects(_sapi, entityId);
            }
        }

        PendingCleanup.Clear();

        var toRemove = new List<long>();
        foreach (var kvp in ActiveCallbacks)
        {
            if (_sapi?.World.GetEntityById(kvp.Key) == null)
            {
                foreach (var callbackId in kvp.Value.Values)
                {
                    _sapi?.Event.UnregisterCallback(callbackId);
                }
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var id in toRemove)
        {
            ActiveCallbacks.Remove(id);
        }
    }
}
