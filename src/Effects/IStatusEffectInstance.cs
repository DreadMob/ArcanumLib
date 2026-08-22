namespace ArcanumLib.Effects
{
    /// <summary>
    /// A live status effect instance attached to an entity.
    /// </summary>
    public interface IStatusEffectInstance
    {
        /// <summary>
        /// Unique id of this instance.
        /// </summary>
        long Id { get; }

        /// <summary>
        /// The effect code.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// The effect definition.
        /// </summary>
        IStatusEffect Effect { get; }

        /// <summary>
        /// Total duration in milliseconds.
        /// </summary>
        float DurationMs { get; }

        /// <summary>
        /// Remaining duration in milliseconds.
        /// </summary>
        float RemainingMs { get; }

        /// <summary>
        /// Current stack count.
        /// </summary>
        int StackCount { get; }

        /// <summary>
        /// Optional consumer data payload.
        /// </summary>
        object? Data { get; }

        /// <summary>
        /// Whether the effect persists through death.
        /// </summary>
        bool PersistThroughDeath { get; }
    }
}
