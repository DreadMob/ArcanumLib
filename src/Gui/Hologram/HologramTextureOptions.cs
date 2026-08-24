using System;
using Cairo;

namespace ArcanumLib.Gui.Hologram;

/// <summary>
/// Configuration for generating a hologram texture from plain text.
/// </summary>
public class HologramTextureOptions
{
    /// <summary>Base font size in pixels.</summary>
    public float FontSize { get; set; } = 24f;

    /// <summary>Texture width in pixels. The generator will expand if any line does not fit.</summary>
    public float LineWidth { get; set; } = 520f;

    /// <summary>Multiplier applied to <see cref="FontSize"/> to determine vertical spacing.</summary>
    public float LineHeightMultiplier { get; set; } = 1.45f;

    /// <summary>Maximum number of lines to render. 0 means unlimited.</summary>
    public int MaxLines { get; set; } = 0;

    /// <summary>Padding above the first line.</summary>
    public double PaddingTop { get; set; } = 10;

    /// <summary>Padding below the last line.</summary>
    public double PaddingBottom { get; set; } = 14;

    /// <summary>Horizontal padding on each side.</summary>
    public double PaddingX { get; set; } = 12;

    /// <summary>Whether to center the text horizontally.</summary>
    public bool Centered { get; set; } = true;

    /// <summary>Background RGBA color (0-1 range). Used when <see cref="DrawBackground"/> is true.</summary>
    public double[]? BackgroundColor { get; set; } = new[] { 0.06, 0.07, 0.10, 0.72 };

    /// <summary>Border RGBA color (0-1 range). Used when <see cref="DrawBackground"/> is true.</summary>
    public double[]? BorderColor { get; set; } = new[] { 0.85, 0.7, 0.25, 0.35 };

    /// <summary>Text RGBA color (0-1 range), or null to keep the context's current source.</summary>
    public double[]? TextColor { get; set; }

    /// <summary>Whether to draw a rounded background and border.</summary>
    public bool DrawBackground { get; set; } = true;

    /// <summary>Shadow RGBA color (0-1 range), or null to skip the shadow.</summary>
    public double[]? ShadowColor { get; set; } = new[] { 0.0, 0.0, 0.0, 0.8 };

    /// <summary>Cairo font face name.</summary>
    public string FontFace { get; set; } = "Sans";

    /// <summary>Cairo font weight.</summary>
    public FontWeight FontWeight { get; set; } = FontWeight.Bold;

    /// <summary>Cairo font slant.</summary>
    public FontSlant FontSlant { get; set; } = FontSlant.Normal;

    /// <summary>
    /// Optional callback for custom line rendering.
    /// Receives the Cairo context, the line index, the trimmed line text, the suggested x and y coordinates,
    /// and the texture width. Return <c>true</c> if the callback has fully handled the line.
    /// </summary>
    public Func<Context, int, string, double, double, double, bool>? RenderLine { get; set; }
}
