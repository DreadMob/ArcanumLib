using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// Interface for an action executor that tracks cooldowns and executes descriptors.
/// </summary>
public interface IActionExecutorService
{
    /// <summary>
    /// Executes an <see cref="ActionDescriptor" /> with cooldown and permission checks.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="context">The operation context.</param>
    /// <returns>The execute result.</returns>
    ActionResult Execute(ActionDescriptor descriptor, ActionContext context);

    /// <summary>
    /// Returns the remaining cooldown in milliseconds for the given player and action.
    /// </summary>
    /// <param name="playerEntityId">The player entity id value.</param>
    /// <param name="actionId">The action id value.</param>
    /// <param name="sapi">The server API instance.</param>
    /// <returns>The remaining cooldown.</returns>
    long GetRemainingCooldown(long playerEntityId, string actionId, ICoreServerAPI sapi);

    /// <summary>
    /// Returns the remaining cooldown in milliseconds for the given player and action.
    /// </summary>
    /// <param name="playerEntityId">The player entity id value.</param>
    /// <param name="actionId">The action id value.</param>
    /// <returns>The remaining cooldown.</returns>
    long GetRemainingCooldown(long playerEntityId, string actionId);

    /// <summary>
    /// Clears all cooldowns for the given player. Call on disconnect.
    /// </summary>
    /// <param name="playerEntityId">The player entity id value.</param>
    void ClearCooldowns(long playerEntityId);

    /// <summary>
    /// Clears all cooldown state. Intended for world unload / test teardown.
    /// </summary>
    void ClearAllCooldowns();
}

/// <summary>
/// Instance-based executor for <see cref="ActionDescriptor" /> instances.
/// Tracks per-player, per-action cooldowns and delegates execution to
/// <see cref="IActionRegistryService" />.
/// </summary>
public sealed class ActionExecutorService : IActionExecutorService
{
    private readonly ICoreServerAPI? _sapi;
    private readonly IActionRegistryService _registry;
    private readonly Dictionary<long, Dictionary<string, long>> _cooldowns = new();
    private readonly object _syncLock = new();

    /// <summary>
    /// Creates an executor with an optional server API used for cooldown timing.
    /// If no API is supplied, the service will resolve one from <see cref="ArcanumServices" />.
    /// </summary>
    /// <param name="sapi">The server API instance.</param>
    /// <param name="registry">The action registry used to execute actions. If null, resolves from <see cref="ArcanumServices" />.</param>
    public ActionExecutorService(ICoreServerAPI? sapi = null, IActionRegistryService? registry = null)
    {
        _sapi = sapi;
        _registry = registry ?? ArcanumServices.Get<IActionRegistryService>()
            ?? throw new InvalidOperationException(
                "IActionRegistryService is not registered. Ensure ActionRegistryModSystem has started.");
    }

    /// <summary>
    /// Executes an <see cref="ActionDescriptor" /> with cooldown and permission checks.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="context">The operation context.</param>
    /// <returns>The execute.</returns>
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

        var result = _registry.Execute(descriptor, effectiveContext);

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
    /// <param name="playerEntityId">The player entity id value.</param>
    /// <param name="actionId">The action id value.</param>
    /// <param name="sapi">The server API instance.</param>
    /// <returns>The remaining cooldown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sapi" /> is <see langword="null" />.</exception>
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
    /// <param name="playerEntityId">The player entity id value.</param>
    /// <param name="actionId">The action id value.</param>
    /// <returns>The remaining cooldown.</returns>
    public long GetRemainingCooldown(long playerEntityId, string actionId)
    {
        var sapi = _sapi ?? ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server);
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
    /// <param name="playerEntityId">The player entity id value.</param>
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
