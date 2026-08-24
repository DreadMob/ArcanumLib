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
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="definitionId">The definition id value.</param>
        /// <param name="qualityTier">The quality tier value.</param>
        void RecordOpen(string playerUid, string definitionId, int qualityTier);

        /// <summary>
        /// Returns the guaranteed quality tier index, or 0 if none.
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="definitionId">The definition id value.</param>
        /// <returns>The guaranteed quality.</returns>
        int GetGuaranteedQuality(string playerUid, string definitionId);

        /// <summary>
        /// Get pity counters for a player/definition, or null if not tracked.
        /// </summary>
        /// <param name="playerUid">The unique player identifier.</param>
        /// <param name="definitionId">The definition id value.</param>
        /// <returns>The counters, or null if none is found.</returns>
        PityCounters? GetCounters(string playerUid, string definitionId);

        /// <summary>
        /// Try to get the pity definition for a given ID.
        /// </summary>
        /// <param name="definitionId">The definition id value.</param>
        /// <param name="definition">When this method returns, contains the <paramref name="definition" /> value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        bool TryGetDefinition(string definitionId, out PityDefinition? definition);
    }
}
