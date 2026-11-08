using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.RadialMenu
{
    /// <summary>
    /// Generic Cairo-styled radial (pie) menu. Sectors are arranged in a circle
    /// around a central cancel button. Each sector has an icon, label, and hover state.
    /// Visual appearance is controlled by an <see cref="IRadialMenuStyle" /> resolved
    /// from <see cref="RadialMenuStyleRegistry" /> via <see cref="SetStyle" />.
    /// </summary>
    public partial class RadialMenuGui : GuiDialog
    {
        /// <summary>Gets the toggle key combination code.</summary>
        public override string? ToggleKeyCombinationCode => null;
        /// <summary>Gets the draw order.</summary>
        public override double DrawOrder => 1.0;
        /// <summary>Radial menus always request an ungrabbed mouse so the cursor can roam freely.</summary>
        public override bool PrefersUngrabbedMouse => true;
        /// <summary>Returns whether the menu should receive mouse events (only when open).</summary>
        /// <returns><c>true</c> if the menu is open; otherwise <c>false</c>.</returns>
        public override bool ShouldReceiveMouseEvents() => IsOpened();
        /// <summary>Returns whether the menu should receive keyboard events (only when open).</summary>
        /// <returns><c>true</c> if the menu is open; otherwise <c>false</c>.</returns>
        public override bool ShouldReceiveKeyboardEvents() => IsOpened();

        private readonly List<RadialMenuItem> _items;
        private float _outerRadius;
        private float _innerRadius;
        private float _originalOuterRadius;
        private int _hoveredIndex = -1;
        /// <summary>Hovered segment of the open sub-arc ring, or -1.</summary>
        private int _hoveredArc = -1;
        /// <summary>Sector whose sub-arc ring is open, or -1.</summary>
        private int _expandedIndex = -1;

        /// <summary>Visual style for this menu, resolved from the style registry.</summary>
        public IRadialMenuStyle Style { get; set; }

        /// <summary>The items displayed in the radial menu.</summary>
        protected List<RadialMenuItem> Items => _items;

        private GlKeys? _holdKey;

        /// <summary>
        /// Creates a radial menu with the given items and radii.
        /// The style defaults to <c>"default"</c>; call <see cref="SetStyle" /> to change it.
        /// </summary>
        /// <param name="capi">Client API.</param>
        /// <param name="title">Unused for rendering, kept for compatibility.</param>
        /// <param name="items">Sector items.</param>
        /// <param name="outerRadius">Outer radius of the radial circle.</param>
        /// <param name="innerRadius">Inner radius (center button area).</param>
        public RadialMenuGui(ICoreClientAPI capi, string title, List<RadialMenuItem> items,
            float outerRadius = 220f, float innerRadius = 48f) : base(capi)
        {
            _items = items ?? new List<RadialMenuItem>();
            _outerRadius = outerRadius;
            _originalOuterRadius = outerRadius;
            _innerRadius = innerRadius;
            Style = RadialMenuStyleRegistry.GetOrDefault("default");
        }

        /// <summary>Sets the hold-to-activate key for the radial menu.</summary>
        /// <param name="key">The key to look up.</param>
        public void SetHoldKey(GlKeys key) => _holdKey = key;

        /// <summary>Sets the visual style by registry key.</summary>
        /// <param name="key">The key to look up.</param>
        public void SetStyle(string key) => Style = RadialMenuStyleRegistry.GetOrDefault(key);

        /// <summary>Performs the on gui opened operation.</summary>
        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        private const float LabelFontSize = 34f;
        private const float DescriptionFontSize = 26f;
        private const float TextAreaHeight = 120f;
        private const int MaxDescriptionLines = 3;
        private const float LineSpacing = 4f;

        /// <summary>Room around the wheel: the drop shadow, and the sub-arc ring when any sector has one.</summary>
        private float Margin => MarginFor(_items);

        private static float MarginFor(List<RadialMenuItem> items)
            => 20f + (items.Exists(i => i.Arcs?.Count > 0) ? RadialMenuLayout.ArcGap + RadialMenuLayout.ArcWidth : 0f);

        private void Compose()
        {
            // Clamp radii to fit within the screen at the current GUI scale
            float screenWidth = (float)(capi.Render.FrameWidth / RuntimeEnv.GUIScale);
            float screenHeight = (float)(capi.Render.FrameHeight / RuntimeEnv.GUIScale);
            float maxDim = Math.Min(screenWidth, screenHeight);
            float maxOuter = (maxDim - 80f - TextAreaHeight) / 2f - (Margin - 20f);
            if (maxOuter < _outerRadius && maxOuter > 40f)
            {
                float scale = maxOuter / _outerRadius;
                _outerRadius = maxOuter;
                _innerRadius *= scale;
            }

            float size = (_outerRadius + Margin) * 2f;
            var dialogBounds = ElementBounds.Fixed(0, 0, size, size + TextAreaHeight)
                .WithAlignment(EnumDialogArea.CenterMiddle);
            var drawBounds = ElementBounds.Fixed(0, 0, size, size + TextAreaHeight);

            SingleComposer = capi.Gui
                .CreateCompo("radialmenu-" + Guid.NewGuid().ToString("N"), dialogBounds)
                .AddStaticCustomDraw(drawBounds, OnDraw)
                .Compose();
        }

        private void OnDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            float scale = (float)RuntimeEnv.GUIScale;
            ctx.Scale(scale, scale);
            Paint(ctx, Style, _items, _outerRadius, _innerRadius, _outerRadius / _originalOuterRadius,
                _hoveredIndex, _hoveredArc, _expandedIndex);
        }

        /// <summary>
        /// Draws a wheel the way the open menu does, onto any Cairo context (unscaled units, the centre at
        /// outer radius + margin from the top left): previews and tests use it without the game.
        /// </summary>
        /// <param name="ctx">Cairo context.</param>
        /// <param name="style">Visual style.</param>
        /// <param name="items">Sector items.</param>
        /// <param name="outerRadius">Outer radius of the wheel.</param>
        /// <param name="innerRadius">Inner radius (center button).</param>
        /// <param name="radiusScale">Text scale (1 at the authored radius).</param>
        /// <param name="hovered">Hovered sector, -1 none, -2 the center.</param>
        /// <param name="hoveredArc">Hovered segment of the open ring, or -1.</param>
        /// <param name="expanded">Sector whose sub-arc ring is open, or -1.</param>
        public static void Paint(Context ctx, IRadialMenuStyle style, List<RadialMenuItem> items, float outerRadius, float innerRadius,
            float radiusScale, int hovered, int hoveredArc, int expanded)
        {
            int count = items.Count;
            if (count == 0) return;

            float margin = MarginFor(items);
            float cx = outerRadius + margin;
            float circleCY = outerRadius + margin;
            var layout = new RadialMenuLayout(count, outerRadius, innerRadius);
            float iconRadius = (outerRadius + innerRadius) / 2f;

            for (int i = 0; i < count; i++)
            {
                float a0 = layout.SectorStart(i);
                float a1 = a0 + layout.AnglePerItem;
                // the sector stays lit while its ring is open, so the needle keeps pointing at the device
                bool lit = (i == hovered && hoveredArc < 0) || i == expanded;

                style.DrawSector(ctx, cx, circleCY, a0, a1, lit, items[i].IsActive, items[i].Disabled,
                    outerRadius, innerRadius);
                DrawSectorIcon(ctx, style, cx, circleCY, a0, a1, iconRadius, 20f, items[i].Icon, items[i].Disabled, items[i].CustomIconDraw);
            }

            if (expanded >= 0 && expanded < count && items[expanded].Arcs is { Count: > 0 } arcs)
            {
                float w = layout.ArcAngle(arcs.Count);
                float arcIconRadius = (layout.ArcInner + layout.ArcOuter) / 2f;
                for (int k = 0; k < arcs.Count; k++)
                {
                    float a0 = layout.ArcStart(expanded, arcs.Count, k);
                    float a1 = a0 + w;
                    bool hov = hovered == expanded && hoveredArc == k;
                    if (style is IRadialMenuArcStyle arcStyle)
                        arcStyle.DrawArc(ctx, cx, circleCY, a0, a1, hov, arcs[k].IsActive, arcs[k].Disabled, layout.ArcOuter, layout.ArcInner);
                    else
                        style.DrawSector(ctx, cx, circleCY, a0, a1, hov, arcs[k].IsActive, arcs[k].Disabled, layout.ArcOuter, layout.ArcInner);
                    DrawSectorIcon(ctx, style, cx, circleCY, a0, a1, arcIconRadius, 15f, arcs[k].Icon, arcs[k].Disabled, arcs[k].CustomIconDraw);
                }
            }

            // Center cancel button
            style.DrawCenterButton(ctx, cx, circleCY, innerRadius);

            // Hover label + description below the circle
            if (hovered >= 0 && hovered < count)
            {
                var item = items[hovered];
                if (hoveredArc >= 0 && item.Arcs != null && hoveredArc < item.Arcs.Count) item = item.Arcs[hoveredArc];
                DrawHoverText(ctx, cx, circleCY + outerRadius + margin - 6f, item, outerRadius, radiusScale);
            }
        }

        private static void DrawHoverText(Context ctx, float cx, float y, RadialMenuItem item, float outerRadius, float radiusScale)
        {
            float maxWidth = outerRadius * 2f - 40f;

            if (!string.IsNullOrWhiteSpace(item.Label))
            {
                ctx.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Bold);
                ctx.SetFontSize(LabelFontSize * radiusScale);
                ctx.SetSourceRGBA(0.95f, 0.92f, 0.88f, 1.0f);
                string label = TruncateToWidth(ctx, item.Label, maxWidth);
                var ext = ctx.TextExtents(label);
                ctx.MoveTo(cx - ext.Width / 2f - ext.XBearing, y - ext.YBearing);
                ctx.ShowText(label);
                y += (float)ext.Height + LineSpacing;
            }

            // a description that only repeats the label is noise — show it only when it says something more
            if (!string.IsNullOrWhiteSpace(item.Description) && !string.Equals(item.Description.Trim(), item.Label?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                ctx.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Normal);
                ctx.SetFontSize(DescriptionFontSize * radiusScale);
                ctx.SetSourceRGBA(0.77f, 0.74f, 0.70f, 0.90f);
                var lines = WrapText(ctx, item.Description, maxWidth, MaxDescriptionLines);
                foreach (var line in lines)
                {
                    var ext = ctx.TextExtents(line);
                    ctx.MoveTo(cx - ext.Width / 2f - ext.XBearing, y - ext.YBearing);
                    ctx.ShowText(line);
                    y += (float)ext.Height + LineSpacing;
                }
            }
        }

        private static List<string> WrapText(Context ctx, string text, float maxWidth, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
                return lines;

            string[] words = text.Split(' ');
            string current = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                var ext = ctx.TextExtents(candidate);
                if (ext.Width <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = word;
                    if (lines.Count >= maxLines)
                        return lines;
                }
                else
                {
                    current = TruncateToWidth(ctx, word, maxWidth);
                    lines.Add(current);
                    current = string.Empty;
                    if (lines.Count >= maxLines)
                        return lines;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                if (lines.Count < maxLines)
                    lines.Add(current);
                else if (lines.Count > 0)
                    lines[lines.Count - 1] = TruncateToWidth(ctx, lines[lines.Count - 1] + " " + current, maxWidth);
            }

            return lines;
        }

        private static string TruncateToWidth(Context ctx, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var ext = ctx.TextExtents(text);
            if (ext.Width <= maxWidth)
                return text;

            const string ellipsis = "...";
            for (int i = text.Length - 1; i >= 1; i--)
            {
                string candidate = text.Substring(0, i) + ellipsis;
                if (ctx.TextExtents(candidate).Width <= maxWidth)
                    return candidate;
            }

            return string.Empty;
        }
    }
}
