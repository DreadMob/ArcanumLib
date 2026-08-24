using Cairo;
using Vintagestory.API.Client;
using RGBA = ArcanumLib.Gui.Theme.RGBA;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Provides shared rendering helpers to <see cref="IHudElementRenderer"/> implementations.
/// Implemented by the hosting <see cref="HudPanel{TSnapshot, THudDefinition, TTheme}"/> derived type
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
    RGBA ThemeRGBA(string key, double alpha = 1.0);

    /// <summary>Resolves a theme font size by key (e.g. "timer", "title").</summary>
    double ThemeFont(string key);

    /// <summary>Resolves the text colour for an element, honouring per-element overrides.</summary>
    RGBA ResolveTextColor(HudElementDefinition el, string themeKey);

    /// <summary>Draws text with optional shadow based on <see cref="TextShadow"/>.</summary>
    void DrawText(Context ctx, string text, double x, double y, RGBA color);

    /// <summary>Draws a named icon at the given position. Returns true if an icon was drawn.</summary>
    bool DrawIcon(Context ctx, string icon, double cx, double cy, double sz, RGBA color, RGBA accent);

    /// <summary>Resolves a localizable text string (e.g. mob display name, lang key).</summary>
    string ResolveText(string text);

    /// <summary>Returns the elapsed time interpolated between the last snapshot and the current client time.</summary>
    long InterpolatedElapsedMs();
}
