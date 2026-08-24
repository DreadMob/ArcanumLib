namespace ArcanumLib.Common
{
    /// <summary>
    /// Helpers for formatting chat and HUD text with Vintage Story font color tags.
    /// </summary>
    public static class ChatFormatUtil
    {
        /// <summary>
        /// Wraps <paramref name="text" /> in a <c>&lt;font color="..."&gt;</c> tag.
        /// Returns empty string for null/whitespace text, and the original text if no color is supplied.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <param name="hexColor">The hex color value.</param>
        /// <returns>The font string, or null if none is found.</returns>
        public static string Font(string text, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (string.IsNullOrWhiteSpace(hexColor)) return text;

            return $"<font color=\"{hexColor}\">{text}</font>";
        }

        /// <summary>
        /// Builds an alert-prefixed message with default styling: red <c>[!]</c> prefix and white text.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <returns>The prefix alert string, or null if none is found.</returns>
        public static string PrefixAlert(string text)
        {
            return PrefixAlert(text, "[!] ", "#ff5555", "#ffffff");
        }

        /// <summary>
        /// Builds an alert-prefixed message with custom colors and the default <c>[!] </c> prefix.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <param name="prefixColor">The prefix color value.</param>
        /// <param name="textColor">The text color value.</param>
        /// <returns>The prefix alert string, or null if none is found.</returns>
        public static string PrefixAlert(string text, string prefixColor, string textColor)
        {
            return PrefixAlert(text, "[!] ", prefixColor, textColor);
        }

        /// <summary>
        /// Builds an alert-prefixed message with a custom prefix string and colors.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <param name="prefix">The prefix value.</param>
        /// <param name="prefixColor">The prefix color value.</param>
        /// <param name="textColor">The text color value.</param>
        /// <returns>The prefix alert string, or null if none is found.</returns>
        public static string PrefixAlert(string text, string prefix, string prefixColor, string textColor)
        {
            return $"{Font(prefix, prefixColor)}{Font(text, textColor)}";
        }
    }
}
