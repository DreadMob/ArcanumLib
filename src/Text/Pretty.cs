using System;
using System.Linq;

namespace ArcanumLib.Text;

/// <summary>
/// Converts raw asset codes and identifiers into human-readable strings.
/// Strips domains, wildcards, and separator characters, then title-cases the
/// remaining tokens. Useful for fallbacks when a collectible has no lang entry.
/// </summary>
public static class Pretty
{
    /// <summary>
    /// Removes line breaks, collapses multiple spaces, and trims a display string.
    /// Also strips common VTML/JSON newline markers.
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        value = value.Replace("\r", " ")
                     .Replace("\n", " ")
                     .Replace("<br>", " ")
                     .Replace("\\n", " ");

        while (value.Contains("  "))
            value = value.Replace("  ", " ");

        return value.Trim();
    }

    /// <summary>
    /// Converts a raw token (e.g. "metalbit-uranium" or "hollow-trials") into a
    /// readable, title-cased string ("Metalbit Uranium", "Hollow Trials").
    /// Underscores and colons are treated as separators.
    /// </summary>
    public static string Readable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        string[] parts = value.Replace('_', '-').Replace(':', '-').Split('-')
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        if (parts.Length == 0) return value;

        return string.Join(" ", parts.Select(Capitalize));
    }

    /// <summary>
    /// Returns the last <c>:</c>-separated segment of a code, pretty-printed.
    /// Example: <c>"albase:encounter:hollowtrials"</c> → <c>"Hollowtrials"</c>.
    /// </summary>
    public static string LastSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        int last = value.LastIndexOf(':');
        if (last >= 0) value = value.Substring(last + 1);

        return Readable(value);
    }

    /// <summary>
    /// Produces a readable fallback from an asset code, stripping the domain,
    /// trailing dashes and wildcard markers.
    /// Example: <c>"game:flower-*"</c> → <c>"Flower"</c>.
    /// </summary>
    public static string TargetCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";

        string stripped = code;
        int colon = stripped.IndexOf(':');
        if (colon >= 0) stripped = stripped.Substring(colon + 1);

        // Remove wildcard markers anywhere in the remaining path, then collapse dashes.
        stripped = stripped.Replace("-*-", "-")
                           .Replace("*-", "-")
                           .Replace("-*", "")
                           .Replace("*", "")
                           .TrimEnd('-', '*')
                           .TrimStart('-');

        while (stripped.Contains("--"))
            stripped = stripped.Replace("--", "-");

        if (string.IsNullOrWhiteSpace(stripped))
            stripped = code.TrimEnd('-', '*');

        return Readable(stripped);
    }

    private static string Capitalize(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        if (word.Length == 1) return word.ToUpperInvariant();

        return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
    }
}
