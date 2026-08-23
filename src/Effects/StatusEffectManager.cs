using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects
{
    /// <summary>
    /// Static manager for applying, ticking, and removing status effects on entities.
    /// </summary>
    public static class StatusEffectManager
    {
        private static readonly ConcurrentDictionary<long, StatusEffectContainer> _containers = new();
        internal static long NextInstanceId = 0;

        /// <summary>
        /// Raised when a new effect instance is created.
        /// </summary>
        public static event Action<Entity, IStatusEffectInstance>? OnEffectApplied;

        /// <summary>
        /// Raised when an existing effect's duration is refreshed.
        /// </summary>
        public static event Action<Entity, IStatusEffectInstance>? OnEffectRefreshed;

        /// <summary>
        /// Raised when an effect's stack count increases.
        /// </summary>
        public static event Action<Entity, IStatusEffectInstance>? OnEffectStacked;

        /// <summary>
        /// Raised when an effect is removed because it expired.
        /// </summary>
        public static event Action<Entity, IStatusEffectInstance>? OnEffectExpired;

        /// <summary>
        /// Raised when an effect is removed manually or because the entity died.
        /// </summary>
        public static event Action<Entity, IStatusEffectInstance>? OnEffectRemoved;

        /// <summary>
        /// Applies an effect to an entity.
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="effect">The effect to apply.</param>
        /// <param name="durationMs">Duration in milliseconds.</param>
        /// <param name="data">Optional payload.</param>
        /// <returns>The active or updated effect instance, or null if the entity was null.</returns>
        public static IStatusEffectInstance? Apply(Entity? entity, IStatusEffect effect, float durationMs, object? data = null)
        {
            if (entity == null) return null;

            var container = _containers.GetOrAdd(entity.EntityId, _ => new StatusEffectContainer(entity));
            StatusEffectInstance? instance;
            StatusEffectInstance? oldInstance;
            StatusEffectApplyResult result;

            lock (container)
            {
                (instance, result, oldInstance) = container.Apply(effect, durationMs, data);
            }

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

        /// <summary>
        /// Removes all effects with the given code from the entity.
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="effectCode">The effect code to remove.</param>
        /// <returns>True if any effect was removed.</returns>
        public static bool Remove(Entity? entity, string effectCode)
        {
            if (entity == null) return false;

            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            IReadOnlyList<StatusEffectInstance> removed;
            lock (container)
            {
                removed = container.RemoveByCode(effectCode);
            }

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

        /// <summary>
        /// Removes a specific effect instance from the entity.
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="instanceId">The unique instance id.</param>
        /// <returns>True if the instance was removed.</returns>
        public static bool Remove(Entity? entity, long instanceId)
        {
            if (entity == null) return false;

            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            StatusEffectInstance? instance;
            lock (container)
            {
                instance = container.RemoveById(instanceId);
            }

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

        /// <summary>
        /// Removes all active effects from the entity.
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <returns>True if any effect was removed.</returns>
        public static bool RemoveAll(Entity? entity)
        {
            if (entity == null) return false;

            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            IReadOnlyList<StatusEffectInstance> removed;
            lock (container)
            {
                removed = container.RemoveAll();
            }

            foreach (var instance in removed)
            {
                SafeRemove(entity, instance, expired: false);
            }

            _containers.TryRemove(entity.EntityId, out _);
            return removed.Count > 0;
        }

        /// <summary>
        /// Returns true if the entity has an active effect with the given code.
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="effectCode">The effect code.</param>
        /// <returns>True if at least one matching effect is active.</returns>
        public static bool Has(Entity? entity, string effectCode)
        {
            if (entity == null) return false;

            if (!_containers.TryGetValue(entity.EntityId, out var container)) return false;

            lock (container)
            {
                return container.Instances.Any(i => i.Code == effectCode);
            }
        }

        /// <summary>
        /// Returns all active effect instances on the entity.
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <returns>A read-only view of the active instances, or an empty collection if none.</returns>
        public static IReadOnlyCollection<IStatusEffectInstance> GetActive(Entity? entity)
        {
            if (entity == null) return Array.Empty<IStatusEffectInstance>();

            if (!_containers.TryGetValue(entity.EntityId, out var container)) return Array.Empty<IStatusEffectInstance>();

            lock (container)
            {
                return container.Instances.ToArray();
            }
        }

        /// <summary>
        /// Ticks all active effects by the given elapsed time.
        /// </summary>
        /// <param name="dt">Seconds since the last tick.</param>
        public static void Tick(float dt)
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

                StatusEffectTickResult result;
                lock (container)
                {
                    result = container.Tick(dt);
                }

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

        /// <summary>
        /// Clears the manager. Intended for tests and shutdown paths.
        /// </summary>
        public static void Clear()
        {
            _containers.Clear();
            NextInstanceId = 0;
        }

        private static void SafeApply(Entity entity, StatusEffectInstance instance)
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

        private static void SafeRemove(Entity entity, StatusEffectInstance instance, bool expired)
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

        private static void SafeTick(Entity entity, StatusEffectInstance instance, float dt)
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
}
