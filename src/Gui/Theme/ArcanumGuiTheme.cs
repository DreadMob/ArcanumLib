using System;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Theme
{
    /// <summary>
    /// Centralized colour palette and Cairo drawing helpers for the Arcanum GUI toolkit.
    /// All hex/rgb values are normalized for Cairo (0..1 range).
    /// </summary>
    public static class ArcanumGuiTheme
    {
        // --------------------------------------------------------------------
        // Surface palette - vanilla brown with a slightly warmer parchment feel
        // so the toolkit reads as part of the base game, not a different mod.
        // --------------------------------------------------------------------
        /// <summary>The surface deepest value.</summary>
        public static readonly RGBA SurfaceDeepest    = RGBA.From(0x1F, 0x18, 0x10, 0.96);
        /// <summary>The surface base value.</summary>
        public static readonly RGBA SurfaceBase       = RGBA.From(0x2E, 0x24, 0x19, 0.95);
        /// <summary>The surface elevated value.</summary>
        public static readonly RGBA SurfaceElevated   = RGBA.From(0x40, 0x35, 0x29, 0.98);
        /// <summary>The surface card value.</summary>
        public static readonly RGBA SurfaceCard       = RGBA.From(0x4A, 0x3C, 0x2C, 0.92);
        /// <summary>The surface card hover value.</summary>
        public static readonly RGBA SurfaceCardHover  = RGBA.From(0x5A, 0x47, 0x32, 0.96);
        /// <summary>The surface card active value.</summary>
        public static readonly RGBA SurfaceCardActive = RGBA.From(0x6E, 0x57, 0x3C, 0.98);

        // --------------------------------------------------------------------
        // Border / divider palette.  We layer a dark inner shadow + a silvered
        // outer rim - same recipe vanilla uses on every dialog.
        // --------------------------------------------------------------------
        /// <summary>The border shadow value.</summary>
        public static readonly RGBA BorderShadow     = RGBA.From(0x12, 0x0C, 0x07, 0.65);
        /// <summary>The border subtle value.</summary>
        public static readonly RGBA BorderSubtle     = RGBA.From(0xE9, 0xDD, 0xCE, 0.10);
        /// <summary>The border default value.</summary>
        public static readonly RGBA BorderDefault    = RGBA.From(0xE9, 0xDD, 0xCE, 0.18);
        /// <summary>The border strong value.</summary>
        public static readonly RGBA BorderStrong     = RGBA.From(0xE9, 0xDD, 0xCE, 0.35);
        /// <summary>The border silver value.</summary>
        public static readonly RGBA BorderSilver     = RGBA.From(0xC9, 0xB7, 0x8F, 0.55);
        /// <summary>The border silver bright value.</summary>
        public static readonly RGBA BorderSilverBright = RGBA.From(0xE9, 0xDD, 0xCE, 0.85);

        // --------------------------------------------------------------------
        // Accent (vanilla "active button" copper) and a warm highlight.
        // --------------------------------------------------------------------
        /// <summary>Full-opacity copper accent used for active buttons and emphasis.</summary>
        public static readonly RGBA Accent           = RGBA.From(0xC5, 0x89, 0x48, 1.00);
        /// <summary>Soft copper accent at 40% opacity for subtle highlights.</summary>
        public static readonly RGBA AccentSoft       = RGBA.From(0xC5, 0x89, 0x48, 0.40);
        /// <summary>Dim copper accent at 18% opacity for backgrounds and hovers.</summary>
        public static readonly RGBA AccentDim        = RGBA.From(0xC5, 0x89, 0x48, 0.18);
        /// <summary>Brightened copper accent for hover and focus states.</summary>
        public static readonly RGBA AccentBright     = RGBA.From(0xE3, 0xA8, 0x6A, 1.00);
        /// <summary>Warm parchment highlight used for separators and subtle borders.</summary>
        public static readonly RGBA Highlight        = RGBA.From(0xA8, 0x8B, 0x6C, 1.00);

        // --------------------------------------------------------------------
        // Status palette - tuned to read against the brown surface.
        // --------------------------------------------------------------------
        /// <summary>Copper status color for available actions.</summary>
        public static readonly RGBA StatusAvailable  = RGBA.From(0xC5, 0x89, 0x48, 1.00); // copper
        /// <summary>Pale steel-blue status color for active or in-progress states.</summary>
        public static readonly RGBA StatusActive     = RGBA.From(0x9B, 0xC5, 0xE6, 1.00); // pale steel blue
        /// <summary>Muted leaf-green status color for completed states.</summary>
        public static readonly RGBA StatusComplete   = RGBA.From(0x9F, 0xCB, 0x6E, 1.00); // muted leaf green
        /// <summary>Dim parchment status color for locked states.</summary>
        public static readonly RGBA StatusLocked     = RGBA.From(0x8A, 0x7C, 0x68, 1.00); // dim parchment
        /// <summary>Gray parchment status color for cooldown states.</summary>
        public static readonly RGBA StatusCooldown   = RGBA.From(0x8A, 0x7C, 0x68, 1.00); // gray parchment
        /// <summary>Muted iron-rust status color for failed states.</summary>
        public static readonly RGBA StatusFailed     = RGBA.From(0xCD, 0x66, 0x5C, 1.00); // muted iron-rust

        // --------------------------------------------------------------------
        // Text palette - vanilla parchment cream as the base.
        // --------------------------------------------------------------------
        /// <summary>Primary text color, high-contrast parchment cream.</summary>
        public static readonly RGBA TextPrimary      = RGBA.From(0xE9, 0xDD, 0xCE, 1.00);
        /// <summary>Secondary text color for labels and supporting text.</summary>
        public static readonly RGBA TextSecondary    = RGBA.From(0xC9, 0xB7, 0x9C, 1.00);
        /// <summary>Muted text color for hints and tertiary information.</summary>
        public static readonly RGBA TextMuted        = RGBA.From(0x8F, 0x80, 0x6A, 1.00);
        /// <summary>Disabled text color for unavailable controls.</summary>
        public static readonly RGBA TextDisabled     = RGBA.From(0x55, 0x47, 0x36, 1.00);

        // --------------------------------------------------------------------
        // Sizing tokens (already wrapped in GuiElement.scaled where used).
        // Keep these unscaled so callers explicitly opt into HiDPI scaling.
        // --------------------------------------------------------------------
        /// <summary>Corner radius tokens for buttons, panels, and cards.</summary>
        public static class Radius
        {
            /// <summary>Small corner radius for compact controls.</summary>
            public const double Small  =  4.0;
            /// <summary>Medium corner radius for standard controls.</summary>
            public const double Medium =  8.0;
            /// <summary>Large corner radius for panels and dialogs.</summary>
            public const double Large  = 12.0;
            /// <summary>Pill-shaped radius for tags and badges.</summary>
            public const double Pill   = 20.0;
        }

        /// <summary>Spacing tokens for consistent layout gaps.</summary>
        public static class Spacing
        {
            /// <summary>Extra-small spacing for tight inline gaps.</summary>
            public const double Xs =  4.0;
            /// <summary>Small spacing for compact layouts.</summary>
            public const double Sm =  8.0;
            /// <summary>Medium spacing for standard layouts.</summary>
            public const double Md = 12.0;
            /// <summary>Large spacing for section separation.</summary>
            public const double Lg = 18.0;
            /// <summary>Extra-large spacing for major section breaks.</summary>
            public const double Xl = 28.0;
        }

        // ====================================================================
        //  Layout helpers
        // ====================================================================

        /// <summary>
        /// Background bounds for a typical Arcanum block-entity config dialog.
        /// </summary>
        /// <returns>The arcanum config background bounds.</returns>
        public static ElementBounds ArcanumConfigBackgroundBounds()
        {
            var bg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bg.BothSizing = ElementSizing.FitToChildren;
            return bg;
        }

        /// <summary>
        /// Dialog bounds for a right-middle anchored Arcanum config dialog.
        /// </summary>
        /// <returns>The arcanum config dialog bounds.</returns>
        public static ElementBounds ArcanumConfigDialogBounds()
        {
            return ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-20, 0);
        }

        // ====================================================================
        //  Cairo drawing helpers
        // ====================================================================

        /// <summary>
        /// Trace a rounded rectangle path. Caller decides whether to fill / stroke.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        public static void RoundedRectPath(Context ctx, double x, double y, double w, double h, double r)
        {
            r = Math.Min(r, Math.Min(w, h) / 2.0);
            if (r <= 0.5)
            {
                ctx.Rectangle(x, y, w, h);
                return;
            }

            ctx.NewSubPath();
            ctx.Arc(x + w - r, y + r,         r, -Math.PI / 2.0,  0.0);
            ctx.Arc(x + w - r, y + h - r,     r,  0.0,            Math.PI / 2.0);
            ctx.Arc(x + r,     y + h - r,     r,  Math.PI / 2.0,  Math.PI);
            ctx.Arc(x + r,     y + r,         r,  Math.PI,        Math.PI * 1.5);
            ctx.ClosePath();
        }

        /// <summary>
        /// Filled rounded rectangle.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="color">The color value.</param>
        public static void FillRoundedRect(Context ctx, double x, double y, double w, double h, double r, RGBA color)
        {
            color.Apply(ctx);
            RoundedRectPath(ctx, x, y, w, h, r);
            ctx.Fill();
        }

        /// <summary>
        /// Filled rounded rectangle using a Cairo color.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="color">The color value.</param>
        public static void FillRoundedRect(Context ctx, double x, double y, double w, double h, double r, Color color)
        {
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            RoundedRectPath(ctx, x, y, w, h, r);
            ctx.Fill();
        }

        /// <summary>
        /// Stroked rounded rectangle (outline only).
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="color">The color value.</param>
        /// <param name="lineWidth">The line width value.</param>
        public static void StrokeRoundedRect(Context ctx, double x, double y, double w, double h, double r, RGBA color, double lineWidth)
        {
            color.Apply(ctx);
            ctx.LineWidth = lineWidth;
            RoundedRectPath(ctx, x, y, w, h, r);
            ctx.Stroke();
        }

        /// <summary>
        /// Stroked rounded rectangle using a Cairo color.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="color">The color value.</param>
        /// <param name="lineWidth">The line width value.</param>
        public static void StrokeRoundedRect(Context ctx, double x, double y, double w, double h, double r, Color color, double lineWidth)
        {
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.LineWidth = lineWidth;
            RoundedRectPath(ctx, x, y, w, h, r);
            ctx.Stroke();
        }

        /// <summary>
        /// Fills and strokes a standard Arcanum card for the supplied bounds.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="b">The b value.</param>
        /// <param name="fill">The fill value.</param>
        /// <param name="stroke">The stroke value.</param>
        /// <param name="lineWidth">The line width value.</param>
        public static void DrawCardBounds(Context ctx, ElementBounds b, RGBA fill, RGBA stroke, double lineWidth)
        {
            double r = GuiElement.scaled(ArcanumGuiTheme.Radius.Medium);
            double x = b.drawX, y = b.drawY, w = b.OuterWidth, h = b.OuterHeight;
            FillRoundedRect(ctx, x, y, w, h, r, fill);
            StrokeRoundedRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, r, stroke, lineWidth);
        }

        /// <summary>
        /// Vertical linear gradient fill of a rounded rectangle.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="top">The top value.</param>
        /// <param name="bottom">The bottom value.</param>
        public static void FillRoundedRectVerticalGradient(Context ctx, double x, double y, double w, double h, double r, RGBA top, RGBA bottom)
        {
            using var grad = new LinearGradient(x, y, x, y + h);
            grad.AddColorStop(0.0, new Color(top.R,    top.G,    top.B,    top.A));
            grad.AddColorStop(1.0, new Color(bottom.R, bottom.G, bottom.B, bottom.A));
            ctx.SetSource(grad);
            RoundedRectPath(ctx, x, y, w, h, r);
            ctx.Fill();
        }

        /// <summary>
        /// Horizontal linear gradient fill of a rounded rectangle.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        public static void FillRoundedRectHorizontalGradient(Context ctx, double x, double y, double w, double h, double r, RGBA left, RGBA right)
        {
            using var grad = new LinearGradient(x, y, x + w, y);
            grad.AddColorStop(0.0, new Color(left.R, left.G, left.B, left.A));
            grad.AddColorStop(1.0, new Color(right.R, right.G, right.B, right.A));
            ctx.SetSource(grad);
            RoundedRectPath(ctx, x, y, w, h, r);
            ctx.Fill();
        }

        /// <summary>
        /// Draws a standard Arcanum card background: vertical gradient fill plus a thin border.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="top">The top value.</param>
        /// <param name="bottom">The bottom value.</param>
        /// <param name="border">The border value.</param>
        /// <param name="lineWidth">The line width value.</param>
        public static void DrawCardBackground(Context ctx, double x, double y, double w, double h, double r, RGBA top, RGBA bottom, RGBA border, double lineWidth)
        {
            FillRoundedRectVerticalGradient(ctx, x, y, w, h, r, top, bottom);
            StrokeRoundedRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, r, border, lineWidth);
        }

        /// <summary>
        /// Draws a standard Arcanum card background with an additional inner border stroke.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="top">The top value.</param>
        /// <param name="bottom">The bottom value.</param>
        /// <param name="outerBorder">The outer border value.</param>
        /// <param name="outerLineWidth">The outer line width value.</param>
        /// <param name="innerBorder">The inner border value.</param>
        /// <param name="innerLineWidth">The inner line width value.</param>
        public static void DrawCardBackground(Context ctx, double x, double y, double w, double h, double r, RGBA top, RGBA bottom, RGBA outerBorder, double outerLineWidth, RGBA innerBorder, double innerLineWidth)
        {
            FillRoundedRectVerticalGradient(ctx, x, y, w, h, r, top, bottom);
            StrokeRoundedRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, r, outerBorder, outerLineWidth);
            double innerOffset = GuiElement.scaled(2.0);
            StrokeRoundedRect(ctx, x + innerOffset, y + innerOffset, w - innerOffset * 2, h - innerOffset * 2, Math.Max(1.0, r - innerOffset), innerBorder, innerLineWidth);
        }

        /// <summary>Performs the draw soft shadow operation.</summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="spread">The spread value.</param>
        /// <param name="maxAlpha">The max alpha value.</param>
        public static void DrawSoftShadow(Context ctx, double x, double y, double w, double h, double r, double spread, double maxAlpha)
        {
            // Reduced to 2 steps for profile FPS optimization. Still looks fine at UI scale.
            const int steps = 2;
            for (int i = steps; i >= 1; i--)
            {
                double t = i / (double)steps;
                double inflate = spread * t;
                double alpha = maxAlpha * (1.0 - t) * (1.0 - t);
                if (alpha <= 0.001) continue;

                ctx.SetSourceRGBA(0.0, 0.0, 0.0, alpha);
                RoundedRectPath(ctx, x - inflate, y - inflate, w + inflate * 2.0, h + inflate * 2.0, r + inflate);
                ctx.Fill();
            }
        }

        /// <summary>
        /// Soft glow halo around a rectangle - similar to shadow but uses a colour.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="spread">The spread value.</param>
        /// <param name="color">The color value.</param>
        public static void DrawGlow(Context ctx, double x, double y, double w, double h, double r, double spread, RGBA color)
        {
            const int steps = 2;
            for (int i = steps; i >= 1; i--)
            {
                double t = i / (double)steps;
                double inflate = spread * t;
                double alpha = color.A * (1.0 - t) * (1.0 - t);
                if (alpha <= 0.001) continue;

                ctx.SetSourceRGBA(color.R, color.G, color.B, alpha);
                RoundedRectPath(ctx, x - inflate, y - inflate, w + inflate * 2.0, h + inflate * 2.0, r + inflate);
                ctx.Fill();
            }
        }

        /// <summary>
        /// Filled circle.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="cx">The cx value.</param>
        /// <param name="cy">The cy value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="color">The color value.</param>
        public static void FillCircle(Context ctx, double cx, double cy, double r, RGBA color)
        {
            color.Apply(ctx);
            ctx.NewSubPath();
            ctx.Arc(cx, cy, r, 0.0, Math.PI * 2.0);
            ctx.Fill();
        }

        /// <summary>
        /// Stroked circle outline.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="cx">The cx value.</param>
        /// <param name="cy">The cy value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="color">The color value.</param>
        /// <param name="lineWidth">The line width value.</param>
        public static void StrokeCircle(Context ctx, double cx, double cy, double r, RGBA color, double lineWidth)
        {
            color.Apply(ctx);
            ctx.LineWidth = lineWidth;
            ctx.NewSubPath();
            ctx.Arc(cx, cy, r, 0.0, Math.PI * 2.0);
            ctx.Stroke();
        }

        /// <summary>
        /// One-pixel inner highlight along the top edge - the classic "glass" rim look.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="r">The r value.</param>
        /// <param name="alpha">The alpha value.</param>
        public static void DrawInnerHighlight(Context ctx, double x, double y, double w, double h, double r, double alpha = 0.10)
        {
            ctx.SetSourceRGBA(1.0, 1.0, 1.0, alpha);
            ctx.LineWidth = 1.0;
            ctx.NewSubPath();
            ctx.Arc(x + r,       y + r, r, Math.PI,         Math.PI * 1.5);
            ctx.Arc(x + w - r,   y + r, r, Math.PI * 1.5,   Math.PI * 2.0);
            ctx.Stroke();
        }

        /// <summary>
        /// Pixel-snap a coordinate - reduces blurry sub-pixel edges on rounded rects.
        /// </summary>
        /// <param name="v">The v value.</param>
        /// <returns>The snap.</returns>
        public static double Snap(double v) => Math.Round(v) + 0.5;

        // ====================================================================
        //  Decorative ornaments
        // ====================================================================

        /// <summary>
        /// Draw a small "L" corner ornament - silver inner stroke with a darker
        /// outer shadow.  Positions itself at the corner of (x,y,w,h).
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="size">The size.</param>
        /// <param name="color">The color value.</param>
        public static void DrawCornerOrnament(Context ctx, double x, double y, double w, double h, double size, RGBA color)
        {
            double inset = 6.0;
            ctx.LineWidth = 1.5;
            ctx.LineCap = LineCap.Round;

            // Top-left.
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.MoveTo(x + inset, y + inset + size);
            ctx.LineTo(x + inset, y + inset);
            ctx.LineTo(x + inset + size, y + inset);
            ctx.Stroke();

            // Top-right.
            ctx.MoveTo(x + w - inset - size, y + inset);
            ctx.LineTo(x + w - inset, y + inset);
            ctx.LineTo(x + w - inset, y + inset + size);
            ctx.Stroke();

            // Bottom-left.
            ctx.MoveTo(x + inset, y + h - inset - size);
            ctx.LineTo(x + inset, y + h - inset);
            ctx.LineTo(x + inset + size, y + h - inset);
            ctx.Stroke();

            // Bottom-right.
            ctx.MoveTo(x + w - inset - size, y + h - inset);
            ctx.LineTo(x + w - inset, y + h - inset);
            ctx.LineTo(x + w - inset, y + h - inset - size);
            ctx.Stroke();
        }

        /// <summary>
        /// Draw a horizontal divider with a silver line in the middle and a small
        /// diamond marker centered on it.  Looks like the dividers in the vanilla
        /// handbook / character creation screens.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="w">The w value.</param>
        /// <param name="color">The color value.</param>
        public static void DrawSilverDivider(Context ctx, double x, double y, double w, RGBA color)
        {
            // Line.
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.7);
            ctx.LineWidth = 1.0;
            ctx.MoveTo(x, y);
            ctx.LineTo(x + w, y);
            ctx.Stroke();

            // Center diamond.
            double cx = x + w / 2.0;
            double s = 3.0;
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.NewSubPath();
            ctx.MoveTo(cx,     y - s);
            ctx.LineTo(cx + s, y);
            ctx.LineTo(cx,     y + s);
            ctx.LineTo(cx - s, y);
            ctx.ClosePath();
            ctx.Fill();
        }
    }

}
