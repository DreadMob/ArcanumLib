namespace ArcanumLib.Progression
{
    /// <summary>
    /// Minimal interface for pity systems (case/gacha guaranteed quality).
    /// </summary>
    public interface IPityProvider
    {
        /// <summary>
        /// Records an open for the given player and definition.
        /// </summary>
        void RecordOpen(string playerUid, string definitionId, int qualityTier);

        /// <summary>
        /// Returns the guaranteed quality tier index, or 0 if none.
        /// </summary>
        int GetGuaranteedQuality(string playerUid, string definitionId);

        /// <summary>
        /// Get pity counters for a player/definition, or null if not tracked.
        /// </summary>
        PityCounters? GetCounters(string playerUid, string definitionId);

        /// <summary>
        /// Try to get the pity definition for a given ID.
        /// </summary>
        bool TryGetDefinition(string definitionId, out PityDefinition? definition);
    }
}
