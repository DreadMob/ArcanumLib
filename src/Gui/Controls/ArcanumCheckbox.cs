using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed checkbox: a small recessed square with a brass check mark when on,
/// plus an optional label to the right. Clicking anywhere inside the bounds
/// (box or label) toggles the state and fires <see cref="Action{T}" />.
/// </summary>
public class ArcanumCheckbox : GuiElement
{
    private readonly string label;
    private readonly Action<bool>? onToggle;

    private bool isChecked;
    private bool hovered;
    private bool pressed;
    private bool enabled = true;
    private LoadedTexture tex;
    private string? cacheKey;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">Control bounds; the box is left-aligned, label fills the rest.</param>
    /// <param name="label">Optional label text drawn right of the box (may be empty).</param>
    /// <param name="initial">Initial checked state.</param>
    /// <param name="onToggle">Called with the new state on toggle.</param>
    public ArcanumCheckbox(ICoreClientAPI capi, ElementBounds bounds, string? label, bool initial, Action<bool>? onToggle)
        : base(capi, bounds)
    {
        this.label = label ?? "";
        isChecked = initial;
        this.onToggle = onToggle;
        tex = new LoadedTexture(capi);
    }

    /// <summary>Current checked state.</summary>
    public bool IsChecked => isChecked;

    /// <summary>Enable/disable the checkbox; disabled checkboxes render dimmed.</summary>
    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; cacheKey = null; } }
    }

    /// <summary>Set the checked state silently (no callback).</summary>
    /// <param name="v">New state.</param>
    public void SetChecked(bool v)
    {
        if (isChecked != v) { isChecked = v; cacheKey = null; }
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <inheritdoc />
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || !Bounds.PointInside(args.X, args.Y)) return;
        pressed = true;
        cacheKey = null;
        args.Handled = true;
    }

    /// <inheritdoc />
    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        bool wasPressed = pressed;
        pressed = false;
        cacheKey = null;
        if (!enabled || !wasPressed || !Bounds.PointInside(args.X, args.Y)) return;

        isChecked = !isChecked;
        try
        {
            api.Gui.PlaySound("menubutton_press", false, 0.2f);
            onToggle?.Invoke(isChecked);
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumCheckbox] Toggle callback failed: {0}", ex);
        }
        args.Handled = true;
    }

    /// <inheritdoc />
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (pressed) { pressed = false; cacheKey = null; }
        base.OnMouseUp(api, args);
    }

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
        string key = $"{isChecked}|{hovered}|{pressed}|{enabled}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
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

            // Recessed square box, vertically centered on the left edge.
            double box = Math.Min(h, scaled(18));
            double bx = scaled(1);
            double by = (h - box) / 2.0;
            double br = scaled(3);
            double inset = pressed ? scaled(1) : 0;

            ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, bx + inset, by + inset, box - inset * 2, box - inset * 2, br,
                p.SurfaceDeepest.WithAlpha(p.SurfaceDeepest.A * dim),
                p.SurfaceBase.WithAlpha(p.SurfaceBase.A * dim));
            var rim = isChecked ? p.Accent.WithAlpha(0.8)
                    : hovered ? p.BorderStrong : p.BorderDefault;
            ArcanumGuiTheme.StrokeRoundedRect(ctx, bx + inset + 0.5, by + inset + 0.5,
                box - inset * 2 - 1, box - inset * 2 - 1, br, rim.WithAlpha(rim.A * dim), scaled(1.0));

            if (isChecked)
            {
                // Soft accent glow inside the box.
                double pad = scaled(2.5);
                ArcanumGuiTheme.FillRoundedRect(ctx, bx + pad, by + pad, box - pad * 2, box - pad * 2,
                    Math.Max(0.5, br - 1), p.Accent.WithAlpha(0.22 * dim));

                // Check mark — dark underlay for contrast, bright brass stroke on top.
                ctx.LineCap = LineCap.Round;
                ctx.LineJoin = LineJoin.Round;
                void CheckPath()
                {
                    ctx.NewPath();
                    ctx.MoveTo(bx + box * 0.22, by + box * 0.55);
                    ctx.LineTo(bx + box * 0.44, by + box * 0.74);
                    ctx.LineTo(bx + box * 0.80, by + box * 0.26);
                }
                CheckPath();
                ctx.SetSourceRGBA(p.BorderShadow.R, p.BorderShadow.G, p.BorderShadow.B, 0.85 * dim);
                ctx.LineWidth = scaled(3.0);
                ctx.Stroke();
                CheckPath();
                ctx.SetSourceRGBA(p.AccentBright.R, p.AccentBright.G, p.AccentBright.B, dim);
                ctx.LineWidth = scaled(2.0);
                ctx.Stroke();
            }

            // Optional label to the right of the box.
            if (label.Length > 0)
            {
                double lx = bx + box + scaled(7);
                ctx.Save();
                ctx.Rectangle(lx, 0, Math.Max(0, w - lx), h);
                ctx.Clip();
                ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
                ctx.SetFontSize(scaled(11));
                var ext = ctx.TextExtents(label);
                var tc = enabled ? p.TextPrimary : p.TextMuted;
                ctx.SetSourceRGBA(tc.R, tc.G, tc.B, tc.A * dim);
                ctx.MoveTo(lx, (h - ext.Height) / 2.0 - ext.YBearing);
                ctx.ShowText(label);
                ctx.Restore();
            }

            generateTexture(surface, ref tex);
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose(); tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumCheckbox] Texture generation failed: {0}", ex);
        }
        finally { ctx?.Dispose(); surface?.Dispose(); }
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); base.Dispose(); }
}

/// <summary>Composer extensions for <see cref="ArcanumCheckbox" />.</summary>
public static class ArcanumCheckboxComposer
{
    /// <summary>Adds a themed checkbox with an optional label.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="label">Optional label text right of the box (may be empty).</param>
    /// <param name="initial">Initial checked state.</param>
    /// <param name="onToggle">Called with the new state on toggle.</param>
    /// <param name="bounds">Control bounds.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The composer for chaining.</returns>
    public static GuiComposer AddArcanumCheckbox(
        this GuiComposer composer, string label, bool initial,
        Action<bool> onToggle, ElementBounds bounds, string? key = null)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(
                new ArcanumCheckbox(composer.Api, bounds, label, initial, onToggle), key);
        return composer;
    }

    /// <summary>Retrieve a checkbox added via <see cref="AddArcanumCheckbox" />.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The checkbox, or null if not found.</returns>
    public static ArcanumCheckbox? GetArcanumCheckbox(this GuiComposer composer, string key)
        => composer.GetElement(key) as ArcanumCheckbox;
}
