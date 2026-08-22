using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Theme;

/// <summary>
/// Preset <see cref="CairoFont"/> configurations for the Arcanum GUI toolkit.
/// </summary>
public static class ArcanumFont
{
    /// <summary>
    /// Large bold title text.
    /// </summary>
    public static CairoFont Title =>
        Colored(ArcanumGuiTheme.TextPrimary, 18.0, FontWeight.Bold);

    /// <summary>
    /// Medium bold header text.
    /// </summary>
    public static CairoFont Header =>
        Colored(ArcanumGuiTheme.TextPrimary, 15.0, FontWeight.Bold);

    /// <summary>
    /// Standard body text.
    /// </summary>
    public static CairoFont Body =>
        Colored(ArcanumGuiTheme.TextSecondary, 14.0, FontWeight.Normal);

    /// <summary>
    /// Bold body text, for labels and emphasis.
    /// </summary>
    public static CairoFont BodyBold =>
        Colored(ArcanumGuiTheme.TextSecondary, 14.0, FontWeight.Bold);

    /// <summary>
    /// Muted caption or helper text.
    /// </summary>
    public static CairoFont Caption =>
        Colored(ArcanumGuiTheme.TextMuted, 12.0, FontWeight.Normal);

    /// <summary>
    /// Create a CairoFont with the given color and size.
    /// </summary>
    public static CairoFont Colored(RGBA color, double size, FontWeight weight = FontWeight.Normal)
    {
        return CairoFont.WhiteSmallishText()
            .WithColor(new[] { color.R, color.G, color.B, color.A })
            .WithFontSize((float)size)
            .WithWeight(weight);
    }

    /// <summary>
    /// Configure an existing font with an Arcanum color.
    /// </summary>
    public static CairoFont WithArcanumColor(this CairoFont font, RGBA color)
    {
        return font.WithColor(new[] { color.R, color.G, color.B, color.A });
    }
}
