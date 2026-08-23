using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects;

/// <summary>
/// Static facade for the <see cref="StatusEffectService"/>.
/// </summary>
public static class StatusEffectManager
{
    private static StatusEffectService? Current => ArcanumServices.Get<StatusEffectService>();

    /// <summary>
    /// Raised when a new effect instance is created.
    /// </summary>
    public static event Action<Entity, IStatusEffectInstance>? OnEffectApplied
    {
        add { if (Current is { } s) s.OnEffectApplied += value; }
        remove { if (Current is { } s) s.OnEffectApplied -= value; }
    }

    /// <summary>
    /// Raised when an existing effect's duration is refreshed.
    /// </summary>
    public static event Action<Entity, IStatusEffectInstance>? OnEffectRefreshed
    {
        add { if (Current is { } s) s.OnEffectRefreshed += value; }
        remove { if (Current is { } s) s.OnEffectRefreshed -= value; }
    }

    /// <summary>
    /// Raised when an effect's stack count increases.
    /// </summary>
    public static event Action<Entity, IStatusEffectInstance>? OnEffectStacked
    {
        add { if (Current is { } s) s.OnEffectStacked += value; }
        remove { if (Current is { } s) s.OnEffectStacked -= value; }
    }

    /// <summary>
    /// Raised when an effect is removed because it expired.
    /// </summary>
    public static event Action<Entity, IStatusEffectInstance>? OnEffectExpired
    {
        add { if (Current is { } s) s.OnEffectExpired += value; }
        remove { if (Current is { } s) s.OnEffectExpired -= value; }
    }

    /// <summary>
    /// Raised when an effect is removed manually or because the entity died.
    /// </summary>
    public static event Action<Entity, IStatusEffectInstance>? OnEffectRemoved
    {
        add { if (Current is { } s) s.OnEffectRemoved += value; }
        remove { if (Current is { } s) s.OnEffectRemoved -= value; }
    }

    /// <summary>
    /// Applies an effect to an entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="effect">The effect to apply.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="data">Optional payload.</param>
    /// <returns>The active or updated effect instance, or null if the entity was null.</returns>
    public static IStatusEffectInstance? Apply(Entity? entity, IStatusEffect effect, float durationMs, object? data = null)
        => Current?.Apply(entity, effect, durationMs, data);

    /// <summary>
    /// Removes all effects with the given code from the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="effectCode">The effect code to remove.</param>
    /// <returns>True if any effect was removed.</returns>
    public static bool Remove(Entity? entity, string effectCode)
        => Current?.Remove(entity, effectCode) ?? false;

    /// <summary>
    /// Removes a specific effect instance from the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="instanceId">The unique instance id.</param>
    /// <returns>True if the instance was removed.</returns>
    public static bool Remove(Entity? entity, long instanceId)
        => Current?.Remove(entity, instanceId) ?? false;

    /// <summary>
    /// Removes all active effects from the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <returns>True if any effect was removed.</returns>
    public static bool RemoveAll(Entity? entity)
        => Current?.RemoveAll(entity) ?? false;

    /// <summary>
    /// Returns true if the entity has an active effect with the given code.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="effectCode">The effect code.</param>
    /// <returns>True if at least one matching effect is active.</returns>
    public static bool Has(Entity? entity, string effectCode)
        => Current?.Has(entity, effectCode) ?? false;

    /// <summary>
    /// Returns all active effect instances on the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <returns>A read-only view of the active instances, or an empty collection if none.</returns>
    public static IReadOnlyCollection<IStatusEffectInstance> GetActive(Entity? entity)
        => Current?.GetActive(entity) ?? Array.Empty<IStatusEffectInstance>();

    /// <summary>
    /// Ticks all active effects by the given elapsed time.
    /// </summary>
    /// <param name="dt">Seconds since the last tick.</param>
    public static void Tick(float dt)
        => Current?.Tick(dt);

    /// <summary>
    /// Clears the manager. Intended for tests and shutdown paths.
    /// </summary>
    public static void Clear()
        => Current?.Clear();
}
