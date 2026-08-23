using System;
using System.Collections.Generic;
using ArcanumLib.Persistence;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// High-level helper that executes <see cref="ActionDescriptor"/> instances through
/// <see cref="ActionRegistry"/>, applying cooldown and permission checks declared on
/// the descriptor. Cooldowns are tracked per-player per-action-id and are server-side only.
/// </summary>
public static class ActionExecutor
{
    private static readonly Dictionary<long, Dictionary<string, long>> _cooldowns = new();
    private static readonly object _syncLock = new();

    /// <summary>
    /// Executes an <see cref="ActionDescriptor"/> with cooldown and permission checks.
    /// </summary>
    public static ActionResult Execute(ActionDescriptor descriptor, ActionContext context)
    {
        if (descriptor == null) return ActionResult.Invalid("Action descriptor is null.");

        // Permission check.
        if (!string.IsNullOrWhiteSpace(descriptor.RequiredPermission) && context.Player != null)
        {
            if (!context.Player.HasPrivilege(descriptor.RequiredPermission))
            {
                return ActionResult.NotAvailable(
                    $"Player lacks permission '{descriptor.RequiredPermission}' for action '{descriptor.Id}'.");
            }
        }

        // Cooldown check.
        if (descriptor.CooldownMs > 0 && context.PlayerEntity != null)
        {
            long entityId = context.PlayerEntity.EntityId;
            long now = context.Sapi.World.ElapsedMilliseconds;

            lock (_syncLock)
            {
                if (_cooldowns.TryGetValue(entityId, out var byAction)
                    && byAction.TryGetValue(descriptor.Id, out var until)
                    && now < until)
                {
                    long remaining = until - now;
                    return ActionResult.NotAvailable(
                        $"Action '{descriptor.Id}' is on cooldown ({remaining}ms remaining).");
                }
            }
        }

        // Build a context with the descriptor's args.
        var effectiveContext = new ActionContext(
            context.Sapi,
            context.Player,
            context.ItemSlot,
            context.TargetPos,
            descriptor.Args);

        var result = ActionRegistry.Execute(descriptor, effectiveContext);

        // Record cooldown on success.
        if (result.IsSuccess && descriptor.CooldownMs > 0 && context.PlayerEntity != null)
        {
            long entityId = context.PlayerEntity.EntityId;
            long now = context.Sapi.World.ElapsedMilliseconds;
            long until = now + descriptor.CooldownMs;

            lock (_syncLock)
            {
                if (!_cooldowns.TryGetValue(entityId, out var byAction))
                {
                    byAction = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    _cooldowns[entityId] = byAction;
                }
                byAction[descriptor.Id] = until;
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the remaining cooldown in milliseconds for the given player and action,
    /// or 0 if not on cooldown. Uses the currently registered server API time.
    /// </summary>
    [Obsolete("Use the overload that accepts ICoreServerAPI to avoid mixed time sources.")]
    public static long GetRemainingCooldown(long playerEntityId, string actionId)
    {
        var sapi = ModDataStore.Sapi;
        if (sapi == null)
        {
            // Without a server API we cannot compute a reliable remaining time.
            return 0;
        }
        return GetRemainingCooldown(playerEntityId, actionId, sapi);
    }

    /// <summary>
    /// Returns the remaining cooldown in milliseconds for the given player and action,
    /// using the provided server API's world time.
    /// </summary>
    public static long GetRemainingCooldown(long playerEntityId, string actionId, ICoreServerAPI sapi)
    {
        if (sapi == null) throw new ArgumentNullException(nameof(sapi));
        if (string.IsNullOrWhiteSpace(actionId)) return 0;

        long now = sapi.World.ElapsedMilliseconds;
        lock (_syncLock)
        {
            if (!_cooldowns.TryGetValue(playerEntityId, out var byAction)) return 0;
            if (!byAction.TryGetValue(actionId, out var until)) return 0;
            return Math.Max(0, until - now);
        }
    }

    /// <summary>
    /// Clears all cooldowns for the given player. Call on disconnect.
    /// </summary>
    public static void ClearCooldowns(long playerEntityId)
    {
        lock (_syncLock)
        {
            _cooldowns.Remove(playerEntityId);
        }
    }

    /// <summary>
    /// Clears all cooldown state. Intended for world unload / test teardown.
    /// </summary>
    public static void ClearAllCooldowns()
    {
        lock (_syncLock)
        {
            _cooldowns.Clear();
        }
    }
}
