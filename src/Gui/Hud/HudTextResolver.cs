using System;
using System.Linq;
using Vintagestory.API.Config;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Resolves localizable strings for HUD labels. Supports composite strings split by '—',
/// plain localization keys containing ':', and optional mob-code markers.
/// </summary>
public static class HudTextResolver
{
    /// <summary>
    /// Resolves a HUD label on the client. Single keys are localized; composite strings
    /// split by '—' resolve each side. Mob-code markers ("mob:code") are passed to
    /// <paramref name="mobNameResolver"/> when provided.
    /// </summary>
    /// <param name="text">The raw text, localization key, or mob-code marker.</param>
    /// <param name="mobNameResolver">Optional resolver for "mob:" markers.</param>
    /// <returns>The localized or resolved string, or <paramref name="text"/> if no localization matched.</returns>
    public static string Resolve(string text, Func<string, string?>? mobNameResolver = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        if (text.StartsWith("mob:", StringComparison.OrdinalIgnoreCase))
        {
            string mobCode = text.Substring(4);
            if (!string.IsNullOrWhiteSpace(mobCode))
            {
                string? localized = mobNameResolver?.Invoke(mobCode);
                if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, mobCode, StringComparison.OrdinalIgnoreCase))
                    return localized;
            }
            return text;
        }

        if (text.Contains('—'))
        {
            var parts = text.Split('—');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                parts[i] = part.Contains(':') ? ResolveSingle(part) : part;
            }
            return string.Join(" — ", parts);
        }

        if (text.Contains(':'))
            return ResolveSingle(text);

        return text;
    }

    private static string ResolveSingle(string key)
    {
        string? value = Lang.Get(key);
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.OrdinalIgnoreCase))
            return key;
        return value;
    }
}
