namespace ArcanumLib.Effects
{
    /// <summary>
    /// Classification of a status effect for dispel and resistance logic.
    /// </summary>
    public enum EffectCategory
    {
        /// <summary>
        /// Default. Not classified as buff or debuff.
        /// </summary>
        None = 0,

        /// <summary>
        /// Beneficial effect (buff).
        /// </summary>
        Buff = 1,

        /// <summary>
        /// Harmful effect (debuff).
        /// </summary>
        Debuff = 2
    }
}
