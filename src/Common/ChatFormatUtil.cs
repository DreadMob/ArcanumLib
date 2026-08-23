namespace ArcanumLib.Common
{
    /// <summary>
    /// Helpers for formatting chat and HUD text with Vintage Story font color tags.
    /// </summary>
    public static class ChatFormatUtil
    {
        /// <summary>
        /// Wraps <paramref name="text"/> in a <c>&lt;font color="..."&gt;</c> tag.
        /// Returns empty string for null/whitespace text, and the original text if no color is supplied.
        /// </summary>
        public static string Font(string text, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (string.IsNullOrWhiteSpace(hexColor)) return text;

            return $"<font color=\"{hexColor}\">{text}</font>";
        }

        /// <summary>
        /// Builds an alert-prefixed message with default styling: red <c>[!]</c> prefix and white text.
        /// </summary>
        public static string PrefixAlert(string text)
        {
            return PrefixAlert(text, "[!] ", "#ff5555", "#ffffff");
        }

        /// <summary>
        /// Builds an alert-prefixed message with custom colors and the default <c>[!] </c> prefix.
        /// </summary>
        public static string PrefixAlert(string text, string prefixColor, string textColor)
        {
            return PrefixAlert(text, "[!] ", prefixColor, textColor);
        }

        /// <summary>
        /// Builds an alert-prefixed message with a custom prefix string and colors.
        /// </summary>
        public static string PrefixAlert(string text, string prefix, string prefixColor, string textColor)
        {
            return $"{Font(prefix, prefixColor)}{Font(text, textColor)}";
        }
    }
}
