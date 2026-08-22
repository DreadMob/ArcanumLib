namespace ArcanumLib.Items
{
    /// <summary>
    /// Configuration for the generic <see cref="ItemModeManager"/>.
    /// Consumers set their own attribute keys and optional resolvers.
    /// </summary>
    public class ItemModeConfig
    {
        /// <summary>
        /// Attribute key that stores the serialized list of <see cref="ItemMode"/> entries.
        /// </summary>
        public string ModesAttributeKey { get; set; } = "arcanumlib:modes";

        /// <summary>
        /// Attribute key that stores the active mode index.
        /// </summary>
        public string ModeIndexAttributeKey { get; set; } = "arcanumlib:mode";

        /// <summary>
        /// Number of tool-mode icons to show per line before a line break.
        /// </summary>
        public int ModesPerLine { get; set; } = 7;

        /// <summary>
        /// Optional logger for non-fatal warnings.
        /// </summary>
        public Vintagestory.API.Common.ILogger? Logger { get; set; }

        /// <summary>
        /// Optional name resolver. Receives the raw <see cref="ItemMode.Name"/> and returns the display text.
        /// </summary>
        public System.Func<string, string?>? NameResolver { get; set; }
    }
}
