using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects;

/// <summary>
/// Interface for an instance-based manager of status effects on entities.
/// </summary>
public interface IStatusEffectService
{
    /// <summary>Raised when a new effect instance is created.</summary>
    event Action<Entity, IStatusEffectInstance>? OnEffectApplied;

    /// <summary>Raised when an existing effect's duration is refreshed.</summary>
    event Action<Entity, IStatusEffectInstance>? OnEffectRefreshed;

    /// <summary>Raised when an effect's stack count increases.</summary>
    event Action<Entity, IStatusEffectInstance>? OnEffectStacked;

    /// <summary>Raised when an effect is removed because it expired.</summary>
    event Action<Entity, IStatusEffectInstance>? OnEffectExpired;

    /// <summary>Raised when an effect is removed manually or because the entity died.</summary>
    event Action<Entity, IStatusEffectInstance>? OnEffectRemoved;

    /// <summary>
    /// Applies an effect to an entity.
    /// </summary>
    IStatusEffectInstance? Apply(Entity? entity, IStatusEffect effect, float durationMs, object? data = null);

    /// <summary>
    /// Removes all effects with the given code from the entity.
    /// </summary>
    bool Remove(Entity? entity, string effectCode);

    /// <summary>
    /// Removes a specific effect instance from the entity.
    /// </summary>
    bool Remove(Entity? entity, long instanceId);

    /// <summary>
    /// Removes all active effects from the entity.
    /// </summary>
    bool RemoveAll(Entity? entity);

    /// <summary>
    /// Removes all effects matching the given category from the entity.
    /// </summary>
    bool RemoveByCategory(Entity? entity, EffectCategory category);

    /// <summary>
    /// Returns true if the entity has an active effect with the given code.
    /// </summary>
    bool Has(Entity? entity, string effectCode);

    /// <summary>
    /// Returns all active effect instances on the entity.
    /// </summary>
    IReadOnlyCollection<IStatusEffectInstance> GetActive(Entity? entity);

    /// <summary>
    /// Ticks all active effects by the given elapsed time.
    /// </summary>
    void Tick(float dt);

    /// <summary>
    /// Clears the manager. Intended for tests and shutdown paths.
    /// </summary>
    void Clear();
}

/// <summary>
/// Instance-based manager for applying, ticking, and removing status effects on entities.
/// Register with <see cref="ArcanumServices" />; resolve via <see cref="ArcanumRuntime.Current" />.<see cref="ArcanumRuntime.Services" />.
/// </summary>
public class StatusEffectService : IStatusEffectService
{
    private readonly ConcurrentDictionary<long, StatusEffectContainer> _containers = new();
    private readonly object _sync = new();
    private long _nextInstanceId;

    /// <summary>
    /// Raised when a new effect instance is created.
    /// </summary>
    public event Action<Entity, IStatusEffectInstance>? OnEffectApplied;

    /// <summary>
    /// Raised when an existing effect's duration is refreshed.
    /// </summary>
    public event Action<Entity, IStatusEffectInstance>? OnEffectRefreshed;

    /// <summary>
    /// Raised when an effect's stack count increases.
    /// </summary>
    public event Action<Entity, IStatusEffectInstance>? OnEffectStacked;

    /// <summary>
    /// Raised when an effect is removed because it expired.
    /// </summary>
    public event Action<Entity, IStatusEffectInstance>? OnEffectExpired;

    /// <summary>
    /// Raised when an effect is removed manually or because the entity died.
    /// </summary>
    public event Action<Entity, IStatusEffectInstance>? OnEffectRemoved;

    /// <summary>
    /// Applies an effect to an entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="effect">The effect to apply.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="data">Optional payload.</param>
    /// <returns>The active or updated effect instance, or <c>null</c> if the entity is null or the effect was resisted.</returns>
    public IStatusEffectInstance? Apply(Entity? entity, IStatusEffect effect, float durationMs, object? data = null)
    {
        if (entity == null) return null;

        lock (_sync)
        {
            // Check immunities and resistances
            var resistance = ArcanumServices.Get<IEffectResistanceService>();
            if (resistance != null)
            {
                if (resistance.IsImmuneToEffect(entity, effect)) return null;
                float durationMult = resistance.GetDurationMultiplier(entity, effect);
                if (durationMult <= 0f) return null;
                durationMs *= durationMult;
            }

            var container = _containers.GetOrAdd(entity.EntityId, _ => new StatusEffectContainer(entity, GetNextInstanceId));
            var (instance, result, oldInstance) = container.Apply(effect, durationMs, data);

            if (instance == null) return null;

            switch (result)
            {
                case StatusEffectApplyResult.New:
                    SafeApply(entity, instance);
                    OnEffectApplied?.Invoke(entity, instance);
                    break;

                case StatusEffectApplyResult.Refreshed:
                    OnEffectRefreshed?.Invoke(entity, instance);
                    break;

                case StatusEffectApplyResult.Stacked:
                    SafeApply(entity, instance);
                    OnEffectStacked?.Invoke(entity, instance);
                    break;

                case StatusEffectApplyResult.Overridden:
                    if (oldInstance != null)
                    {
                        SafeRemove(entity, oldInstance, expired: false);
                    }
                    SafeApply(entity, instance);
                    OnEffectApplied?.Invoke(entity, instance);
                    break;
            }

            return instance;
        }
    }

