using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects
{
    /// <summary>
    /// Defines how a status effect behaves when applied, ticked, and removed.
    /// </summary>
    public interface IStatusEffect
    {
        /// <summary>
        /// Unique code for this effect, used for stacking/refreshing.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// How subsequent applications of the same effect interact with an active instance.
        /// </summary>
        EnumStackMode StackMode { get; }

        /// <summary>
        /// Maximum number of stacks when <see cref="StackMode"/> is <see cref="EnumStackMode.Stack"/>.
        /// </summary>
        int MaxStacks { get; }

        /// <summary>
        /// Whether the effect should remain on the entity after death.
        /// </summary>
        bool PersistThroughDeath { get; }

        /// <summary>
        /// Whether <see cref="OnTick"/> does meaningful work. When false, the manager
        /// skips per-tick calls for this effect to avoid unnecessary overhead.
        /// Default implementations should return false when <see cref="OnTick"/> is empty.
        /// Defaults to <c>true</c> for backwards compatibility with effects that do not
        /// declare this property explicitly.
        /// </summary>
        bool HasTick => true;

        /// <summary>
        /// Called once when the effect is applied or re-stacked.
        /// </summary>
        /// <param name="entity">The affected entity.</param>
        /// <param name="instance">The effect instance.</param>
        void OnApply(Entity entity, IStatusEffectInstance instance);

        /// <summary>
        /// Called once when the effect is removed or expires.
        /// </summary>
        /// <param name="entity">The affected entity.</param>
        /// <param name="instance">The effect instance.</param>
        void OnRemove(Entity entity, IStatusEffectInstance instance);

        /// <summary>
        /// Called each tick while the effect is active.
        /// </summary>
        /// <param name="entity">The affected entity.</param>
        /// <param name="instance">The effect instance.</param>
        /// <param name="dt">Seconds since the last tick.</param>
        void OnTick(Entity entity, IStatusEffectInstance instance, float dt);
    }
}
