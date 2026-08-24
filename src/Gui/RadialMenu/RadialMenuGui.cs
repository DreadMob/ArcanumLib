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
        /// <summary>Gets a value indicating whether the prefers ungrabbed mouse is enabled.</summary>
        public override bool PrefersUngrabbedMouse => true;
        /// <summary>Returns a value indicating whether the operation should receive mouse events.</summary>
        /// <returns>true if the operation should receive mouse events; otherwise, false.</returns>
        public override bool ShouldReceiveMouseEvents() => IsOpened();
        /// <summary>Returns a value indicating whether the operation should receive keyboard events.</summary>
        /// <returns>true if the operation should receive keyboard events; otherwise, false.</returns>
        public override bool ShouldReceiveKeyboardEvents() => IsOpened();

        private readonly List<RadialMenuItem> _items;
        private float _outerRadius;
        private float _innerRadius;
        private float _iconRadius;
        private float _originalOuterRadius;
        private int _hoveredIndex = -1;

        /// <summary>Visual style for this menu, resolved from the style registry.</summary>
        public IRadialMenuStyle Style { get; set; }

        /// <summary>Gets the items.</summary>
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
            _iconRadius = (outerRadius + innerRadius) / 2f;
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

        private void Compose()
        {
            // Clamp radii to fit within the screen at the current GUI scale
            float screenWidth = (float)(capi.Render.FrameWidth / RuntimeEnv.GUIScale);
            float screenHeight = (float)(capi.Render.FrameHeight / RuntimeEnv.GUIScale);
            float maxDim = Math.Min(screenWidth, screenHeight);
            float maxOuter = (maxDim - 80f - TextAreaHeight) / 2f;
            if (maxOuter < _outerRadius && maxOuter > 40f)
            {
                float scale = maxOuter / _outerRadius;
                _outerRadius = maxOuter;
                _innerRadius *= scale;
                _iconRadius = (_outerRadius + _innerRadius) / 2f;
            }

            float size = _outerRadius * 2f + 40f;
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

            int count = _items.Count;
            if (count == 0) return;

            float cx = _outerRadius + 20f;
            float circleCY = _outerRadius + 20f;

            float anglePerItem = 2f * (float)Math.PI / count;
            float startAngle = -(float)Math.PI / 2f - anglePerItem / 2f;

            for (int i = 0; i < count; i++)
            {
                float a0 = startAngle + i * anglePerItem;
                float a1 = a0 + anglePerItem;
                bool hovered = i == _hoveredIndex;

                Style.DrawSector(ctx, cx, circleCY, a0, a1, hovered, _items[i].IsActive, _items[i].Disabled,
                    _outerRadius, _innerRadius);
                DrawSectorIcon(ctx, cx, circleCY, a0, a1, _items[i].Icon, _items[i].Disabled, _items[i].CustomIconDraw);
            }

            // Center cancel button
            Style.DrawCenterButton(ctx, cx, circleCY, _innerRadius);

            // Hover label + description below the circle
            if (_hoveredIndex >= 0 && _hoveredIndex < count)
            {
                DrawHoverText(ctx, cx, circleCY + _outerRadius + 14f, _items[_hoveredIndex]);
            }
        }

        private void DrawHoverText(Context ctx, float cx, float y, RadialMenuItem item)
        {
            float radiusScale = _outerRadius / _originalOuterRadius;
            float maxWidth = _outerRadius * 2f - 40f;

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

            if (!string.IsNullOrWhiteSpace(item.Description))
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

        private List<string> WrapText(Context ctx, string text, float maxWidth, int maxLines)
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

        private string TruncateToWidth(Context ctx, string text, float maxWidth)
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