    /// <summary>
    /// Removes all effects with the given code from the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="effectCode">The effect code to remove.</param>
    /// <returns><c>true</c> if any effect was removed.</returns>
    public bool Remove(Entity? entity, string effectCode)
    {
        if (entity == null) return false;

        lock (_sync)
        {
            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            var removed = container.RemoveByCode(effectCode);

            foreach (var instance in removed)
            {
                SafeRemove(entity, instance, expired: false);
            }

            if (container.IsEmpty)
            {
                _containers.TryRemove(entity.EntityId, out _);
            }

            return removed.Count > 0;
        }
    }

    /// <summary>
    /// Removes a specific effect instance from the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="instanceId">The unique instance id.</param>
    /// <returns><c>true</c> if the instance was removed.</returns>
    public bool Remove(Entity? entity, long instanceId)
    {
        if (entity == null) return false;

        lock (_sync)
        {
            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            var instance = container.RemoveById(instanceId);

            if (instance != null)
            {
                SafeRemove(entity, instance, expired: false);
            }

            if (container.IsEmpty)
            {
                _containers.TryRemove(entity.EntityId, out _);
            }

            return instance != null;
        }
    }

    /// <summary>
    /// Removes all active effects from the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <returns><c>true</c> if any effect was removed.</returns>
    public bool RemoveAll(Entity? entity)
    {
        if (entity == null) return false;

        lock (_sync)
        {
            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            var removed = container.RemoveAll();

            foreach (var instance in removed)
            {
                SafeRemove(entity, instance, expired: false);
            }

            _containers.TryRemove(entity.EntityId, out _);
            return removed.Count > 0;
        }
    }

    /// <summary>
    /// Removes all effects matching the given category from the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="category">The category to remove (Buff, Debuff, or None).</param>
    /// <returns><c>true</c> if any effect was removed.</returns>
    public bool RemoveByCategory(Entity? entity, EffectCategory category)
    {
        if (entity == null || category == EffectCategory.None) return false;

        lock (_sync)
        {
            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            var removed = container.RemoveByCategory(category);

            foreach (var instance in removed)
            {
                SafeRemove(entity, instance, expired: false);
            }

            if (container.IsEmpty)
            {
                _containers.TryRemove(entity.EntityId, out _);
            }

            return removed.Count > 0;
        }
    }

    /// <summary>
    /// Returns true if the entity has an active effect with the given code.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="effectCode">The effect code.</param>
    /// <returns><c>true</c> if at least one matching effect is active.</returns>
    public bool Has(Entity? entity, string effectCode)
    {
        if (entity == null) return false;

        lock (_sync)
        {
            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;
            return container.Instances.Any(i => i.Code == effectCode);
        }
    }

    /// <summary>
    /// Returns all active effect instances on the entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <returns>A read-only view of the active instances, or an empty collection if none.</returns>
    public IReadOnlyCollection<IStatusEffectInstance> GetActive(Entity? entity)
    {
        if (entity == null) return Array.Empty<IStatusEffectInstance>();

        lock (_sync)
        {
            if (!_containers.TryGetValue(entity.EntityId, out var container)) return Array.Empty<IStatusEffectInstance>();
            return container.Instances.ToArray();
        }
    }

    /// <summary>
    /// Ticks all active effects by the given elapsed time.
    /// </summary>
    /// <param name="dt">Seconds since the last tick.</param>
    public void Tick(float dt)
    {
        lock (_sync)
        {
            foreach (var kvp in _containers)
            {
                var container = kvp.Value;
                var entity = container.Entity;
                if (entity is null)
                {
                    _containers.TryRemove(kvp.Key, out _);
                    continue;
                }

                var result = container.Tick(dt);

                foreach (var instance in result.Expired)
                {
                    SafeRemove(entity, instance, expired: true);
                }

                foreach (var instance in result.RemovedByDeath)
                {
                    SafeRemove(entity, instance, expired: false);
                }

                foreach (var instance in result.Alive)
                {
                    if (instance.Effect.HasTick)
                        SafeTick(entity, instance, dt);
                }

                if (container.IsEmpty)
                {
                    _containers.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Clears the manager. Intended for tests and shutdown paths.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _containers.Clear();
            Interlocked.Exchange(ref _nextInstanceId, 0);
        }
    }

    private long GetNextInstanceId() => Interlocked.Increment(ref _nextInstanceId);

    private void SafeApply(Entity entity, StatusEffectInstance instance)
    {
        try
        {
            instance.Effect.OnApply(entity, instance);
        }
        catch (Exception ex)
        {
            (entity.Api as ICoreAPI)?.Logger?.Warning("[ArcanumLib] [StatusEffects] OnApply failed for {0}: {1}", instance.Code, ex.Message);
        }
    }

    private void SafeRemove(Entity entity, StatusEffectInstance instance, bool expired)
    {
        try
        {
            instance.Effect.OnRemove(entity, instance);
        }
        catch (Exception ex)
        {
            (entity.Api as ICoreAPI)?.Logger?.Warning("[ArcanumLib] [StatusEffects] OnRemove failed for {0}: {1}", instance.Code, ex.Message);
        }

        if (expired)
        {
            OnEffectExpired?.Invoke(entity, instance);
        }
        else
        {
            OnEffectRemoved?.Invoke(entity, instance);
        }
    }

    private void SafeTick(Entity entity, StatusEffectInstance instance, float dt)
    {
        try
        {
            instance.Effect.OnTick(entity, instance, dt);
        }
        catch (Exception ex)
        {
            (entity.Api as ICoreAPI)?.Logger?.Warning("[ArcanumLib] [StatusEffects] OnTick failed for {0}: {1}", instance.Code, ex.Message);
        }
    }
}
