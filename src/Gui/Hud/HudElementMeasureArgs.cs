using Cairo;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Arguments passed to <see cref="IHudElementRenderer.MeasureHeight" /> and
/// <see cref="IHudElementRenderer.MeasureMinWidth" />. Provides the Cairo context
/// for text measuring, the element definition, the snapshot, the theme, and layout spacing.
/// All values are unscaled — renderers apply <c>GuiElement.scaled()</c> during draw, not measure.
/// </summary>
public sealed class HudElementMeasureArgs
{
    /// <summary>Cairo context for text measuring (1x1 scratch surface).</summary>
    public Context Context { get; init; } = null!;

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

    /// <summary>Default icon size (unscaled).</summary>
    public double IconSize { get; init; }

    /// <summary>Gap between icon and text (unscaled).</summary>
    public double IconGap { get; init; }
}
