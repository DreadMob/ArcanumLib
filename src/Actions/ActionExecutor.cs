using System;
using ArcanumLib.Core;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// Static facade for the <see cref="ActionExecutorService"/>.
/// </summary>
public static class ActionExecutor
{
    private static ActionExecutorService Service =>
        ArcanumServices.Get<ActionExecutorService>()
        ?? throw new InvalidOperationException(
            "ActionExecutor has not been initialized. Ensure ActionRegistryModSystem is loaded.");

    /// <summary>
    /// Executes an <see cref="ActionDescriptor"/> with cooldown and permission checks.
    /// </summary>
    public static ActionResult Execute(ActionDescriptor descriptor, ActionContext context)
        => Service.Execute(descriptor, context);

    /// <summary>
    /// Returns the remaining cooldown in milliseconds for the given player and action,
    /// or 0 if not on cooldown. Uses the currently registered server API time.
    /// </summary>
    public static long GetRemainingCooldown(long playerEntityId, string actionId)
        => Service.GetRemainingCooldown(playerEntityId, actionId);

    /// <summary>
    /// Returns the remaining cooldown in milliseconds for the given player and action,
    /// using the provided server API's world time.
    /// </summary>
    public static long GetRemainingCooldown(long playerEntityId, string actionId, ICoreServerAPI sapi)
        => Service.GetRemainingCooldown(playerEntityId, actionId, sapi);

    /// <summary>
    /// Clears all cooldowns for the given player. Call on disconnect.
    /// </summary>
    public static void ClearCooldowns(long playerEntityId)
        => Service.ClearCooldowns(playerEntityId);

    /// <summary>
    /// Clears all cooldown state. Intended for world unload / test teardown.
    /// </summary>
    public static void ClearAllCooldowns()
        => Service.ClearAllCooldowns();
}
