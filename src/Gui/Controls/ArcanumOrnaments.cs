using System;
using ArcanumLib.Diagnostics;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Decorative rivet strips drawn along the top and bottom edges of a bounds.
/// Drop over a dialog background to get the bolted-plate look. Reads the
/// active <see cref="ArcanumGuiTheme.Palette" /> at compose time.
/// </summary>
public class GuiElementRivetFrame : GuiElement
{
    private readonly bool includeCorners;
    private readonly double inset;

    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="inset">Distance of rivet centers from the outer edge.</param>
    /// <param name="includeCorners">Whether corner rivets are also drawn.</param>
    public GuiElementRivetFrame(ICoreClientAPI capi, ElementBounds bounds, double inset = 10, bool includeCorners = true)
        : base(capi, bounds)
    {
        this.inset = inset;
        this.includeCorners = includeCorners;
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        var p = ArcanumGuiTheme.Palette;
        double x = Bounds.bgDrawX, y = Bounds.bgDrawY;
        double w = Bounds.OuterWidth, h = Bounds.OuterHeight;
        double r = scaled(2.2), off = scaled(inset);

        ArcanumGuiTheme.DrawRivetRow(ctx, x + off * 2, y + off, w - off * 4, r, 0, p.BorderSilver.WithAlpha(0.55));
        ArcanumGuiTheme.DrawRivetRow(ctx, x + off * 2, y + h - off, w - off * 4, r, 0, p.BorderSilver.WithAlpha(0.55));
        if (includeCorners)
            ArcanumGuiTheme.DrawCornerRivets(ctx, x, y, w, h, off, r, p.BorderSilver.WithAlpha(0.8));
    }
}

/// <summary>
/// Decorative engraved nameplate strip — a recessed brass bar for headers.
/// Pair it with static text centered on the same bounds.
/// </summary>
public class GuiElementPlaque : GuiElement
{
    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value.</param>
    public GuiElementPlaque(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds) { }

    /// <inheritdoc />
    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        var p = ArcanumGuiTheme.Palette;
        double x = Bounds.bgDrawX, y = Bounds.bgDrawY;
        double w = Bounds.OuterWidth, h = Bounds.OuterHeight;
        double r = scaled(ArcanumGuiTheme.Radius.Small);

        // Recessed plate: darker fill + brass rim + rivets at both ends.
        ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, x, y, w, h, r,
            p.SurfaceDeepest, p.SurfaceBase);
        ArcanumGuiTheme.StrokeRoundedRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, r,
            p.AccentSoft, scaled(1.0));
        double rv = scaled(1.8);
        ArcanumGuiTheme.DrawRivet(ctx, x + scaled(7), y + h / 2, rv, p.Accent);
        ArcanumGuiTheme.DrawRivet(ctx, x + w - scaled(7), y + h / 2, rv, p.Accent);
    }
}

/// <summary>
/// Analog dial gauge — a steampunk-friendly readout for a 0..1 value.
/// Draws a brass bezel, ticked arc, needle and a small caption. Static;
/// call <see cref="SetValue" /> then recompose/re-render to update.
/// </summary>
public class GuiElementDialGauge : GuiElement
{
    private readonly string caption;
    private readonly RGBA? needleColor;
    private double value;

    /// <summary>Current needle position, 0..1.</summary>
    public double Value => value;

    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value (square works best).</param>
    /// <param name="value">Initial needle position, 0..1.</param>
    /// <param name="caption">Small caption drawn under the dial center.</param>
    /// <param name="needleColor">Needle color override; default = palette accent.</param>
    public GuiElementDialGauge(ICoreClientAPI capi, ElementBounds bounds, double value = 0, string caption = "", RGBA? needleColor = null)
        : base(capi, bounds)
    {
        this.value = Math.Clamp(value, 0, 1);
        this.caption = caption;
        this.needleColor = needleColor;
    }

    /// <summary>Updates the needle position; takes effect on next recompose.</summary>
    /// <param name="v">New value, clamped to 0..1.</param>
    public void SetValue(double v) => value = Math.Clamp(v, 0, 1);

    /// <inheritdoc />
    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        double x = Bounds.bgDrawX, y = Bounds.bgDrawY;
        double w = Bounds.OuterWidth, h = Bounds.OuterHeight;
        double r = Math.Min(w, h) / 2 - scaled(2);
        double sc = scaled(1.0); // ui scale factor
        ArcanumGuiTheme.DrawDialGauge(ctx, x + w / 2, y + h / 2, r, value, caption, sc, needleColor);
    }
}

