using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects
{
    /// <summary>
    /// The result of an <see cref="StatusEffectContainer.Apply"/> call.
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
        private readonly Entity _entity;
        private readonly List<StatusEffectInstance> _instances = new();

        public StatusEffectContainer(Entity entity)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        public Entity Entity => _entity;

        public bool IsEmpty => _instances.Count == 0;

        public IReadOnlyList<StatusEffectInstance> Instances => _instances;

        /// <summary>
        /// Applies an effect to the entity according to the effect's stack mode.
        /// </summary>
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
        /// Removes the instance with the given id.
        /// </summary>
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
        public IReadOnlyList<StatusEffectInstance> RemoveAll()
        {
            var removed = _instances.ToList();
            _instances.Clear();
            return removed;
        }

        /// <summary>
        /// Ticks all instances, returning expired and death-removed instances separately.
        /// </summary>
        public StatusEffectTickResult Tick(float dt)
        {
            var expired = new List<StatusEffectInstance>();
            var removedByDeath = new List<StatusEffectInstance>();
            var alive = new List<StatusEffectInstance>();

            if (_entity == null || _entity.Alive == false)
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

        private static StatusEffectInstance CreateInstance(IStatusEffect effect, float durationMs, object? data)
        {
            var id = System.Threading.Interlocked.Increment(ref StatusEffectManager.NextInstanceId);
            return new StatusEffectInstance(id, effect, durationMs, data);
        }
    }

    /// <summary>
    /// Result of a <see cref="StatusEffectContainer.Tick"/> call.
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

        public StatusEffectTickResult(IReadOnlyList<StatusEffectInstance> expired, IReadOnlyList<StatusEffectInstance> removedByDeath, IReadOnlyList<StatusEffectInstance> alive)
        {
            Expired = expired;
            RemovedByDeath = removedByDeath;
            Alive = alive;
        }
    }
}
