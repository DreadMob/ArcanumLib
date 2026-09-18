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
        // Palette slot - swap to retheme the whole toolkit.
        // All color accessors read through <see cref="Palette" />; controls keep
        // working unchanged. Mods assign once at client startup (or snapshot it
        // for per-dialog themes via <see cref="ArcanumGuiDialog" /> overloads).
        // --------------------------------------------------------------------
        /// <summary>The active palette. Defaults to the vanilla-brown look.</summary>
        public static GuiThemePalette Palette = GuiThemePalette.Vanilla;

        /// <summary>
        /// Temporarily installs <paramref name="palette" /> as the active palette until the
        /// returned <see cref="PaletteScope" /> is disposed. Intended for
        /// <c>using (ArcanumGuiTheme.WithPalette(p)) { ... }</c> blocks around composer
        /// or dialog construction. Scopes nest and restore in LIFO order.
        /// </summary>
        /// <param name="palette">The palette to make active.</param>
        /// <returns>A scope that restores the previous palette on dispose.</returns>
        public static PaletteScope WithPalette(GuiThemePalette palette) => new(palette);

        // --------------------------------------------------------------------
        // Surface palette - vanilla brown with a slightly warmer parchment feel
        // so the toolkit reads as part of the base game, not a different mod.
        // --------------------------------------------------------------------
        /// <summary>The surface deepest value.</summary>
        public static RGBA SurfaceDeepest    => Palette.SurfaceDeepest;
        /// <summary>The surface base value.</summary>
        public static RGBA SurfaceBase       => Palette.SurfaceBase;
        /// <summary>The surface elevated value.</summary>
        public static RGBA SurfaceElevated   => Palette.SurfaceElevated;
        /// <summary>The surface card value.</summary>
        public static RGBA SurfaceCard       => Palette.SurfaceCard;
        /// <summary>The surface card hover value.</summary>
        public static RGBA SurfaceCardHover  => Palette.SurfaceCardHover;
        /// <summary>The surface card active value.</summary>
        public static RGBA SurfaceCardActive => Palette.SurfaceCardActive;

        // --------------------------------------------------------------------
        // Border / divider palette.  We layer a dark inner shadow + a silvered
        // outer rim - same recipe vanilla uses on every dialog.
        // --------------------------------------------------------------------
        /// <summary>The border shadow value.</summary>
        public static RGBA BorderShadow       => Palette.BorderShadow;
        /// <summary>The border subtle value.</summary>
        public static RGBA BorderSubtle       => Palette.BorderSubtle;
        /// <summary>The border default value.</summary>
        public static RGBA BorderDefault      => Palette.BorderDefault;
        /// <summary>The border strong value.</summary>
        public static RGBA BorderStrong       => Palette.BorderStrong;
        /// <summary>The border silver value.</summary>
        public static RGBA BorderSilver       => Palette.BorderSilver;
        /// <summary>The border silver bright value.</summary>
        public static RGBA BorderSilverBright => Palette.BorderSilverBright;

        // --------------------------------------------------------------------
        // Accent (vanilla "active button" copper) and a warm highlight.
        // --------------------------------------------------------------------
        /// <summary>Full-opacity copper accent used for active buttons and emphasis.</summary>
        public static RGBA Accent       => Palette.Accent;
        /// <summary>Soft copper accent at 40% opacity for subtle highlights.</summary>
        public static RGBA AccentSoft   => Palette.AccentSoft;
        /// <summary>Dim copper accent at 18% opacity for backgrounds and hovers.</summary>
        public static RGBA AccentDim    => Palette.AccentDim;
        /// <summary>Brightened copper accent for hover and focus states.</summary>
        public static RGBA AccentBright => Palette.AccentBright;
        /// <summary>Warm parchment highlight used for separators and subtle borders.</summary>
        public static RGBA Highlight    => Palette.Highlight;

        // --------------------------------------------------------------------
        // Status palette - tuned to read against the brown surface.
        // --------------------------------------------------------------------
        /// <summary>Copper status color for available actions.</summary>
        public static RGBA StatusAvailable => Palette.StatusAvailable;
        /// <summary>Pale steel-blue status color for active or in-progress states.</summary>
        public static RGBA StatusActive    => Palette.StatusActive;
        /// <summary>Muted leaf-green status color for completed states.</summary>
        public static RGBA StatusComplete  => Palette.StatusComplete;
        /// <summary>Dim parchment status color for locked states.</summary>
        public static RGBA StatusLocked    => Palette.StatusLocked;
        /// <summary>Gray parchment status color for cooldown states.</summary>
        public static RGBA StatusCooldown  => Palette.StatusCooldown;
        /// <summary>Muted iron-rust status color for failed states.</summary>
        public static RGBA StatusFailed    => Palette.StatusFailed;

        // --------------------------------------------------------------------
        // Text palette - vanilla parchment cream as the base.
        // --------------------------------------------------------------------
        /// <summary>Primary text color, high-contrast parchment cream.</summary>
        public static RGBA TextPrimary   => Palette.TextPrimary;
        /// <summary>Secondary text color for labels and supporting text.</summary>
        public static RGBA TextSecondary => Palette.TextSecondary;
        /// <summary>Muted text color for hints and tertiary information.</summary>
        public static RGBA TextMuted     => Palette.TextMuted;
        /// <summary>Disabled text color for unavailable controls.</summary>
        public static RGBA TextDisabled  => Palette.TextDisabled;

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

        // ====================================================================
        //  Rivets / fasteners — used by ornate (steampunk) decorations.
        // ====================================================================

        /// <summary>
        /// Draws a single dome rivet: dark base circle, lighter dome, tiny
        /// specular dot at top-left. Reads as a screw/bolt head.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="cx">Center X.</param>
        /// <param name="cy">Center Y.</param>
        /// <param name="r">Rivet radius.</param>
        /// <param name="metal">Metal base color.</param>
        public static void DrawRivet(Context ctx, double cx, double cy, double r, RGBA metal)
        {
            // Outer dark ring.
            FillCircle(ctx, cx, cy, r, metal.WithAlpha(metal.A * 0.55).Lerp(new RGBA(0, 0, 0, 1), 0.55));
            // Dome.
            FillCircle(ctx, cx, cy, r * 0.72, metal);
            // Specular glint top-left.
            FillCircle(ctx, cx - r * 0.24, cy - r * 0.26, r * 0.24,
                metal.Lerp(new RGBA(1, 1, 1, 1), 0.55).WithAlpha(metal.A));
        }

        /// <summary>
        /// Draws rivets along a horizontal strip (e.g. a frame edge).
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Center Y of the rivet row.</param>
        /// <param name="w">Strip width.</param>
        /// <param name="r">Rivet radius.</param>
        /// <param name="spacing">Gap between rivet centers; 0 → evenly distribute.</param>
        /// <param name="metal">Metal base color.</param>
        public static void DrawRivetRow(Context ctx, double x, double y, double w, double r, double spacing, RGBA metal)
        {
            if (w < r * 2) return;
            int count = spacing > 0 ? (int)(w / spacing) + 1 : Math.Max(2, (int)(w / (r * 5)));
            if (count < 2) count = 2;
            double step = w / (count - 1);
            for (int i = 0; i < count; i++) DrawRivet(ctx, x + i * step, y, r, metal);
        }

        /// <summary>
        /// Draws rivets in all four corners of a rect — the classic frame fastener look.
        /// </summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <param name="w">The w value.</param>
        /// <param name="h">The h value.</param>
        /// <param name="inset">Distance of rivet centers from the rect edge.</param>
        /// <param name="r">Rivet radius.</param>
        /// <param name="metal">Metal base color.</param>
        public static void DrawCornerRivets(Context ctx, double x, double y, double w, double h, double inset, double r, RGBA metal)
        {
            DrawRivet(ctx, x + inset,     y + inset,     r, metal);
            DrawRivet(ctx, x + w - inset, y + inset,     r, metal);
            DrawRivet(ctx, x + inset,     y + h - inset, r, metal);
            DrawRivet(ctx, x + w - inset, y + h - inset, r, metal);
        }

        /// <summary>
        /// Draws an analog dial gauge centered at (cx, cy): brass bezel, ticked
        /// 270°-sweep arc, needle at <paramref name="value" /> (0..1), caption
        /// under the hub. <paramref name="uiScale" /> multiplies all thickness/offset
        /// tokens so callers can render at any resolution.
        /// </summary>
        public static void DrawDialGauge(Context ctx, double cx, double cy, double r, double value, string caption, double uiScale, RGBA? needleColor = null)
        {
            var p = Palette;
            double s(double v) => v * uiScale;
            value = Math.Clamp(value, 0, 1);

            // Bezel: brass ring + dark face.
            FillCircle(ctx, cx, cy, r, p.AccentSoft);
            FillCircle(ctx, cx, cy, r - s(2.5), p.SurfaceDeepest);
            StrokeCircle(ctx, cx, cy, r - s(2.5), p.BorderSilver.WithAlpha(0.5), s(0.8));

            // Tick arc: 270° sweep from bottom-left to bottom-right.
            double a0 = Math.PI * 0.75, a1 = Math.PI * 2.25;
            for (int i = 0; i <= 10; i++)
            {
                double a = a0 + (a1 - a0) * i / 10.0;
                double ri = r - s(5.5), ro = r - s(3.0);
                ctx.SetSourceRGBA(p.TextSecondary.R, p.TextSecondary.G, p.TextSecondary.B, 0.8);
                ctx.LineWidth = i % 5 == 0 ? s(1.4) : s(0.7);
                ctx.MoveTo(cx + Math.Cos(a) * ri, cy + Math.Sin(a) * ri);
                ctx.LineTo(cx + Math.Cos(a) * ro, cy + Math.Sin(a) * ro);
                ctx.Stroke();
            }

            // Needle.
            double na = a0 + (a1 - a0) * value;
            var nc = needleColor ?? p.Accent;
            ctx.SetSourceRGBA(nc.R, nc.G, nc.B, nc.A);
            ctx.LineWidth = s(1.6);
            ctx.LineCap = LineCap.Round;
            ctx.MoveTo(cx, cy);
            ctx.LineTo(cx + Math.Cos(na) * (r - s(6)), cy + Math.Sin(na) * (r - s(6)));
            ctx.Stroke();
            FillCircle(ctx, cx, cy, s(2.2), p.Accent);

            // Caption under center.
            if (!string.IsNullOrEmpty(caption))
            {
                ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
                ctx.SetFontSize(s(7.5));
                var ext = ctx.TextExtents(caption);
                ctx.MoveTo(cx - ext.Width / 2 - ext.XBearing, cy + r * 0.55);
                ctx.SetSourceRGBA(p.TextMuted.R, p.TextMuted.G, p.TextMuted.B, 0.9);
                ctx.ShowText(caption);
            }
        }

        /// <summary>
        /// Draws a lever-style toggle plate: recessed slot + sliding brass knob.
        /// </summary>
        public static void DrawTogglePlate(Context ctx, double x, double y, double w, double h, bool isOn, double uiScale)
        {
            var p = Palette;
            double s(double v) => v * uiScale;
            double r = h / 2;

            FillRoundedRectVerticalGradient(ctx, x, y, w, h, r, p.SurfaceDeepest, p.SurfaceBase);
            StrokeRoundedRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, r, p.BorderDefault, s(1));

            if (isOn)
            {
                ctx.Save();
                RoundedRectPath(ctx, x + w / 2, y, w / 2, h, r);
                ctx.Clip();
                FillRoundedRect(ctx, x, y, w, h, r, p.Accent.WithAlpha(0.35));
                ctx.Restore();
            }

            double kr = h / 2 - s(2.5);
            double kx = isOn ? x + w - h / 2 : x + h / 2;
            FillCircle(ctx, kx, y + h / 2, kr, p.Accent);
            StrokeCircle(ctx, kx, y + h / 2, kr, p.BorderShadow, s(1));
            FillCircle(ctx, kx - kr * 0.25, y + h / 2 - kr * 0.3, kr * 0.28, p.AccentBright.WithAlpha(0.9));
        }
    }

}
