using System;

namespace ArcanumLib.Effects
{
    /// <summary>
    /// Default implementation of a status effect instance.
    /// </summary>
    internal sealed class StatusEffectInstance : IStatusEffectInstance
    {
        /// <summary>
        /// Unique id of this instance.
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// The effect code.
        /// </summary>
        public string Code => Effect.Code;

        /// <summary>
        /// The effect definition.
        /// </summary>
        public IStatusEffect Effect { get; }

        /// <summary>
        /// Total duration in milliseconds.
        /// </summary>
        public float DurationMs { get; }

        /// <summary>
        /// Remaining duration in milliseconds.
        /// </summary>
        public float RemainingMs { get; internal set; }

        /// <summary>
        /// Current stack count.
        /// </summary>
        public int StackCount { get; internal set; } = 1;

        /// <summary>
        /// Optional consumer data payload.
        /// </summary>
        public object? Data { get; }

        /// <summary>
        /// Whether the effect persists through death.
        /// </summary>
        public bool PersistThroughDeath => Effect.PersistThroughDeath;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="id">Unique id.</param>
        /// <param name="effect">The effect definition.</param>
        /// <param name="durationMs">Duration in milliseconds.</param>
        /// <param name="data">Optional payload.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="effect" /> is <see langword="null" />.</exception>
        public StatusEffectInstance(long id, IStatusEffect effect, float durationMs, object? data = null)
        {
            Id = id;
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            DurationMs = durationMs;
            RemainingMs = durationMs;
            Data = data;
        }
    }
}
