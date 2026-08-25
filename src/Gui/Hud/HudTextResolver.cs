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
    /// <paramref name="mobNameResolver" /> when provided.
    /// </summary>
    /// <param name="text">The raw text, localization key, or mob-code marker.</param>
    /// <param name="mobNameResolver">Optional resolver for "mob:" markers.</param>
    /// <param name="customResolver">Optional resolver called before <see cref="Lang.Get" /> for keys containing a ':'.</param>
    /// <returns>The localized or resolved string, or <paramref name="text" /> if no localization matched. Returns <see cref="string.Empty" /> when <paramref name="text" /> is null or whitespace.</returns>
    public static string Resolve(string text, Func<string, string?>? mobNameResolver = null, Func<string, string?>? customResolver = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;

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
                parts[i] = part.Contains(':') ? ResolveSingle(part, null) : part;
            }
            return string.Join(" — ", parts);
        }

        if (text.Contains(':'))
            return ResolveSingle(text, customResolver);

        return text;
    }

    private static string ResolveSingle(string key, Func<string, string?>? customResolver)
    {
        if (customResolver != null)
        {
            try
            {
                string? custom = customResolver(key);
                if (!string.IsNullOrWhiteSpace(custom) && !string.Equals(custom, key, StringComparison.OrdinalIgnoreCase))
                    return custom;
            }
            catch (Exception ex)
            {
                // Resolver failure — continue to vanilla fallback.
                System.Diagnostics.Debug.WriteLine($"[ArcanumLib] HudTextResolver custom resolver failed for '{key}': {ex.Message}");
            }
        }

        string? value = Lang.Get(key);
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.OrdinalIgnoreCase))
            return key;
        return value;
    }
}
