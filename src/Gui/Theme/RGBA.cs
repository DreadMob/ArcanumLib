using System;
using Cairo;

namespace ArcanumLib.Gui.Theme;

/// <summary>
/// Small readable color struct for Cairo rendering. RGB and alpha are normalized to 0..1.
/// </summary>
public readonly struct RGBA
{
    /// <summary>Red channel, normalized to 0..1.</summary>
    public readonly double R, G, B, A;

    /// <summary>Creates a color from normalized 0..1 channel values.</summary>
    /// <param name="r">Red channel, 0..1.</param>
    /// <param name="g">Green channel, 0..1.</param>
    /// <param name="b">Blue channel, 0..1.</param>
    /// <param name="a">Alpha channel, 0..1.</param>
    public RGBA(double r, double g, double b, double a)
    {
        R = r; G = g; B = b; A = a;
    }

    /// <summary>Creates a color from 0..255 RGB components and a 0..1 alpha.</summary>
    /// <param name="r">Red channel, 0..255.</param>
    /// <param name="g">Green channel, 0..255.</param>
    /// <param name="b">Blue channel, 0..255.</param>
    /// <param name="a">Alpha channel, 0..1.</param>
    /// <returns>A normalized <see cref="RGBA" /> instance.</returns>
    public static RGBA From(int r, int g, int b, double a) =>
        new RGBA(r / 255.0, g / 255.0, b / 255.0, a);

    /// <summary>
    /// Parse a hex color string (#RRGGBB or #RGB). Returns null if invalid.
    /// </summary>
    /// <param name="hex">The hex value.</param>
    /// <returns>The parse hex color, or null if none is found.</returns>
    public static RGBA? ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length == 3)
        {
            int r = Convert.ToInt32(hex.Substring(0, 1) + hex.Substring(0, 1), 16);
            int g = Convert.ToInt32(hex.Substring(1, 1) + hex.Substring(1, 1), 16);
            int b = Convert.ToInt32(hex.Substring(2, 1) + hex.Substring(2, 1), 16);
            return new RGBA(r / 255.0, g / 255.0, b / 255.0, 1.0);
        }
        if (hex.Length == 6)
        {
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return new RGBA(r / 255.0, g / 255.0, b / 255.0, 1.0);
        }
        return null;
    }

    /// <summary>
    /// Convert 0xAARRGGBB packed int to RGBA (alpha is fully opaque unless 0).
    /// </summary>
    /// <param name="argb">The argb value.</param>
    /// <returns>The from argb.</returns>
    public static RGBA FromArgb(int argb)
    {
        int a = (argb >> 24) & 0xFF;
        int r = (argb >> 16) & 0xFF;
        int g = (argb >> 8) & 0xFF;
        int b = argb & 0xFF;
        return From(r, g, b, a / 255.0);
    }

    /// <summary>Returns a copy of this color with the specified alpha.</summary>
    /// <param name="a">New alpha channel, 0..1.</param>
    /// <returns>A new <see cref="RGBA" /> with the given alpha.</returns>
    public RGBA WithAlpha(double a) => new RGBA(R, G, B, a);

    /// <summary>Linearly interpolates between this color and <paramref name="other" /> by <paramref name="t" />.</summary>
    /// <param name="other">The target color.</param>
    /// <param name="t">Interpolation factor, clamped to 0..1.</param>
    /// <returns>The interpolated color.</returns>
    public RGBA Lerp(RGBA other, double t)
    {
        t = Math.Max(0.0, Math.Min(1.0, t));
        return new RGBA(
            R + (other.R - R) * t,
            G + (other.G - G) * t,
            B + (other.B - B) * t,
            A + (other.A - A) * t);
    }

    /// <summary>Sets this color as the source on a Cairo <see cref="Context" />.</summary>
    /// <param name="ctx">The Cairo context to configure.</param>
    public void Apply(Context ctx) => ctx.SetSourceRGBA(R, G, B, A);
}
