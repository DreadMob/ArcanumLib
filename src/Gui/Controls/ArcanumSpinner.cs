using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed numeric stepper: the current value sits in a recessed center field
/// with brass −/+ buttons on either side. Stepping clamps to [min, max] and
/// fires <see cref="Action{T}" /> with the new value.
/// </summary>
public class ArcanumSpinner : GuiElement
{
    private readonly Action<int>? onChange;

    private int value;
    private int minValue;
    private int maxValue;
    private int step;

    private int hoverZone;    // 0 = none/field, 1 = minus, 2 = plus
    private int pressedZone;
    private bool hovered;
    private bool enabled = true;
    private LoadedTexture tex;
    private string? cacheKey;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">Control bounds (~110x24 reads best).</param>
    /// <param name="initial">Initial value, clamped into range.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <param name="step">Step applied per button click.</param>
    /// <param name="onChange">Called with the new value on every step.</param>
    public ArcanumSpinner(ICoreClientAPI capi, ElementBounds bounds,
        int initial, int min, int max, int step, Action<int>? onChange)
        : base(capi, bounds)
    {
        minValue = min;
        maxValue = Math.Max(min, max);
        this.step = Math.Max(1, step);
        value = Math.Clamp(initial, minValue, maxValue);
        this.onChange = onChange;
        tex = new LoadedTexture(capi);
    }

    /// <summary>Current value.</summary>
    public int GetValue() => value;

