using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// Instance-based executor for <see cref="ActionDescriptor"/> instances.
/// Tracks per-player, per-action cooldowns and delegates execution to
/// <see cref="ActionRegistry"/>.
/// </summary>
public class ActionExecutorService
{
    private readonly ICoreServerAPI? _sapi;
    private readonly Dictionary<long, Dictionary<string, long>> _cooldowns = new();
    private readonly object _syncLock = new();

    /// <summary>
    /// Creates an executor with an optional server API used for cooldown timing.
    /// If no API is supplied, the service will resolve one from <see cref="ArcanumServices"/>.
    /// </summary>
    public ActionExecutorService(ICoreServerAPI? sapi = null)
    {
        _sapi = sapi;
    }

    /// <summary>
    /// Executes an <see cref="ActionDescriptor"/> with cooldown and permission checks.
    /// </summary>
    public ActionResult Execute(ActionDescriptor descriptor, ActionContext context)
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

        // Declarative condition check.
        if (descriptor.Condition != null && !descriptor.Condition.Evaluate(context))
        {
            return ActionResult.NotAvailable(
                $"Condition for action '{descriptor.Id}' was not satisfied.");
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
    /// using the provided server API's world time.
    /// </summary>
    public long GetRemainingCooldown(long playerEntityId, string actionId, ICoreServerAPI sapi)
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
    /// Returns the remaining cooldown in milliseconds for the given player and action,
    /// or 0 if not on cooldown. Uses the currently registered server API time.
    /// </summary>
    public long GetRemainingCooldown(long playerEntityId, string actionId)
    {
        var sapi = _sapi ?? ArcanumServices.Get<ICoreServerAPI>() ?? ModDataStore.Sapi;
        if (sapi == null)
        {
            // Without a server API we cannot compute a reliable remaining time.
            return 0;
        }
        return GetRemainingCooldown(playerEntityId, actionId, sapi);
    }

    /// <summary>
    /// Clears all cooldowns for the given player. Call on disconnect.
    /// </summary>
    public void ClearCooldowns(long playerEntityId)
    {
        lock (_syncLock)
        {
            _cooldowns.Remove(playerEntityId);
        }
    }

    /// <summary>
    /// Clears all cooldown state. Intended for world unload / test teardown.
    /// </summary>
    public void ClearAllCooldowns()
    {
        lock (_syncLock)
        {
            _cooldowns.Clear();
        }
    }
}
