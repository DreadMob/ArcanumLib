using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects;

/// <summary>
/// Instance-based store for per-entity immunities and resistances to status effects.
/// Immunities completely block effects whose tags match.
/// Resistances reduce the effective duration of matching effects.
/// Registered in <see cref="Core.ArcanumServices" /> and disposed with the <see cref="Core.ArcanumRuntime" />.
/// </summary>
public sealed class EffectResistanceService : IDisposable
{
    private sealed class EntityModifiers
    {
        public HashSet<string> Immunities = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> Resistances = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ConcurrentDictionary<long, EntityModifiers> _store = new();
    private bool _disposed;

    /// <summary>
    /// Adds a full immunity to effects with the given tag.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="tag">The tag value.</param>
    public void AddImmunity(Entity entity, string tag)
    {
        if (entity == null || string.IsNullOrWhiteSpace(tag)) return;
        var mods = _store.GetOrAdd(entity.EntityId, _ => new EntityModifiers());
        lock (mods) { mods.Immunities.Add(tag); }
    }

    /// <summary>
    /// Removes an immunity by tag.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="tag">The tag value.</param>
    public void RemoveImmunity(Entity entity, string tag)
    {
        if (entity == null || string.IsNullOrWhiteSpace(tag)) return;
        if (!_store.TryGetValue(entity.EntityId, out var mods)) return;
        lock (mods) { mods.Immunities.Remove(tag); }
    }

    /// <summary>
    /// Adds a resistance (0..1) to effects with the given tag.
    /// 0 = no resistance, 0.5 = 50% duration reduction, 1 = full immunity equivalent.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="tag">The tag value.</param>
    /// <param name="amount">The amount value.</param>
    public void AddResistance(Entity entity, string tag, float amount)
    {
        if (entity == null || string.IsNullOrWhiteSpace(tag)) return;
        amount = Math.Clamp(amount, 0f, 1f);
        var mods = _store.GetOrAdd(entity.EntityId, _ => new EntityModifiers());
        lock (mods) { mods.Resistances[tag] = amount; }
    }

    /// <summary>
    /// Removes a resistance by tag.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="tag">The tag value.</param>
    public void RemoveResistance(Entity entity, string tag)
    {
        if (entity == null || string.IsNullOrWhiteSpace(tag)) return;
        if (!_store.TryGetValue(entity.EntityId, out var mods)) return;
        lock (mods) { mods.Resistances.Remove(tag); }
    }

    /// <summary>
    /// Returns true if the entity is fully immune to the given tag.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="tag">The tag value.</param>
    /// <returns>true if immune; otherwise, false.</returns>
    public bool IsImmune(Entity entity, string tag)
    {
        if (entity == null || string.IsNullOrWhiteSpace(tag)) return false;
        if (!_store.TryGetValue(entity.EntityId, out var mods)) return false;
        lock (mods) { return mods.Immunities.Contains(tag); }
    }

    /// <summary>
    /// Returns true if the entity is immune to any of the effect's tags.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="effect">The effect value.</param>
    /// <returns>true if immune to effect; otherwise, false.</returns>
    public bool IsImmuneToEffect(Entity entity, IStatusEffect effect)
    {
        if (entity == null || effect == null) return false;
        if (!_store.TryGetValue(entity.EntityId, out var mods)) return false;
        lock (mods)
        {
            foreach (var tag in effect.Tags)
            {
                if (mods.Immunities.Contains(tag)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Returns the effective duration multiplier for an effect (0..1).
    /// 1.0 = full duration, 0.5 = half duration, 0 = blocked.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="effect">The effect value.</param>
    /// <returns>The duration multiplier.</returns>
    public float GetDurationMultiplier(Entity entity, IStatusEffect effect)
    {
        if (entity == null || effect == null) return 1f;
        if (!_store.TryGetValue(entity.EntityId, out var mods)) return 1f;

        float minMultiplier = 1f;
        lock (mods)
        {
            foreach (var tag in effect.Tags)
            {
                if (mods.Immunities.Contains(tag)) return 0f;
                if (mods.Resistances.TryGetValue(tag, out var resist))
                {
                    float mult = 1f - resist;
                    if (mult < minMultiplier) minMultiplier = mult;
                }
            }
        }
        return minMultiplier;
    }

    /// <summary>
    /// Clears all immunities and resistances for the entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public void Clear(Entity entity)
    {
        if (entity == null) return;
        _store.TryRemove(entity.EntityId, out _);
    }

    /// <summary>
    /// Clears all stored modifiers. Intended for world shutdown.
    /// </summary>
    public void ClearAll()
    {
        _store.Clear();
    }

    /// <summary>
    /// Disposes the service and clears all stored modifiers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearAll();
    }
}
