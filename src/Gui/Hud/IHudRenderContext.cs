using Cairo;
using Vintagestory.API.Client;
using RGBA = ArcanumLib.Gui.Theme.RGBA;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Provides shared rendering helpers to <see cref="IHudElementRenderer" /> implementations.
/// Implemented by the hosting <see cref="HudPanel{TSnapshot, THudDefinition, TTheme}" /> derived type
/// so that renderers can access theme colours, text resolution, icon drawing, and interpolation
/// without a hard reference to the panel class.
/// </summary>
public interface IHudRenderContext
{
    /// <summary>The client API, used for world time and rendering helpers.</summary>
    ICoreClientAPI Api { get; }

    /// <summary>Client time in ms when the last snapshot was received.</summary>
    long SnapshotReceivedMs { get; }

    /// <summary>Whether text should be drawn with a drop shadow.</summary>
    bool TextShadow { get; }

    /// <summary>Resolves a theme colour by key (e.g. "accent", "textPrimary").</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="alpha">The alpha value.</param>
    /// <returns>The theme rgba.</returns>
    RGBA ThemeRGBA(string key, double alpha = 1.0);

    /// <summary>Resolves a theme font size by key (e.g. "timer", "title").</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The theme font.</returns>
    double ThemeFont(string key);

    /// <summary>Resolves the text colour for an element, honouring per-element overrides.</summary>
    /// <param name="el">The el value.</param>
    /// <param name="themeKey">The theme key value.</param>
    /// <returns>The resolve text color.</returns>
    RGBA ResolveTextColor(HudElementDefinition el, string themeKey);

    /// <summary>Draws text with optional shadow based on <see cref="TextShadow" />.</summary>
    /// <param name="ctx">The ctx value.</param>
    /// <param name="text">The text value.</param>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <param name="color">The color value.</param>
    void DrawText(Context ctx, string text, double x, double y, RGBA color);

    /// <summary>Draws a named icon at the given position. Returns true if an icon was drawn.</summary>
    /// <param name="ctx">The ctx value.</param>
    /// <param name="icon">The icon value.</param>
    /// <param name="cx">The cx value.</param>
    /// <param name="cy">The cy value.</param>
    /// <param name="sz">The sz value.</param>
    /// <param name="color">The color value.</param>
    /// <param name="accent">The accent value.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    bool DrawIcon(Context ctx, string icon, double cx, double cy, double sz, RGBA color, RGBA accent);

    /// <summary>Resolves a localizable text string (e.g. mob display name, lang key).</summary>
    /// <param name="text">The text value.</param>
    /// <returns>The resolve text string, or null if none is found.</returns>
    string ResolveText(string text);

    /// <summary>Returns the elapsed time interpolated between the last snapshot and the current client time.</summary>
    /// <returns>The interpolated elapsed ms.</returns>
    long InterpolatedElapsedMs();
}
