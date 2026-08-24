using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Arguments passed to an <see cref="IHudElementRenderer.Draw"/> call.
/// Provides the Cairo context, the element definition, the snapshot, the theme
/// and the bounding rectangle to draw into.
/// </summary>
public sealed class HudElementRenderArgs
{
    /// <summary>Cairo context to draw on.</summary>
    public Context Context { get; init; } = null!;

    /// <summary>The client API, used for text measuring and texture helpers.</summary>
    public ICoreClientAPI ClientApi { get; init; } = null!;

    /// <summary>Element definition for this row.</summary>
    public HudElementDefinition Element { get; init; } = null!;

    /// <summary>Current HUD snapshot.</summary>
    public IHudSnapshot Snapshot { get; init; } = null!;

    /// <summary>Resolved theme, including colours, fonts and layout.</summary>
    public HudTheme Theme { get; init; } = null!;

    /// <summary>Resolved layout spacing.</summary>
    public HudThemeLayout Layout { get; init; } = null!;

    /// <summary>The mod-specific HUD definition (e.g. encounter definition). Cast in renderers.</summary>
    public object? Definition { get; init; }

    /// <summary>Shared rendering context providing theme colours, text helpers, and interpolation.</summary>
    public IHudRenderContext RenderContext { get; init; } = null!;

    /// <summary>Left X coordinate inside the panel.</summary>
    public double X { get; init; }

    /// <summary>Top Y coordinate inside the panel.</summary>
    public double Y { get; init; }

    /// <summary>Available width for this element.</summary>
    public double W { get; init; }

    /// <summary>Inner padding (already scaled).</summary>
    public double Pad { get; init; }

    /// <summary>Default icon size (already scaled).</summary>
    public double IconSize { get; init; }

    /// <summary>Gap between icon and text (already scaled).</summary>
    public double IconGap { get; init; }
}