    /// <summary>Enable/disable the spinner; disabled spinners render dimmed and ignore input.</summary>
    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; cacheKey = null; } }
    }

    /// <summary>Set the value silently (no callback), clamped into range.</summary>
    /// <param name="v">New value.</param>
    public void SetValue(int v)
    {
        int nv = Math.Clamp(v, minValue, maxValue);
        if (nv != value) { value = nv; cacheKey = null; }
    }

    /// <summary>Update min/max/step; the current value is re-clamped.</summary>
    /// <param name="min">New minimum.</param>
    /// <param name="max">New maximum.</param>
    /// <param name="step">New step.</param>
    public void SetRange(int min, int max, int step)
    {
        minValue = min;
        maxValue = Math.Max(min, max);
        this.step = Math.Max(1, step);
        value = Math.Clamp(value, minValue, maxValue);
        cacheKey = null;
    }

    /// <summary>Side-button width in px (square-ish, capped at a third of the width).</summary>
    private double BtnW => Math.Min(Bounds.OuterHeight, Bounds.OuterWidth / 3.0);

    /// <summary>Zone under an absolute mouse position: 1 = minus, 2 = plus, 0 = field/outside.</summary>
    private int ZoneAt(double absX, double absY)
    {
        if (!Bounds.PointInside(absX, absY)) return 0;
        double bw = BtnW;
        double relX = absX - Bounds.absX;
        if (relX < bw) return 1;
        if (relX >= Bounds.OuterWidth - bw) return 2;
        return 0;
    }

    private void ApplyDelta(int delta)
    {
        int nv = Math.Clamp(value + delta, minValue, maxValue);
        if (nv == value) return;
        value = nv;
        cacheKey = null;
        try
        {
            api?.Gui?.PlaySound("menubutton", false, 0.15f);
            onChange?.Invoke(nv);
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumSpinner] Change callback failed: {0}", ex);
        }
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <inheritdoc />
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || !Bounds.PointInside(args.X, args.Y)) return;
        int zone = ZoneAt(args.X, args.Y);
        pressedZone = zone;
        cacheKey = null;
        args.Handled = true;
        if (zone == 1) ApplyDelta(-step);
        else if (zone == 2) ApplyDelta(step);
    }

    /// <inheritdoc />
    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (pressedZone != 0) { pressedZone = 0; cacheKey = null; }
        base.OnMouseUpOnElement(api, args);
    }

    /// <inheritdoc />
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (pressedZone != 0) { pressedZone = 0; cacheKey = null; }
        base.OnMouseUp(api, args);
    }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        bool nowHovered = enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        int nowZone = nowHovered ? ZoneAt(api.Input.MouseX, api.Input.MouseY) : 0;
        if (nowHovered != hovered || nowZone != hoverZone)
        {
            hovered = nowHovered;
            hoverZone = nowZone;
            cacheKey = null;
        }

        RegenerateIfNeeded();
        if (tex?.TextureId > 0)
            api.Render.Render2DLoadedTexture(tex, (float)Bounds.absX, (float)Bounds.absY);
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;
        tex ??= new LoadedTexture(api!);
        string key = $"{value}|{minValue}|{maxValue}|{step}|{hoverZone}|{pressedZone}|{enabled}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
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
            double r = scaled(ArcanumGuiTheme.Radius.Small);
            double gap = scaled(2);
            double bw = Math.Min(h, w / 3.0);

            // ── Recessed value field between the buttons. ────────────────
            double fx = bw + gap;
            double fw = Math.Max(1, w - 2 * (bw + gap));
            ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, fx, 0, fw, h, r,
                p.SurfaceDeepest.WithAlpha(p.SurfaceDeepest.A * dim),
                p.SurfaceBase.WithAlpha(p.SurfaceBase.A * dim));
            ArcanumGuiTheme.StrokeRoundedRect(ctx, fx + 0.5, 0.5, fw - 1, h - 1, r,
                p.BorderDefault.WithAlpha(p.BorderDefault.A * dim), scaled(0.8));

            // ── Side buttons. ────────────────────────────────────────────
            DrawStepButton(ctx, p, 0, h, bw, r, "-", value > minValue,
                hoverZone == 1, pressedZone == 1, dim);
            DrawStepButton(ctx, p, w - bw, h, bw, r, "+", value < maxValue,
                hoverZone == 2, pressedZone == 2, dim);

            // ── Centered value text. ─────────────────────────────────────
            string label = value.ToString();
            ctx.Save();
            ctx.Rectangle(fx, 0, fw, h);
            ctx.Clip();
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
            ctx.SetFontSize(scaled(11));
            var ext = ctx.TextExtents(label);
            ctx.SetSourceRGBA(p.TextPrimary.R, p.TextPrimary.G, p.TextPrimary.B, 0.95 * dim);
            ctx.MoveTo(fx + (fw - ext.Width) / 2.0 - ext.XBearing,
                       (h - ext.Height) / 2.0 - ext.YBearing);
            ctx.ShowText(label);
            ctx.Restore();

            generateTexture(surface, ref tex);
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose(); tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumSpinner] Texture generation failed: {0}", ex);
        }
        finally { ctx?.Dispose(); surface?.Dispose(); }
    }

    /// <summary>Draws one −/+ brass plate button at (bx, 0) sized (bw, h).</summary>
    private void DrawStepButton(Context ctx, GuiThemePalette p, double bx, double h, double bw,
        double r, string glyph, bool canAct, bool hov, bool prs, double dim)
    {
        if (canAct)
        {
            double t = prs ? 0.75 : hov ? 0.65 : 0.45;
            ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, bx, 0, bw, h, r,
                p.AccentDim.Lerp(p.Accent, t).WithAlpha(dim),
                p.AccentDim.Lerp(p.Accent, t * 0.6).WithAlpha(dim));
            ArcanumGuiTheme.StrokeRoundedRect(ctx, bx + 0.5, 0.5, bw - 1, h - 1, r,
                p.Accent.WithAlpha((hov ? 0.95 : 0.65) * dim), scaled(1.0));
            ArcanumGuiTheme.DrawInnerHighlight(ctx, bx + 1, 1, bw - 2, h - 2, Math.Max(0.5, r - 1), 0.1 * dim);
        }
        else
        {
            // Direction is at its limit — dead plate.
            ArcanumGuiTheme.FillRoundedRect(ctx, bx, 0, bw, h, r,
                p.SurfaceCard.WithAlpha(p.SurfaceCard.A * 0.6 * dim));
            ArcanumGuiTheme.StrokeRoundedRect(ctx, bx + 0.5, 0.5, bw - 1, h - 1, r,
                p.BorderDefault.WithAlpha(p.BorderDefault.A * 0.7 * dim), scaled(0.8));
        }

        var gc = canAct ? p.TextPrimary : p.TextMuted;
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(scaled(12));
        var ext = ctx.TextExtents(glyph);
        ctx.SetSourceRGBA(gc.R, gc.G, gc.B, gc.A * dim);
        ctx.MoveTo(bx + (bw - ext.Width) / 2.0 - ext.XBearing,
                   (h - ext.Height) / 2.0 - ext.YBearing + (prs ? scaled(0.5) : 0));
        ctx.ShowText(glyph);
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); base.Dispose(); }
}

/// <summary>Composer extensions for <see cref="ArcanumSpinner" />.</summary>
public static class ArcanumSpinnerComposer
{
    /// <summary>Adds a themed numeric stepper.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="initial">Initial value, clamped into range.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <param name="step">Step applied per click.</param>
    /// <param name="onChange">Called with the new value on every step.</param>
    /// <param name="bounds">Control bounds.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The composer for chaining.</returns>
    public static GuiComposer AddArcanumSpinner(
        this GuiComposer composer, int initial, int min, int max, int step,
        Action<int> onChange, ElementBounds bounds, string? key = null)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(
                new ArcanumSpinner(composer.Api, bounds, initial, min, max, step, onChange), key);
        return composer;
    }

    /// <summary>Retrieve a spinner added via <see cref="AddArcanumSpinner" />.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The spinner, or null if not found.</returns>
    public static ArcanumSpinner? GetArcanumSpinner(this GuiComposer composer, string key)
        => composer.GetElement(key) as ArcanumSpinner;
}
