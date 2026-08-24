using System;
using Cairo;
using Vintagestory.API.Client;
using ArcanumLib.Gui.Theme;

namespace ArcanumLib.Gui.Icons
{
    /// <summary>
    /// Cairo vector icon renderer for custom tab decorations.
    /// Draws decorative elements (dividers, bullets, header symbols) using Cairo paths.
    /// Used by <see cref="GuiElementCustomTabContent"/> to render data-driven tab content.
    /// Consumers can choose which icons to use via decorPrefix localization keys.
    /// The generic glyphs (skull, hourglass, shield, crown, sword, rift) are also registered
    /// in <see cref="CustomIconRegistry"/> under <c>arcanum:&lt;name&gt;</c> keys so any mod
    /// can reference them without depending on this static class directly.
    /// </summary>
    public static class CustomTabIconRenderer
    {
        private static bool _registered;

        /// <summary>
        /// Registers the generic glyphs into <see cref="CustomIconRegistry"/>.
        /// Safe to call multiple times. Keys: <c>arcanum:skull</c>, <c>arcanum:hourglass</c>,
        /// <c>arcanum:shield</c>, <c>arcanum:crown</c>, <c>arcanum:sword</c>, <c>arcanum:rift</c>,
        /// <c>arcanum:star</c>, <c>arcanum:section-divider</c>, <c>arcanum:section-header</c>,
        /// <c>arcanum:entry-bullet</c>, <c>arcanum:sub-dot</c>.
        /// </summary>
        public static void RegisterGenericIcons()
        {
            if (_registered) return;
            _registered = true;
            CustomIconRegistry.Register("arcanum:skull", (ctx, cx, cy, r, color) => DrawSkullIcon(ctx, cx, cy, r, color));
            CustomIconRegistry.Register("arcanum:hourglass", (ctx, cx, cy, r, color) => DrawHourglassIcon(ctx, cx, cy, r, color));
            CustomIconRegistry.Register("arcanum:shield", (ctx, cx, cy, r, color) => DrawShieldIcon(ctx, cx, cy, r, color));
            CustomIconRegistry.Register("arcanum:crown", (ctx, cx, cy, r, color) => DrawCrownIcon(ctx, cx, cy, r, color));
            CustomIconRegistry.Register("arcanum:sword", (ctx, cx, cy, r, color) => DrawSwordIcon(ctx, cx, cy, r, color));
            CustomIconRegistry.Register("arcanum:rift", (ctx, cx, cy, r, color) => DrawRiftIcon(ctx, cx, cy, r, color));
            CustomIconRegistry.Register("arcanum:star", (ctx, cx, cy, r, color) => DrawActiveStar(ctx, cx, cy, r, color));
        }
        /// <summary>
        /// Draw a decorative section divider — horizontal line with a centered diamond ornament.
        /// </summary>
        public static void DrawSectionDivider(Context ctx, double x, double y, double w, RGBA color)
        {
            double s = GuiElement.scaled(3.5);
            double cx = x + w / 2.0;
            double lineY = y;

            // Faded line segments
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.25);
            ctx.LineWidth = 1.0;
            ctx.MoveTo(x, lineY);
            ctx.LineTo(cx - s * 2.5, lineY);
            ctx.Stroke();
            ctx.MoveTo(cx + s * 2.5, lineY);
            ctx.LineTo(x + w, lineY);
            ctx.Stroke();

            // Center diamond ornament
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.7);
            ctx.MoveTo(cx, lineY - s);
            ctx.LineTo(cx + s, lineY);
            ctx.LineTo(cx, lineY + s);
            ctx.LineTo(cx - s, lineY);
            ctx.ClosePath();
            ctx.Fill();

