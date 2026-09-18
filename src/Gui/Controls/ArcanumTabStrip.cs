using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed horizontal tab strip: a single row of equal-width tabs sharing the
/// element bounds. The active tab renders as an accent-lit brass plate, inactive
/// tabs as dark recessed plates. Fires <see cref="Action{T}" /> with the new
/// index on tab change.
/// </summary>
public class ArcanumTabStrip : GuiElement
{
    private readonly string[] names;
    private readonly Action<int>? onTab;

    private int selectedIndex;
    private int hoveredIndex = -1;
    private int pressedIndex = -1;
    private bool enabled = true;
    private LoadedTexture tex;
    private string? cacheKey;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">Strip bounds; each tab gets an equal share of the width.</param>
    /// <param name="names">Tab labels, left to right.</param>
    /// <param name="selected">Initially selected index.</param>
    /// <param name="onTab">Called with the new index when the user picks a tab.</param>
    public ArcanumTabStrip(ICoreClientAPI capi, ElementBounds bounds, string[] names, int selected, Action<int>? onTab)
        : base(capi, bounds)
    {
        this.names = names ?? Array.Empty<string>();
        selectedIndex = Math.Clamp(selected, 0, Math.Max(0, this.names.Length - 1));
        this.onTab = onTab;
        tex = new LoadedTexture(capi);
    }

    /// <summary>Currently selected tab index.</summary>
    public int SelectedIndex => selectedIndex;

    /// <summary>Currently selected tab label ("" when the strip is empty).</summary>
    public string SelectedName =>
        selectedIndex >= 0 && selectedIndex < names.Length ? names[selectedIndex] : "";

