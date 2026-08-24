namespace ArcanumLib.Effects
{
    /// <summary>
    /// Defines how repeated applications of the same effect interact.
    /// </summary>
    public enum EnumStackMode
    {
        /// <summary>
        /// Resets the remaining duration of the existing effect.
        /// </summary>
        Refresh,

        /// <summary>
        /// Increases the stack count up to <see cref="IStatusEffect.MaxStacks" />, then refreshes.
        /// </summary>
        Stack,

        /// <summary>
        /// Removes the old effect and applies a new one.
        /// </summary>
        Override,

        /// <summary>
        /// Always creates a new, independent instance.
        /// </summary>
        Independent
    }
}
