using System;
using System.Collections.Generic;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Resolves a named <typeparamref name="TTheme"/> by merging a custom override
/// over an optional built-in theme and a fallback base theme.
/// </summary>
public static class HudThemeResolver
{
    /// <summary>
    /// Resolves a theme by name. Custom themes take priority over built-ins;
    /// built-in themes are merged over the <paramref name="baseTheme"/> so that
    /// unspecified fields fall back to the defaults.
    /// </summary>
    /// <param name="name">Theme name to resolve.</param>
    /// <param name="customThemes">Optional dictionary of JSON/custom themes keyed by name.</param>
    /// <param name="builtInFactory">Optional factory that returns a built-in theme for a name.</param>
    /// <param name="baseTheme">Fallback theme used when the name is unknown or no theme matched.</param>
    /// <returns>The resolved theme, or <paramref name="baseTheme"/> if no theme was found.</returns>
    public static TTheme Resolve<TTheme>(
        string name,
        Dictionary<string, TTheme>? customThemes,
        Func<string, TTheme?>? builtInFactory,
        TTheme baseTheme) where TTheme : HudTheme
    {
        if (string.IsNullOrWhiteSpace(name)) return baseTheme;

        TTheme? overlay = null;
        if (customThemes?.TryGetValue(name, out var found) == true)
            overlay = found;
        if (overlay == null)
            overlay = builtInFactory?.Invoke(name);
        if (overlay == null)
            return baseTheme;

        // If the custom theme overrides a built-in name, merge the custom
        // over the built-in first so non-overridden built-in fields survive.
        var builtIn = builtInFactory?.Invoke(name);
        if (builtIn != null && !ReferenceEquals(overlay, builtIn))
            overlay = (TTheme)builtIn.Merge(overlay);

        return (TTheme)baseTheme.Merge(overlay);
    }
}
