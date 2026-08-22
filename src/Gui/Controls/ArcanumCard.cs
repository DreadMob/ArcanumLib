using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Renders a rounded card background with optional status strip and corner ornament.
/// </summary>
public class ArcanumCard : GuiElement
{
    private readonly RGBA _fill;
    private readonly RGBA _border;
    private readonly RGBA? _accent;
    private readonly bool _drawInnerBorder;
    private readonly bool _drawOrnament;
    private readonly Action<Context, ElementBounds>? _drawContent;

    public ArcanumCard(
        ICoreClientAPI capi,
        ElementBounds bounds,
        RGBA? fill = null,
        RGBA? border = null,
        RGBA? accent = null,
        bool drawInnerBorder = true,
        bool drawOrnament = true,
        Action<Context, ElementBounds>? drawContent = null)
        : base(capi, bounds)
    {
        _fill = fill ?? ArcanumGuiTheme.SurfaceCard.WithAlpha(0.45);
        _border = border ?? ArcanumGuiTheme.BorderShadow.WithAlpha(0.55);
        _accent = accent;
        _drawInnerBorder = drawInnerBorder;
        _drawOrnament = drawOrnament;
        _drawContent = drawContent;
    }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();

        double r = GuiElement.scaled(ArcanumGuiTheme.Radius.Medium);
        double x = Bounds.drawX;
        double y = Bounds.drawY;
        double w = Bounds.OuterWidth;
        double h = Bounds.OuterHeight;

        // Card fill and outer border.
        ArcanumGuiTheme.FillRoundedRect(ctx, x, y, w, h, r, _fill);
        ArcanumGuiTheme.StrokeRoundedRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, r, _border, GuiElement.scaled(1.0));

        // Optional inner silver border.
        if (_drawInnerBorder)
        {
            double inset = GuiElement.scaled(2.0);
            ArcanumGuiTheme.StrokeRoundedRect(ctx,
                x + inset, y + inset,
                w - inset * 2.0, h - inset * 2.0,
                Math.Max(1.0, r - inset),
                ArcanumGuiTheme.BorderSilver.WithAlpha(0.45),
                GuiElement.scaled(1.0));
        }

        // Optional status accent strip on the left.
        if (_accent.HasValue)
        {
            ArcanumGuiTheme.FillRoundedRect(ctx,
                x + GuiElement.scaled(8.0), y + GuiElement.scaled(12.0),
                GuiElement.scaled(3.0), h - GuiElement.scaled(24.0),
                GuiElement.scaled(1.5),
                _accent.Value.WithAlpha(0.95));
        }

        // Optional corner ornament.
        if (_drawOrnament)
        {
            ArcanumGuiTheme.DrawCornerOrnament(ctx, x, y, w, h,
                GuiElement.scaled(8.0),
                ArcanumGuiTheme.BorderSilver.WithAlpha(0.65));
        }

        // Custom content drawn relative to the card origin.
        if (_drawContent != null)
        {
            ctx.Save();
            ctx.Translate(x, y);
            _drawContent(ctx, Bounds);
            ctx.Restore();
        }
    }
}

public static class ArcanumCardComposerHelpers
{
    /// <summary>
    /// Adds a card background element.
    /// </summary>
    public static GuiComposer AddArcanumCard(
        this GuiComposer composer,
        ElementBounds bounds,
        RGBA? fill = null,
        RGBA? border = null,
        RGBA? accent = null,
        bool drawInnerBorder = true,
        bool drawOrnament = true,
        Action<Context, ElementBounds>? drawContent = null,
        string? key = null)
    {
        if (!composer.Composed)
        {
            composer.AddStaticElement(new ArcanumCard(composer.Api, bounds, fill, border, accent, drawInnerBorder, drawOrnament, drawContent), key);
        }
        return composer;
    }
}
