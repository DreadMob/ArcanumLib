namespace ArcanumLib.Persistence
{
    /// <summary>
    /// Simple versioned envelope used to store data in the savegame.
    /// </summary>
    internal class ModDataStoreEnvelope
    {
        /// <summary>
        /// The schema version of the payload.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// The JSON-serialized payload.
        /// </summary>
        public string Payload { get; set; } = string.Empty;
    }
}
