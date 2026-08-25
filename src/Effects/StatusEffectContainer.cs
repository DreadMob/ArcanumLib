using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects
{
    /// <summary>
    /// The result of an <see cref="StatusEffectContainer.Apply" /> call.
    /// </summary>
    internal enum StatusEffectApplyResult
    {
        New,
        Refreshed,
        Stacked,
        Overridden
    }

    /// <summary>
    /// Holds active status effect instances for a single entity.
    /// </summary>
    internal class StatusEffectContainer
    {
        private readonly long _entityId;
        private readonly WeakReference<Entity> _entityRef;
        private readonly List<StatusEffectInstance> _instances = new();
        private readonly Func<long> _nextId;

        /// <summary>Performs the status effect container operation.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="nextId">The next id value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity" /> is <see langword="null" />.</exception>
        public StatusEffectContainer(Entity entity, Func<long> nextId)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _entityId = entity.EntityId;
            _entityRef = new WeakReference<Entity>(entity);
            _nextId = nextId ?? throw new ArgumentNullException(nameof(nextId));
        }

        /// <summary>
        /// The entity id this container belongs to.
        /// </summary>
        public long EntityId => _entityId;

        /// <summary>
        /// The target entity, or null if the entity has been garbage-collected.
        /// </summary>
        public Entity? Entity => _entityRef.TryGetTarget(out var e) ? e : null;

        /// <summary>True when no status effects are currently active on the entity.</summary>
        public bool IsEmpty => _instances.Count == 0;

        /// <summary>Read-only view of the active status effect instances.</summary>
        public IReadOnlyList<StatusEffectInstance> Instances => _instances;

        /// <summary>
        /// Applies an effect to the entity according to the effect's stack mode.
        /// </summary>
        /// <param name="effect">The effect value.</param>
        /// <param name="durationMs">The duration ms value.</param>
        /// <param name="data">The associated data.</param>
        /// <returns>The apply.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="effect" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a argument out of range occurs.</exception>
        public (StatusEffectInstance? instance, StatusEffectApplyResult result, StatusEffectInstance? oldInstance) Apply(
            IStatusEffect effect, float durationMs, object? data)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            if (effect.StackMode == EnumStackMode.Independent)
            {
                var instance = CreateInstance(effect, durationMs, data);
                _instances.Add(instance);
                return (instance, StatusEffectApplyResult.New, null);
            }

            var existing = _instances.FirstOrDefault(i => i.Code == effect.Code);
            if (existing == null)
            {
                var instance = CreateInstance(effect, durationMs, data);
                _instances.Add(instance);
                return (instance, StatusEffectApplyResult.New, null);
            }

            switch (effect.StackMode)
            {
                case EnumStackMode.Refresh:
                    existing.RemainingMs = durationMs;
                    existing.StackCount = 1;
                    return (existing, StatusEffectApplyResult.Refreshed, null);

                case EnumStackMode.Stack:
                    if (existing.StackCount < effect.MaxStacks)
                    {
                        existing.StackCount++;
                        existing.RemainingMs = durationMs;
                        return (existing, StatusEffectApplyResult.Stacked, null);
                    }
                    existing.RemainingMs = durationMs;
                    return (existing, StatusEffectApplyResult.Refreshed, null);

                case EnumStackMode.Override:
                    _instances.Remove(existing);
                    var newInstance = CreateInstance(effect, durationMs, data);
                    _instances.Add(newInstance);
                    return (newInstance, StatusEffectApplyResult.Overridden, existing);

                default:
                    throw new ArgumentOutOfRangeException(nameof(effect.StackMode), $"Unhandled stack mode {effect.StackMode}.");
            }
        }

        /// <summary>
        /// Removes all instances with the given effect code.
        /// </summary>
        /// <param name="code">The code value.</param>
        /// <returns>A collection of remove by code values.</returns>
        public IReadOnlyList<StatusEffectInstance> RemoveByCode(string code)
        {
            var removed = _instances.Where(i => i.Code == code).ToList();
            foreach (var instance in removed)
            {
                _instances.Remove(instance);
            }
            return removed;
        }

        /// <summary>
        /// Removes all instances matching the given category.
        /// </summary>
        /// <param name="category">The category value.</param>
        /// <returns>A collection of remove by category values.</returns>
        public IReadOnlyList<StatusEffectInstance> RemoveByCategory(EffectCategory category)
        {
            if (category == EffectCategory.None)
                return Array.Empty<StatusEffectInstance>();

            var removed = _instances.Where(i => i.Effect.Category == category).ToList();
            foreach (var instance in removed)
            {
                _instances.Remove(instance);
            }
            return removed;
        }

        /// <summary>
        /// Removes the instance with the given id.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>The remove by id, or null if none is found.</returns>
        public StatusEffectInstance? RemoveById(long id)
        {
            var index = _instances.FindIndex(i => i.Id == id);
            if (index < 0) return null;

            var instance = _instances[index];
            _instances.RemoveAt(index);
            return instance;
        }

        /// <summary>
        /// Removes all active effects and returns them.
        /// </summary>
        /// <returns>A collection of remove all values.</returns>
        public IReadOnlyList<StatusEffectInstance> RemoveAll()
        {
            var removed = _instances.ToList();
            _instances.Clear();
            return removed;
        }

        /// <summary>
        /// Ticks all instances, returning expired and death-removed instances separately.
        /// </summary>
        /// <param name="dt">The elapsed time in seconds.</param>
        /// <returns>The tick.</returns>
        public StatusEffectTickResult Tick(float dt)
        {
            var expired = new List<StatusEffectInstance>();
            var removedByDeath = new List<StatusEffectInstance>();
            var alive = new List<StatusEffectInstance>();

            var entity = Entity;
            if (entity == null || entity.Alive == false)
            {
                foreach (var instance in _instances.ToList())
                {
                    if (instance.PersistThroughDeath)
                    {
                        alive.Add(instance);
                    }
                    else
                    {
                        removedByDeath.Add(instance);
                    }
                }

                foreach (var instance in removedByDeath)
                {
                    _instances.Remove(instance);
                }

                return new StatusEffectTickResult(expired, removedByDeath, alive);
            }

            foreach (var instance in _instances.ToList())
            {
                instance.RemainingMs -= dt * 1000f;
                alive.Add(instance);

                if (instance.RemainingMs <= 0)
                {
                    expired.Add(instance);
                }
            }

            foreach (var instance in expired)
            {
                _instances.Remove(instance);
                alive.Remove(instance);
            }

            return new StatusEffectTickResult(expired, removedByDeath, alive);
        }

        private StatusEffectInstance CreateInstance(IStatusEffect effect, float durationMs, object? data)
        {
            var id = _nextId();
            return new StatusEffectInstance(id, effect, durationMs, data);
        }
    }

    /// <summary>
    /// Result of a <see cref="StatusEffectContainer.Tick" /> call.
    /// </summary>
    internal readonly struct StatusEffectTickResult
    {
        /// <summary>
        /// Instances that expired during the tick.
        /// </summary>
        public IReadOnlyList<StatusEffectInstance> Expired { get; }

        /// <summary>
        /// Instances removed because the entity died.
        /// </summary>
        public IReadOnlyList<StatusEffectInstance> RemovedByDeath { get; }

        /// <summary>
        /// Instances still active after the tick.
        /// </summary>
        public IReadOnlyList<StatusEffectInstance> Alive { get; }

        /// <summary>Performs the status effect tick result operation.</summary>
        /// <param name="expired">The collection of expired values.</param>
        /// <param name="removedByDeath">The collection of removed by death values.</param>
        /// <param name="alive">The collection of alive values.</param>
        public StatusEffectTickResult(IReadOnlyList<StatusEffectInstance> expired, IReadOnlyList<StatusEffectInstance> removedByDeath, IReadOnlyList<StatusEffectInstance> alive)
        {
            Expired = expired;
            RemovedByDeath = removedByDeath;
            Alive = alive;
        }
    }
}
