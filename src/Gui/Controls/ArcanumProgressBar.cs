using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed horizontal progress bar: a recessed track with a brass fill and an
/// optional centered percentage label. Not interactive — update it with
/// <see cref="SetValue" /> and it re-renders on the next frame.
/// </summary>
public class ArcanumProgressBar : GuiElement
{
    private double value;
    private bool showLabel = true;
    private LoadedTexture tex;
    private string? cacheKey;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">Bar bounds (~160x14 reads best).</param>
    /// <param name="initial">Initial fill fraction, clamped to 0..1.</param>
    public ArcanumProgressBar(ICoreClientAPI capi, ElementBounds bounds, double initial = 0)
        : base(capi, bounds)
    {
        value = Math.Clamp(initial, 0, 1);
        tex = new LoadedTexture(capi);
    }

    /// <summary>Current fill fraction, 0..1.</summary>
    public double Value => value;

    /// <summary>Whether the centered "NN%" label is drawn. Default true.</summary>
    public bool ShowLabel
    {
        get => showLabel;
        set { if (showLabel != value) { showLabel = value; cacheKey = null; } }
    }

    /// <summary>Set the fill fraction (clamped to 0..1) and redraw.</summary>
    /// <param name="v">New value, 0..1.</param>
    public void SetValue(double v)
    {
        double nv = Math.Clamp(v, 0, 1);
        if (Math.Abs(nv - value) > 0.0005) { value = nv; cacheKey = null; }
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        RegenerateIfNeeded();
        if (tex?.TextureId > 0)
            api.Render.Render2DLoadedTexture(tex, (float)Bounds.absX, (float)Bounds.absY);
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;
        tex ??= new LoadedTexture(api!);
        string key = $"{(int)(value * 1000)}|{showLabel}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
        if (string.Equals(cacheKey, key, StringComparison.Ordinal) && tex.TextureId > 0) return;
        cacheKey = key;

        int w = Math.Max(1, (int)Bounds.OuterWidth);
        int h = Math.Max(1, (int)Bounds.OuterHeight);
        ImageSurface? surface = null; Context? ctx = null;
        try
        {
            surface = new ImageSurface(Format.Argb32, w, h);
            ctx = new Context(surface);
            var p = ArcanumGuiTheme.Palette;
            double r = h / 2.0; // pill-shaped ends, like the slider track.

            // Recessed track.
            ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, 0, 0, w, h, r,
                p.SurfaceDeepest, p.SurfaceBase);
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0.5, 0.5, w - 1, h - 1, r,
                p.BorderDefault, scaled(0.8));

            // Brass fill clipped to the track interior.
            double fw = (w - 2) * value;
            if (fw > 0.5)
            {
                ctx.Save();
                ArcanumGuiTheme.RoundedRectPath(ctx, 1, 1, w - 2, h - 2, Math.Max(0.5, r - 1));
                ctx.Clip();
                ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, 1, 1, fw, h - 2,
                    Math.Min(Math.Max(0.5, r - 1), fw / 2),
                    p.AccentDim.Lerp(p.Accent, 0.5), p.Accent.WithAlpha(0.85));
                // Top sheen on the fill.
                ArcanumGuiTheme.FillRoundedRect(ctx, 1, 1, fw, Math.Max(1, (h - 2) * 0.32),
                    Math.Min(Math.Max(0.5, r - 1), fw / 2), p.AccentBright.WithAlpha(0.16));
                ctx.Restore();
            }

            // Centered percentage label with a dark shadow pass for contrast.
            if (showLabel && h >= scaled(8))
            {
                string label = $"{(int)Math.Round(value * 100)}%";
                ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
                ctx.SetFontSize(scaled(9.5));
                var ext = ctx.TextExtents(label);
                double lx = (w - ext.Width) / 2.0 - ext.XBearing;
                double ly = (h - ext.Height) / 2.0 - ext.YBearing;
                ctx.SetSourceRGBA(p.SurfaceDeepest.R, p.SurfaceDeepest.G, p.SurfaceDeepest.B, 0.8);
                ctx.MoveTo(lx + 0.75, ly + 0.75);
                ctx.ShowText(label);
                ctx.SetSourceRGBA(p.TextPrimary.R, p.TextPrimary.G, p.TextPrimary.B, 0.95);
                ctx.MoveTo(lx, ly);
                ctx.ShowText(label);
            }

            generateTexture(surface, ref tex);
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose(); tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumProgressBar] Texture generation failed: {0}", ex);
        }
        finally { ctx?.Dispose(); surface?.Dispose(); }
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); base.Dispose(); }
}

/// <summary>Composer extensions for <see cref="ArcanumProgressBar" />.</summary>
public static class ArcanumProgressBarComposer
{
    /// <summary>Adds a themed progress bar.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="bounds">Bar bounds.</param>
    /// <param name="initial">Initial fill fraction, 0..1.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The composer for chaining.</returns>
    public static GuiComposer AddArcanumProgressBar(
        this GuiComposer composer, ElementBounds bounds, double initial = 0, string? key = null)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(
                new ArcanumProgressBar(composer.Api, bounds, initial), key);
        return composer;
    }

    /// <summary>Retrieve a progress bar added via <see cref="AddArcanumProgressBar" />.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The progress bar, or null if not found.</returns>
    public static ArcanumProgressBar? GetArcanumProgressBar(this GuiComposer composer, string key)
        => composer.GetElement(key) as ArcanumProgressBar;
}