            // Inner highlight dot
            ctx.SetSourceRGBA(1.0, 0.95, 0.75, color.A * 0.4);
            ctx.Arc(cx, lineY, s * 0.35, 0, Math.PI * 2);
            ctx.Fill();
        }

        /// <summary>
        /// Draw a small diamond/rune symbol before a section header title.
        /// </summary>
        public static void DrawSectionHeaderIcon(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            // Outer diamond
            ctx.MoveTo(cx, cy - s * 0.6);
            ctx.LineTo(cx + s * 0.6, cy);
            ctx.LineTo(cx, cy + s * 0.6);
            ctx.LineTo(cx - s * 0.6, cy);
            ctx.ClosePath();
            ctx.Stroke();

            // Inner dot
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.6);
            ctx.Arc(cx, cy, s * 0.18, 0, Math.PI * 2);
            ctx.Fill();
        }

        /// <summary>
        /// Draw a small arrow/bullet marker for normal entries.
        /// </summary>
        public static void DrawEntryBullet(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.8);
            ctx.LineWidth = s * 0.12;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Small chevron arrow
            ctx.MoveTo(cx - s * 0.25, cy - s * 0.30);
            ctx.LineTo(cx + s * 0.20, cy);
            ctx.LineTo(cx - s * 0.25, cy + s * 0.30);
            ctx.Stroke();
        }

        /// <summary>
        /// Draw a star marker for active entries.
        /// </summary>
        public static void DrawActiveStar(Context ctx, double cx, double cy, double r, RGBA color)
        {
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            int spikes = 5;
            int count = spikes * 2;
            for (int j = 0; j < count; j++)
            {
                double angle = j * Math.PI / spikes - Math.PI / 2;
                double radius = (j % 2 == 0) ? r : r * 0.42;
                double px = cx + Math.Cos(angle) * radius;
                double py = cy + Math.Sin(angle) * radius;
                if (j == 0) ctx.MoveTo(px, py);
                else ctx.LineTo(px, py);
            }
            ctx.ClosePath();
            ctx.Fill();
        }

        /// <summary>
        /// Draw a small dot for sub-item indentation.
        /// </summary>
        public static void DrawSubDot(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.5);
            ctx.Arc(cx, cy, s * 0.15, 0, Math.PI * 2);
            ctx.Fill();
        }

        /// <summary>
        /// Draw a rift/void crack icon — suitable for an "About" section.
        /// A jagged vertical crack with small branches.
        /// </summary>
        public static void DrawRiftIcon(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.Save();
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.LineWidth = s * 0.06;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Main crack
            ctx.MoveTo(cx - s * 0.10, cy - s * 0.55);
            ctx.LineTo(cx + s * 0.15, cy - s * 0.20);
            ctx.LineTo(cx - s * 0.08, cy + s * 0.10);
            ctx.LineTo(cx + s * 0.12, cy + s * 0.55);
            ctx.Stroke();

            // Small branches
            ctx.MoveTo(cx + s * 0.15, cy - s * 0.20);
            ctx.LineTo(cx + s * 0.40, cy - s * 0.30);
            ctx.Stroke();

            ctx.MoveTo(cx - s * 0.08, cy + s * 0.10);
            ctx.LineTo(cx - s * 0.35, cy + s * 0.05);
            ctx.Stroke();

            ctx.MoveTo(cx + s * 0.12, cy + s * 0.55);
            ctx.LineTo(cx + s * 0.35, cy + s * 0.45);
            ctx.Stroke();

            ctx.Restore();
        }

        /// <summary>
        /// Draw a shield icon — a shield outline with a vertical line.
        /// </summary>
        public static void DrawShieldIcon(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.Save();
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.LineWidth = s * 0.07;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Shield outline
            ctx.MoveTo(cx, cy - s * 0.55);
            ctx.LineTo(cx + s * 0.40, cy - s * 0.35);
            ctx.LineTo(cx + s * 0.40, cy + s * 0.15);
            ctx.CurveTo(cx + s * 0.40, cy + s * 0.45, cx + s * 0.20, cy + s * 0.55, cx, cy + s * 0.60);
            ctx.CurveTo(cx - s * 0.20, cy + s * 0.55, cx - s * 0.40, cy + s * 0.45, cx - s * 0.40, cy + s * 0.15);
            ctx.LineTo(cx - s * 0.40, cy - s * 0.35);
            ctx.ClosePath();
            ctx.Stroke();

            // Inner vertical line
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.6);
            ctx.MoveTo(cx, cy - s * 0.35);
            ctx.LineTo(cx, cy + s * 0.40);
            ctx.Stroke();

            ctx.Restore();
        }

        /// <summary>
        /// Draw a crown/rank icon — a simple crown shape.
        /// </summary>
        public static void DrawCrownIcon(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.Save();
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.LineWidth = s * 0.07;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Crown base
            ctx.MoveTo(cx - s * 0.45, cy + s * 0.35);
            ctx.LineTo(cx + s * 0.45, cy + s * 0.35);
            ctx.Stroke();

            // Crown spikes
            ctx.MoveTo(cx - s * 0.45, cy + s * 0.35);
            ctx.LineTo(cx - s * 0.35, cy - s * 0.40);
            ctx.LineTo(cx - s * 0.18, cy - s * 0.10);
            ctx.LineTo(cx, cy - s * 0.50);
            ctx.LineTo(cx + s * 0.18, cy - s * 0.10);
            ctx.LineTo(cx + s * 0.35, cy - s * 0.40);
            ctx.LineTo(cx + s * 0.45, cy + s * 0.35);
            ctx.Stroke();

            // Base fill
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.3);
            ctx.MoveTo(cx - s * 0.45, cy + s * 0.35);
            ctx.LineTo(cx + s * 0.45, cy + s * 0.35);
            ctx.LineTo(cx + s * 0.40, cy + s * 0.50);
            ctx.LineTo(cx - s * 0.40, cy + s * 0.50);
            ctx.ClosePath();
            ctx.Fill();

            ctx.Restore();
        }

        /// <summary>
        /// Draw a skull/danger icon.
        /// </summary>
        public static void DrawSkullIcon(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.Save();
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.LineWidth = s * 0.06;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Skull dome
            ctx.Arc(cx, cy - s * 0.10, s * 0.40, Math.PI, 0);
            ctx.LineTo(cx + s * 0.35, cy + s * 0.20);
            ctx.LineTo(cx + s * 0.20, cy + s * 0.20);
            ctx.LineTo(cx + s * 0.15, cy + s * 0.40);
            ctx.LineTo(cx - s * 0.15, cy + s * 0.40);
            ctx.LineTo(cx - s * 0.20, cy + s * 0.20);
            ctx.LineTo(cx - s * 0.35, cy + s * 0.20);
            ctx.ClosePath();
            ctx.Stroke();

            // Eye sockets
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.7);
            ctx.Arc(cx - s * 0.16, cy - s * 0.05, s * 0.10, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Arc(cx + s * 0.16, cy - s * 0.05, s * 0.10, 0, Math.PI * 2);
            ctx.Fill();

            ctx.Restore();
        }

        /// <summary>
        /// Draw an hourglass/rotation icon.
        /// </summary>
        public static void DrawHourglassIcon(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.Save();
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.LineWidth = s * 0.07;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Top and bottom bars
            ctx.MoveTo(cx - s * 0.35, cy - s * 0.50);
            ctx.LineTo(cx + s * 0.35, cy - s * 0.50);
            ctx.Stroke();
            ctx.MoveTo(cx - s * 0.35, cy + s * 0.50);
            ctx.LineTo(cx + s * 0.35, cy + s * 0.50);
            ctx.Stroke();

            // Sand chamber
            ctx.MoveTo(cx - s * 0.35, cy - s * 0.50);
            ctx.LineTo(cx + s * 0.35, cy - s * 0.50);
            ctx.LineTo(cx - s * 0.35, cy + s * 0.50);
            ctx.LineTo(cx + s * 0.35, cy + s * 0.50);
            ctx.ClosePath();
            ctx.Stroke();

            // Sand falling
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A * 0.5);
            ctx.MoveTo(cx, cy - s * 0.15);
            ctx.LineTo(cx, cy + s * 0.15);
            ctx.Stroke();

            ctx.Restore();
        }

        /// <summary>
        /// Draw a challenge/sword icon.
        /// </summary>
        public static void DrawSwordIcon(Context ctx, double cx, double cy, double s, RGBA color)
        {
            ctx.Save();
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.LineWidth = s * 0.06;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Blade
            ctx.MoveTo(cx + s * 0.30, cy - s * 0.50);
            ctx.LineTo(cx - s * 0.20, cy + s * 0.20);
            ctx.Stroke();

            // Crossguard
            ctx.MoveTo(cx - s * 0.35, cy + s * 0.05);
            ctx.LineTo(cx - s * 0.05, cy + s * 0.05);
            ctx.Stroke();

            // Handle
            ctx.MoveTo(cx - s * 0.20, cy + s * 0.20);
            ctx.LineTo(cx - s * 0.35, cy + s * 0.45);
            ctx.Stroke();

            // Pommel
            ctx.Arc(cx - s * 0.35, cy + s * 0.45, s * 0.06, 0, Math.PI * 2);
            ctx.Fill();

            ctx.Restore();
        }
    }
}
