using System;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Static icon element that renders an asset from <see cref="ImageIconCache"/>.
/// Supports clipping shapes (circle, hexagon, diamond) and optional tinting.
/// </summary>
public class ArcanumIcon : GuiElement
{
    private readonly string _assetPath;
    private readonly double _radius;
    private readonly RGBA _color;
    private readonly IconFit _fit;
    private readonly bool _tint;

    /// <summary>
    /// Creates a static icon element that renders an asset from <see cref="ImageIconCache"/>.
    /// </summary>
    public ArcanumIcon(
        ICoreClientAPI capi,
        ElementBounds bounds,
        string assetPath,
        RGBA? color = null,
        IconFit fit = IconFit.None,
        bool tint = false)
        : base(capi, bounds)
    {
        _assetPath = assetPath ?? "";
        _radius = bounds.OuterWidth / 2.0;
        _color = color ?? ArcanumGuiTheme.TextPrimary;
        _fit = fit;
        _tint = tint;
    }

    /// <summary>Renders the clipped and tinted icon, or a placeholder if the asset fails to load.</summary>
    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        if (string.IsNullOrWhiteSpace(_assetPath) || _radius <= 0)
            return;

        ImageIconCache.Init(api);
        Bounds.CalcWorldBounds();

        double cx = Bounds.OuterWidth / 2.0;
        double cy = Bounds.OuterHeight / 2.0;

        if (!ImageIconCache.TryDrawIcon(ctx, _assetPath, cx, cy, _radius, _color, _fit, _tint))
        {
            // Icon failed to load; draw a placeholder circle so the layout doesn't look broken.
            ArcanumGuiTheme.StrokeCircle(ctx, cx, cy, _radius * 0.8, ArcanumGuiTheme.BorderSubtle, GuiElement.scaled(1.0));
        }
    }
}

public static class ArcanumIconComposerHelpers
{
    /// <summary>
    /// Adds a static icon element.
    /// </summary>
    public static GuiComposer AddArcanumIcon(
        this GuiComposer composer,
        string assetPath,
        ElementBounds bounds,
        RGBA? color = null,
        IconFit fit = IconFit.None,
        bool tint = false,
        string? key = null)
    {
        if (!composer.Composed)
        {
            composer.AddStaticElement(new ArcanumIcon(composer.Api, bounds, assetPath, color, fit, tint), key);
        }
        return composer;
    }
}
