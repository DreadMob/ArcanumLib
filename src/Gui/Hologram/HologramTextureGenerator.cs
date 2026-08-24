using System;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Hologram;

/// <summary>
/// Generates a <see cref="HologramTexture"/> from a string using Cairo.
/// </summary>
public static class HologramTextureGenerator
{
    /// <summary>
    /// Creates a Cairo-backed texture for the given text and options.
    /// </summary>
    /// <param name="capi">Client API for uploading the texture.</param>
    /// <param name="text">The multi-line text to render.</param>
    /// <param name="options">Texture generation options.</param>
    /// <param name="version">Version to store on the returned texture.</param>
    /// <returns>A hologram texture. The underlying <see cref="LoadedTexture"/> may be null if the text is empty.</returns>
    public static HologramTexture Generate(ICoreClientAPI capi, string? text, HologramTextureOptions options, long version)
    {
        if (capi == null) throw new ArgumentNullException(nameof(capi));
        if (options == null) throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(text))
            return new HologramTexture();

        var rawLines = text.Split('\n');
        string[] lines = options.MaxLines > 0 && rawLines.Length > options.MaxLines
            ? rawLines[..options.MaxLines]
            : rawLines;

        int width = (int)options.LineWidth;
        double lineHeight = options.FontSize * options.LineHeightMultiplier;
        double height = options.PaddingTop + options.FontSize + (lines.Length - 1) * lineHeight + options.PaddingBottom;

        // Measure each line so the texture is wide enough.
        using var tmpSurface = new ImageSurface(Format.Argb32, 1, 1);
        using var tmpCtx = new Context(tmpSurface);
        tmpCtx.SelectFontFace(options.FontFace, options.FontSlant, options.FontWeight);
        tmpCtx.SetFontSize(options.FontSize);
        foreach (var raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var ext = tmpCtx.TextExtents(line);
            double needed = ext.Width + options.PaddingX * 2;
            if (needed > width)
                width = (int)Math.Ceiling(needed);
        }

        var surface = new ImageSurface(Format.Argb32, width, (int)Math.Ceiling(height));
        var ctx = new Context(surface);

        if (options.DrawBackground)
            DrawBackground(ctx, width, height, options);

        ctx.SelectFontFace(options.FontFace, options.FontSlant, options.FontWeight);
        ctx.SetFontSize(options.FontSize);

        double y = options.PaddingTop + options.FontSize;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                y += lineHeight * 0.5;
                continue;
            }

            bool handled = options.RenderLine?.Invoke(ctx, i, line, 0, y, width) ?? false;
            if (!handled)
            {
                if (options.TextColor is { Length: >= 4 })
                    ctx.SetSourceRGBA(options.TextColor[0], options.TextColor[1], options.TextColor[2], options.TextColor[3]);
                else
                    ctx.SetSourceRGBA(0.95, 0.95, 0.95, 1.0);

                var ext = ctx.TextExtents(line);
                double x = options.Centered ? (width - ext.Width) / 2.0 : options.PaddingX;

                if (options.ShadowColor is { Length: >= 4 })
                {
                    ctx.SetSourceRGBA(options.ShadowColor[0], options.ShadowColor[1], options.ShadowColor[2], options.ShadowColor[3]);
                    ctx.MoveTo(x + 1, y + 1);
                    ctx.ShowText(line);
                }

                if (options.TextColor is { Length: >= 4 })
                    ctx.SetSourceRGBA(options.TextColor[0], options.TextColor[1], options.TextColor[2], options.TextColor[3]);
                else
                    ctx.SetSourceRGBA(0.95, 0.95, 0.95, 1.0);
                ctx.MoveTo(x, y);
                ctx.ShowText(line);
            }

            y += lineHeight;
        }

        var texture = new LoadedTexture(capi);
        capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref texture);

        ctx.Dispose();
        surface.Dispose();

        return new HologramTexture { Texture = texture, Version = version };
    }

    private static void DrawBackground(Context ctx, int width, double height, HologramTextureOptions options)
    {
        double[] bg = options.BackgroundColor ?? new[] { 0.0, 0.0, 0.0, 0.6 };
        double[] border = options.BorderColor ?? new[] { 1.0, 1.0, 1.0, 0.3 };
        double radius = Math.Min(10.0, Math.Min(width, height) / 6.0);

        RoundedRect(ctx, 0.5, 0.5, width - 1, height - 1, radius);
        ctx.SetSourceRGBA(bg[0], bg[1], bg[2], bg[3]);
        ctx.Fill();

        RoundedRect(ctx, 0.5, 0.5, width - 1, height - 1, radius);
        ctx.SetSourceRGBA(border[0], border[1], border[2], border[3]);
        ctx.LineWidth = 1.2;
        ctx.Stroke();
    }

    private static void RoundedRect(Context ctx, double x, double y, double w, double h, double r)
    {
        ctx.MoveTo(x + r, y);
        ctx.LineTo(x + w - r, y);
        ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
        ctx.LineTo(x + w, y + h - r);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.LineTo(x + r, y + h);
        ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
        ctx.LineTo(x, y + r);
        ctx.Arc(x + r, y + r, r, Math.PI, 3 * Math.PI / 2);
        ctx.ClosePath();
    }
}