/// <summary>
/// Lever-style on/off toggle — reads as a small brass switch plate.
/// </summary>
public class ArcanumToggle : GuiElement
{
    private readonly Action<bool> onToggle;
    private bool isOn;
    private bool hovered;
    private bool enabled = true;
    private LoadedTexture tex;
    private string? cacheKey;

    /// <summary>Current switch state.</summary>
    public bool IsOn => isOn;

    /// <summary>
    /// Gets or sets whether the toggle accepts input. Disabled toggles ignore clicks.
    /// </summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value) return;
            enabled = value;
            cacheKey = null;
        }
    }

    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value (~46x22 reads best).</param>
    /// <param name="initial">Initial state.</param>
    /// <param name="onToggle">Called with the new state on flip.</param>
    public ArcanumToggle(ICoreClientAPI capi, ElementBounds bounds, bool initial, Action<bool> onToggle)
        : base(capi, bounds)
    {
        isOn = initial;
        this.onToggle = onToggle;
        tex = new LoadedTexture(capi);
        GuiTextureTracker.Register(nameof(ArcanumToggle));
    }

    /// <summary>Sets the switch state without firing the callback.</summary>
    /// <param name="v">New state.</param>
    public void SetValue(bool v) { if (isOn != v) { isOn = v; cacheKey = null; } }

    /// <inheritdoc />
    public override void ComposeElements(Context ctx, ImageSurface surface) { }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        hovered = enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        RegenerateIfNeeded();
        if (tex?.TextureId > 0)
            api.Render.Render2DLoadedTexture(tex, (float)Bounds.absX, (float)Bounds.absY);
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;
        string key = $"{isOn}|{hovered}|{enabled}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
        if (string.Equals(cacheKey, key, StringComparison.Ordinal) && tex?.TextureId > 0) return;
        cacheKey = key;

        int w = (int)Bounds.OuterWidth, h = (int)Bounds.OuterHeight;
        if (w <= 0 || h <= 0) return;

        ImageSurface? surface = null;
        Context? ctx = null;
        try
        {
            surface = new ImageSurface(Format.Argb32, w, h);
            ctx = new Context(surface);
            ArcanumGuiTheme.DrawTogglePlate(ctx, 0, 0, w, h, isOn, scaled(1.0));
            generateTexture(surface, ref tex);
            GuiTextureTracker.Regen(nameof(ArcanumToggle));
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose();
            tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumToggle] Texture generation failed: {0}", ex);
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
        }
    }

    /// <inheritdoc />
    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled) return;
        if (!Bounds.PointInside(args.X, args.Y)) return;
        isOn = !isOn;
        cacheKey = null;
        onToggle?.Invoke(isOn);
        api.Gui.PlaySound("menubutton_press", false, 0.2f);
        args.Handled = true;
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); GuiTextureTracker.Unregister(nameof(ArcanumToggle)); base.Dispose(); }
}

/// <summary>Composer extensions for the ornament controls.</summary>
public static class ArcanumOrnamentComposer
{
    /// <summary>Adds rivet strips along the bounds' top and bottom edges.</summary>
    public static GuiComposer AddRivetFrame(this GuiComposer composer, ElementBounds bounds, double inset = 10, bool corners = true)
    {
        if (!composer.Composed) composer.AddStaticElement(new GuiElementRivetFrame(composer.Api, bounds, inset, corners));
        return composer;
    }

    /// <summary>Adds a recessed nameplate strip.</summary>
    public static GuiComposer AddPlaque(this GuiComposer composer, ElementBounds bounds)
    {
        if (!composer.Composed) composer.AddStaticElement(new GuiElementPlaque(composer.Api, bounds));
        return composer;
    }

    /// <summary>Adds an analog dial gauge.</summary>
    public static GuiComposer AddDialGauge(this GuiComposer composer, ElementBounds bounds, double value = 0, string caption = "", string? key = null)
    {
        if (!composer.Composed) composer.AddStaticElement(new GuiElementDialGauge(composer.Api, bounds, value, caption), key);
        return composer;
    }

    /// <summary>Adds a lever-style toggle switch.</summary>
    public static GuiComposer AddToggle(this GuiComposer composer, ElementBounds bounds, bool initial, Action<bool> onToggle, string? key = null)
    {
        if (!composer.Composed) composer.AddInteractiveElement(new ArcanumToggle(composer.Api, bounds, initial, onToggle), key);
        return composer;
    }
}