    /// <summary>Enable/disable the strip; disabled strips render dimmed and ignore input.</summary>
    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; cacheKey = null; } }
    }

    /// <summary>Select a tab silently (no callback).</summary>
    /// <param name="index">New tab index, clamped into range.</param>
    public void SetSelectedIndex(int index)
    {
        int nv = Math.Clamp(index, 0, Math.Max(0, names.Length - 1));
        if (nv != selectedIndex) { selectedIndex = nv; cacheKey = null; }
    }

    /// <summary>Width of a single tab cell in px.</summary>
    private double TabWidth => names.Length > 0 ? Bounds.OuterWidth / names.Length : 0;

    /// <summary>Tab index under an absolute mouse position, or -1 when outside.</summary>
    private int TabIndexAt(double absX, double absY)
    {
        if (names.Length == 0 || !Bounds.PointInside(absX, absY)) return -1;
        double tabW = TabWidth;
        if (tabW <= 0) return -1;
        return Math.Clamp((int)((absX - Bounds.absX) / tabW), 0, names.Length - 1);
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <inheritdoc />
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled) return;
        int i = TabIndexAt(args.X, args.Y);
        if (i < 0) return;

        pressedIndex = i;
        cacheKey = null;
        args.Handled = true;

        if (i != selectedIndex)
        {
            selectedIndex = i;
            try
            {
                api.Gui.PlaySound("menubutton_press", false, 0.2f);
                onTab?.Invoke(i);
            }
            catch (Exception ex)
            {
                api?.Logger?.Warning("[ArcanumTabStrip] Tab callback failed: {0}", ex);
            }
        }
    }

    /// <inheritdoc />
    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (pressedIndex >= 0) { pressedIndex = -1; cacheKey = null; }
        base.OnMouseUpOnElement(api, args);
    }

    /// <inheritdoc />
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (pressedIndex >= 0) { pressedIndex = -1; cacheKey = null; }
        base.OnMouseUp(api, args);
    }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;

        int nowHovered = enabled ? TabIndexAt(api.Input.MouseX, api.Input.MouseY) : -1;
        if (nowHovered != hoveredIndex)
        {
            bool hadHover = hoveredIndex >= 0;
            hoveredIndex = nowHovered;
            cacheKey = null;
            if (hoveredIndex >= 0 && !hadHover && enabled)
            {
                try { api?.Gui?.PlaySound("menubutton", false, 0.12f); }
                catch (Exception ex) { api?.Logger?.Warning("[ArcanumTabStrip] Hover sound failed: {0}", ex); }
            }
        }

        RegenerateIfNeeded();
        if (tex?.TextureId > 0)
            api?.Render?.Render2DLoadedTexture(tex, (float)Bounds.absX, (float)Bounds.absY);
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;
        tex ??= new LoadedTexture(api!);
        string key = $"{selectedIndex}|{hoveredIndex}|{pressedIndex}|{enabled}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
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

            // Baseline under the whole strip; the tab plates sit on top of it.
            ctx.SetSourceRGBA(p.BorderDefault.R, p.BorderDefault.G, p.BorderDefault.B, p.BorderDefault.A * dim);
            ctx.LineWidth = scaled(1.0);
            ctx.MoveTo(0, h - 0.5);
            ctx.LineTo(w, h - 0.5);
            ctx.Stroke();

            double tabW = names.Length > 0 ? w / (double)names.Length : w;
            for (int i = 0; i < names.Length; i++)
            {
                double tx = i * tabW + gap / 2;
                double tw = Math.Max(1, tabW - gap);
                bool active = i == selectedIndex;
                bool hov = i == hoveredIndex;
                bool prs = i == pressedIndex;

                if (active)
                {
                    // Accent-lit brass plate for the selected tab.
                    ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, tx, 0, tw, h, r,
                        p.AccentDim.Lerp(p.Accent, prs ? 0.75 : 0.55).WithAlpha(dim),
                        p.AccentDim.Lerp(p.Accent, 0.30).WithAlpha(dim));
                    ArcanumGuiTheme.StrokeRoundedRect(ctx, tx + 0.5, 0.5, tw - 1, h - 1, r,
                        p.Accent.WithAlpha(0.9 * dim), scaled(1.0));
                    ArcanumGuiTheme.DrawInnerHighlight(ctx, tx + 1, 1, tw - 2, h - 2, Math.Max(0.5, r - 1), 0.12 * dim);
                }
                else
                {
                    // Dark recessed plate for inactive tabs.
                    var top = hov ? p.SurfaceBase.Lerp(p.SurfaceElevated, 0.5) : p.SurfaceDeepest;
                    var bottom = hov ? p.SurfaceElevated : p.SurfaceBase;
                    ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, tx, 0, tw, h, r,
                        top.WithAlpha(top.A * dim), bottom.WithAlpha(bottom.A * dim));
                    ArcanumGuiTheme.StrokeRoundedRect(ctx, tx + 0.5, 0.5, tw - 1, h - 1, r,
                        (hov ? p.BorderStrong : p.BorderDefault).WithAlpha(dim), scaled(0.8));
                }

                // Tab label, clipped to its cell.
                ctx.Save();
                ctx.Rectangle(tx, 0, tw, h);
                ctx.Clip();
                ctx.SelectFontFace("Sans", FontSlant.Normal, active ? FontWeight.Bold : FontWeight.Normal);
                ctx.SetFontSize(scaled(11));
                var ext = ctx.TextExtents(names[i]);
                var tc = active ? p.TextPrimary : hov ? p.TextPrimary : p.TextSecondary;
                ctx.SetSourceRGBA(tc.R, tc.G, tc.B, tc.A * dim);
                ctx.MoveTo(tx + (tw - ext.Width) / 2.0 - ext.XBearing,
                           (h - ext.Height) / 2.0 - ext.YBearing);
                ctx.ShowText(names[i]);
                ctx.Restore();
            }

            generateTexture(surface, ref tex);
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose(); tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumTabStrip] Texture generation failed: {0}", ex);
        }
        finally { ctx?.Dispose(); surface?.Dispose(); }
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); base.Dispose(); }
}

/// <summary>Composer extensions for <see cref="ArcanumTabStrip" />.</summary>
public static class ArcanumTabStripComposer
{
    /// <summary>Adds a themed horizontal tab strip.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="names">Tab labels, left to right.</param>
    /// <param name="selected">Initially selected index.</param>
    /// <param name="onTab">Called with the new index on tab change.</param>
    /// <param name="bounds">Strip bounds.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The composer for chaining.</returns>
    public static GuiComposer AddArcanumTabStrip(
        this GuiComposer composer, string[] names, int selected,
        Action<int> onTab, ElementBounds bounds, string? key = null)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(
                new ArcanumTabStrip(composer.Api, bounds, names, selected, onTab), key);
        return composer;
    }

    /// <summary>Retrieve a tab strip added via <see cref="AddArcanumTabStrip" />.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The tab strip, or null if not found.</returns>
    public static ArcanumTabStrip? GetArcanumTabStrip(this GuiComposer composer, string key)
        => composer.GetElement(key) as ArcanumTabStrip;
}
