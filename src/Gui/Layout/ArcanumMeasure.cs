using System;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Layout;

/// <summary>
/// Text measurement helpers for the Arcanum GUI toolkit.
/// Wraps <see cref="TextDrawUtil"/> and low-level Cairo extents.
/// </summary>
public static class ArcanumMeasure
{
    /// <summary>
    /// Returns the line height for the supplied font.
    /// </summary>
    public static double GetLineHeight(ICoreClientAPI capi, CairoFont font)
    {
        return capi.Gui.Text.GetLineHeight(font);
    }

    /// <summary>
    /// Returns the number of lines the text occupies when wrapped to <paramref name="boxWidth"/>.
    /// </summary>
    public static int GetQuantityTextLines(ICoreClientAPI capi, CairoFont font, string text, double boxWidth)
    {
        return capi.Gui.Text.GetQuantityTextLines(font, text, boxWidth);
    }

    /// <summary>
    /// Returns the total height of wrapped text, including any <paramref name="lineHeightMul"/>.
    /// </summary>
    public static double GetTextHeight(ICoreClientAPI capi, CairoFont font, string text, double boxWidth, double lineHeightMul = 1.0)
    {
        int lines = GetQuantityTextLines(capi, font, text, boxWidth);
        double lineHeight = GetLineHeight(capi, font);
        return lines * lineHeight * Math.Max(0.5, lineHeightMul);
    }

    /// <summary>
    /// Returns the width of a single line of text.
    /// </summary>
    public static double GetTextWidth(CairoFont font, string text)
    {
        return font.GetTextExtents(text).Width;
    }

    /// <summary>
    /// Breaks the text into <see cref="TextLine"/>s for a given width.
    /// </summary>
    public static TextLine[] Lineize(ICoreClientAPI capi, CairoFont font, string text, double boxWidth)
    {
        return capi.Gui.Text.Lineize(font, text, boxWidth, EnumLinebreakBehavior.Default);
    }
}
