using System;
using ArcanumLib.Diagnostics;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using FuncInt = System.Func<int, bool>;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed horizontal slider: recessed track, brass fill and a round brass knob.
/// Drop-in themed replacement for the vanilla <c>AddSlider</c> — same
/// <see cref="SetValues" /> / <see cref="GetValue" /> surface.
/// </summary>
public class ArcanumSlider : GuiElement
{
    private readonly FuncInt onNewValue;
    private readonly string unitName;

    private int value;
    private int minValue;
    private int maxValue = 100;
    private int step = 1;
    private string unitSuffix = "";

    private bool dragging;
    private bool hovered;
    private bool enabled = true;
    private LoadedTexture tex;
    private string? cacheKey;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">Slider bounds (~180x24 reads best).</param>
    /// <param name="onNewValue">Called while dragging; return true to accept.</param>
    /// <param name="unitName">Optional unit appended to the readout.</param>
    public ArcanumSlider(ICoreClientAPI capi, ElementBounds bounds, FuncInt onNewValue, string? unitName = null)
        : base(capi, bounds)
    {
        this.onNewValue = onNewValue;
        this.unitName = unitName ?? "";
        tex = new LoadedTexture(capi);
        GuiTextureTracker.Register(nameof(ArcanumSlider));
    }

    /// <summary>Enable/disable the slider; disabled sliders render dimmed.</summary>
    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; cacheKey = null; } }
    }

    /// <summary>Current value.</summary>
    public int GetValue() => value;

    /// <summary>Set value + range; same call shape as the vanilla slider.</summary>
    public void SetValues(int newValue, int minValue, int maxValue, int step, string unitSuffix = "")
    {
        this.minValue = minValue;
        this.maxValue = Math.Max(minValue, maxValue);
        this.step = Math.Max(1, step);
        this.unitSuffix = unitSuffix;
        value = Math.Clamp(newValue, this.minValue, this.maxValue);
        cacheKey = null;
    }

    /// <summary>Set the value silently (no callback).</summary>
    public void SetValue(int v)
    {
        int nv = Math.Clamp(v, minValue, maxValue);
        if (nv != value) { value = nv; cacheKey = null; }
    }

    private double Frac => maxValue > minValue ? (value - minValue) / (double)(maxValue - minValue) : 0;

    private void UpdateFromX(double mouseX)
    {
        double w = Bounds.OuterWidth;
        double f = w > 0 ? Math.Clamp((mouseX - Bounds.absX) / w, 0, 1) : 0;
        int raw = (int)Math.Round(minValue + f * (maxValue - minValue));
        int stepped = minValue + (int)Math.Round((raw - minValue) / (double)step) * step;
        int nv = Math.Clamp(stepped, minValue, maxValue);
        if (nv != value)
        {
            value = nv;
            cacheKey = null;
            try { onNewValue?.Invoke(nv); }
            catch (Exception ex) { api?.Logger?.Warning("[ArcanumSlider] Callback failed: {0}", ex); }
        }
    }

    /// <inheritdoc />
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || !Bounds.PointInside(args.X, args.Y)) return;
        dragging = true;
        UpdateFromX(args.X);
        api.Gui.PlaySound("menubutton", false, 0.15f);
        args.Handled = true;
    }

    /// <inheritdoc />
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (dragging) { UpdateFromX(args.X); args.Handled = true; }
        base.OnMouseMove(api, args);
    }

    /// <inheritdoc />
    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        dragging = false;
        base.OnMouseUpOnElement(api, args);
    }

    /// <inheritdoc />
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        dragging = false;
        base.OnMouseUp(api, args);
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        bool nowHovered = enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        if (nowHovered != hovered) { hovered = nowHovered; cacheKey = null; }

        RegenerateIfNeeded();
        if (tex?.TextureId > 0)
            api.Render.Render2DLoadedTexture(tex, (float)Bounds.absX, (float)Bounds.absY);
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;
        tex ??= new LoadedTexture(api!);
        string key = $"{value}|{hovered}|{dragging}|{enabled}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
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
            double dim = enabled ? 1.0 : 0.45;

            // Track hugs the bottom of the bounds so the readout text above
            // it stays fully inside the texture (24 px bounds → ~10 px strip).
            double th = scaled(10);
            double knobR = scaled(8);
            double cy = h - Math.Max(th / 2, knobR) - scaled(1);
            double ty = cy - th / 2;
            ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, 0, ty, w, th, th / 2,
                p.SurfaceDeepest, p.SurfaceBase);
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0.5, ty + 0.5, w - 1, th - 1, th / 2,
                p.BorderDefault, scaled(0.8));

            // Brass fill up to the knob.
            double kx = th + Frac * (w - 2 * th);
            if (Frac > 0.001)
            {
                ctx.Save();
                ArcanumGuiTheme.RoundedRectPath(ctx, 1, ty + 1, w - 2, th - 2, th / 2 - 1);
                ctx.Clip();
                ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, 1, ty + 1, Math.Max(1, kx - 1), th - 2, th / 2 - 1,
                    p.AccentDim.Lerp(p.Accent, 0.5), p.Accent.WithAlpha(0.85));
                ctx.Restore();
            }

            // Knob — brass circle with a darker center pin.
            double knobY = cy;
            double kAlpha = dim;
            var kFill = hovered || dragging ? p.AccentBright : p.Accent;
            ctx.NewPath();
            ctx.Arc(kx, knobY, knobR, 0, Math.PI * 2);
            var grad = new RadialGradient(kx - knobR * 0.4, knobY - knobR * 0.4, knobR * 0.15,
                                          kx, knobY, knobR * 1.1);
            grad.AddColorStop(0, new Color(kFill.R, kFill.G, kFill.B, kAlpha));
            grad.AddColorStop(1, new Color(p.BorderShadow.R, p.BorderShadow.G, p.BorderShadow.B, kAlpha));
            ctx.SetSource(grad);
            ctx.FillPreserve();
            ctx.SetSourceRGBA(p.BorderSilverBright.R, p.BorderSilverBright.G, p.BorderSilverBright.B, 0.8 * kAlpha);
            ctx.LineWidth = scaled(1.0);
            ctx.Stroke();
            ctx.NewPath();
            ctx.Arc(kx, knobY, knobR * 0.35, 0, Math.PI * 2);
            ctx.SetSourceRGBA(p.SurfaceDeepest.R, p.SurfaceDeepest.G, p.SurfaceDeepest.B, 0.9 * kAlpha);
            ctx.Fill();

            // Readout text, right-aligned above the track. Baseline clamped by
            // the font ascent so glyphs never clip above the texture top even
            // when the bounds leave only a thin strip.
            string label = unitName.Length > 0 ? $"{value}{unitSuffix} {unitName}" : $"{value}{unitSuffix}";
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
            ctx.SetFontSize(scaled(9));
            var ext = ctx.TextExtents(label);
            double baseline = Math.Max(ctx.FontExtents.Ascent + scaled(0.5), ty - scaled(2));
            ctx.SetSourceRGBA(p.TextSecondary.R, p.TextSecondary.G, p.TextSecondary.B, 0.95 * kAlpha);
            ctx.MoveTo(w - ext.Width - scaled(4), baseline);
            ctx.ShowText(label);

            generateTexture(surface, ref tex);
            GuiTextureTracker.Regen(nameof(ArcanumSlider));
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose(); tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumSlider] Texture generation failed: {0}", ex);
        }
        finally { ctx?.Dispose(); surface?.Dispose(); }
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); GuiTextureTracker.Unregister(nameof(ArcanumSlider)); base.Dispose(); }
}

/// <summary>Composer extensions for <see cref="ArcanumSlider" />.</summary>
public static class ArcanumSliderComposer
{
    /// <summary>Adds a themed slider. Signature matches vanilla <c>AddSlider</c>.</summary>
    public static GuiComposer AddArcanumSlider(
        this GuiComposer composer, FuncInt onNewValue,
        ElementBounds bounds, string? key = null, string? unitName = null)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(new ArcanumSlider(composer.Api, bounds, onNewValue, unitName), key);
        return composer;
    }

    /// <summary>Retrieve a slider added via <see cref="AddArcanumSlider" />.</summary>
    public static ArcanumSlider? GetArcanumSlider(this GuiComposer composer, string key)
        => composer.GetElement(key) as ArcanumSlider;
}
