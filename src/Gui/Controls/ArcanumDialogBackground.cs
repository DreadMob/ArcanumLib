using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed dialog background for the Arcanum GUI toolkit.
/// Draws a flat dark dialog with rounded corners, a soft outer shadow, a one-pixel
/// inner highlight along the top edge and an accent strip just below the title bar.
/// This is a drop-in replacement for <c>AddShadedDialogBG</c>.
/// </summary>
public class ArcanumDialogBackground : GuiElement
{
    private readonly bool withTitlebar;
    private readonly bool drawAccentStrip;

    /// <summary>
    /// Creates a new dialog background.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="withTitlebar">Whether the title-bar tint should be drawn.</param>
    /// <param name="drawAccentStrip">Whether a decorative accent line is drawn under the title bar.</param>
    public ArcanumDialogBackground(ICoreClientAPI capi, ElementBounds bounds, bool withTitlebar, bool drawAccentStrip)
        : base(capi, bounds)
    {
        this.withTitlebar = withTitlebar;
        this.drawAccentStrip = drawAccentStrip;
    }

    /// <summary>Composes the dialog background with shadow, gradient, borders and decorative elements.</summary>
    /// <param name="ctx">The ctx value.</param>
    /// <param name="surface">The surface value.</param>
    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();

        double titleH = withTitlebar ? scaled(GuiStyle.TitleBarHeight) : 0.0;
        double radius = scaled(ArcanumGuiTheme.Radius.Medium);

        double x = Bounds.bgDrawX;
        double y = Bounds.bgDrawY;
        double w = Bounds.OuterWidth;
        double h = Bounds.OuterHeight;

        // Outer shadow.
        ArcanumGuiTheme.DrawSoftShadow(ctx, x, y, w, h, radius, scaled(20.0), 0.55);

        // Main panel - vertical gradient elevated -> base.
        ArcanumGuiTheme.FillRoundedRectVerticalGradient(
            ctx, x, y, w, h, radius,
            ArcanumGuiTheme.SurfaceElevated,
            ArcanumGuiTheme.SurfaceBase);

        // Title-bar tint sliver.
        if (withTitlebar && titleH > 0.0)
        {
            ArcanumGuiTheme.RoundedRectPath(ctx, x, y, w, titleH, radius);
            ctx.Clip();
            ArcanumGuiTheme.FillRoundedRectVerticalGradient(
                ctx, x, y, w, titleH, radius,
                ArcanumGuiTheme.SurfaceCard,
                ArcanumGuiTheme.SurfaceElevated);
            ctx.ResetClip();

            // Decorative copper accent line under the title bar.
            if (drawAccentStrip)
            {
                double stripY = y + titleH - scaled(1.0);
                ArcanumGuiTheme.DrawSilverDivider(
                    ctx,
                    x + scaled(20.0),
                    stripY,
                    w - scaled(40.0),
                    ArcanumGuiTheme.Accent.WithAlpha(0.7));
            }
        }

        // Outer dark rim - reads as a recessed groove.
        ArcanumGuiTheme.StrokeRoundedRect(
            ctx, x + 0.5, y + 0.5, w - 1, h - 1, radius,
            ArcanumGuiTheme.BorderShadow, scaled(1.0));

        // Silver inlay - inset by 2px so it reads as a separate ornament.
        double inset = scaled(3.0);
        ArcanumGuiTheme.StrokeRoundedRect(
            ctx,
            x + inset, y + inset,
            w - inset * 2.0, h - inset * 2.0,
            Math.Max(1.0, radius - inset),
            ArcanumGuiTheme.BorderSilver, scaled(1.0));

        // Top inner highlight.
        ArcanumGuiTheme.DrawInnerHighlight(ctx, x + 1, y + 1, w - 2, h - 2, radius - 1, 0.10);

        // Corner ornaments (silver).
        ArcanumGuiTheme.DrawCornerOrnament(
            ctx, x, y, w, h,
            scaled(10.0),
            ArcanumGuiTheme.BorderSilverBright);
    }
}

/// <summary>
/// Composer extension methods for adding <see cref="ArcanumDialogBackground" /> elements.
/// </summary>
public static class ArcanumDialogComposerHelpers
{
    /// <summary>
    /// Adds an Arcanum-styled dialog background to the composer.
    /// </summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="withTitleBar">The with title bar value.</param>
    /// <param name="accentStrip">The accent strip value.</param>
    /// <returns>The add arcanum dialog background.</returns>
    public static GuiComposer AddArcanumDialogBackground(
        this GuiComposer composer,
        ElementBounds bounds,
        bool withTitleBar = true,
        bool accentStrip = true)
    {
        if (!composer.Composed)
        {
            composer.AddStaticElement(new ArcanumDialogBackground(composer.Api, bounds, withTitleBar, accentStrip));
        }
        return composer;
    }
}
