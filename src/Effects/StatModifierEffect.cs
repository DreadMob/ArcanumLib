using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Effects
{
    /// <summary>
    /// A reusable status effect that modifies an <see cref="EntityStats" /> category.
    /// </summary>
    public class StatModifierEffect : IStatusEffect
    {
        /// <summary>
        /// Unique code for this effect.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// The <see cref="EntityStats" /> category to modify (e.g. "walkspeed").
        /// </summary>
        public string StatCategory { get; }

        /// <summary>
        /// The raw value added to the stat. Use negative values for reductions.
        /// The final blended value is the base plus the sum of all active modifiers.
        /// </summary>
        public float Value { get; }

        /// <summary>
        /// How repeated applications of the same effect interact.
        /// </summary>
        public EnumStackMode StackMode { get; set; } = EnumStackMode.Refresh;

        /// <summary>
        /// Maximum number of stacks when <see cref="StackMode" /> is <see cref="EnumStackMode.Stack" />.
        /// </summary>
        public int MaxStacks { get; set; } = 1;

        /// <summary>
        /// Whether the effect remains on the entity after death.
        /// </summary>
        public bool PersistThroughDeath { get; set; } = false;

        /// <summary>
        /// Stat modifier effects have no per-tick behavior.
        /// </summary>
        public bool HasTick => false;

        /// <summary>
        /// Creates a new stat modifier effect.
        /// </summary>
        /// <param name="code">Unique effect code.</param>
        /// <param name="statCategory">The stat category to modify.</param>
        /// <param name="value">The raw value to add.</param>
        public StatModifierEffect(string code, string statCategory, float value)
        {
            Code = code;
            StatCategory = statCategory;
            Value = value;
        }

        /// <summary>
        /// Applies the stat change. For stacking, the effective value is <see cref="Value" /> multiplied by the stack count.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="instance">The instance value.</param>
        public void OnApply(Entity entity, IStatusEffectInstance instance)
        {
            if (entity?.Stats == null) return;

            var key = GetStatKey(instance);
            var effectiveValue = StackMode == EnumStackMode.Independent ? Value : Value * instance.StackCount;

            entity.Stats.Set(StatCategory, key, effectiveValue);
        }

        /// <summary>
        /// Removes the stat change.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="instance">The instance value.</param>
        public void OnRemove(Entity entity, IStatusEffectInstance instance)
        {
            if (entity?.Stats == null) return;

            var key = GetStatKey(instance);
            entity.Stats.Remove(StatCategory, key);
        }

        /// <summary>
        /// No per-tick behavior for a simple stat modifier.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="instance">The instance value.</param>
        /// <param name="dt">The elapsed time in seconds.</param>
        public void OnTick(Entity entity, IStatusEffectInstance instance, float dt)
        {
        }

        private string GetStatKey(IStatusEffectInstance instance)
        {
            return StackMode == EnumStackMode.Independent ? $"{Code}:{instance.Id}" : Code;
        }
    }
}
