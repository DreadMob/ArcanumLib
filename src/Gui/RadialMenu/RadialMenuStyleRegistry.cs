using System;
using System.Collections.Generic;

namespace ArcanumLib.Gui.RadialMenu;

/// <summary>
/// Static registry for <see cref="IRadialMenuStyle" /> implementations.
/// Consumers register custom styles by string key; the radial menu looks them
/// up at draw time. A built-in <c>"default"</c> style is always available.
/// </summary>
public static class RadialMenuStyleRegistry
{
    private static readonly Dictionary<string, IRadialMenuStyle> _styles =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly DefaultRadialMenuStyle _fallback = new();

    /// <summary>
    /// Registers a style under its <see cref="IRadialMenuStyle.Key" />.
    /// Overwrites any existing style with the same key.
    /// </summary>
    /// <param name="style">The style value.</param>
    public static void Register(IRadialMenuStyle style)
    {
        if (style == null) return;
        _styles[style.Key] = style;
    }

    /// <summary>
    /// Removes a style by key. The <c>"default"</c> style cannot be removed.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public static bool Unregister(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Equals("default", StringComparison.OrdinalIgnoreCase))
            return false;
        return _styles.Remove(key);
    }

    /// <summary>
    /// Retrieves a style by key. Returns the fallback <c>"default"</c> style
    /// if the key is not found.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The or default.</returns>
    public static IRadialMenuStyle GetOrDefault(string? key)
    {
        if (!string.IsNullOrEmpty(key) && _styles.TryGetValue(key, out var style))
            return style;
        return _fallback;
    }

    /// <summary>Checks whether a style with the given key is registered.</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>true if registered; otherwise, false.</returns>
    public static bool IsRegistered(string key)
        => !string.IsNullOrEmpty(key) && _styles.ContainsKey(key);
}
