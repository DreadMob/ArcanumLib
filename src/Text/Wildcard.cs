using System;

namespace ArcanumLib.Text;

/// <summary>
/// Case-insensitive wildcard matching for asset codes and similar identifiers.
/// <c>*</c> matches any sequence (including empty), <c>?</c> matches any single character.
/// </summary>
public static class Wildcard
{
    /// <summary>
    /// Returns true when <paramref name="input"/> matches <paramref name="pattern"/>.
    /// </summary>
    public static bool Match(string? input, string? pattern)
    {
        if (input == null || pattern == null) return false;

        int i = 0, p = 0, starIndex = -1, match = 0;

        while (i < input.Length)
        {
            if (p < pattern.Length &&
                (pattern[p] == '*' ||
                 char.ToLowerInvariant(input[i]) == char.ToLowerInvariant(pattern[p]) ||
                 pattern[p] == '?'))
            {
                if (pattern[p] == '*')
                {
                    starIndex = p;
                    match = i;
                    p++;
                    continue;
                }

                i++;
                p++;
            }
            else if (starIndex >= 0)
            {
                p = starIndex + 1;
                match++;
                i = match;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
            p++;

        return p == pattern.Length;
    }

    /// <summary>
    /// Quick pre-check: returns true when <paramref name="pattern"/> is exactly
    /// <c>prefix*</c> (a single star at the end). Useful for fast-path registry scans.
    /// </summary>
    public static bool IsSimplePrefix(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;

        int first = pattern.IndexOf('*');
        int last = pattern.LastIndexOf('*');

        return first == last && last == pattern.Length - 1;
    }
}
