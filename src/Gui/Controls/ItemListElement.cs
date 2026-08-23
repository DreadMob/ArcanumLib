using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.Theme;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.Gui.Controls
{
    /// <summary>
    /// Status of an item row in the list.
    /// </summary>
    public enum ItemRowStatus
    {
        Locked,
        Available,
        Owned
    }

    /// <summary>
    /// Data for a single row in the item list.
    /// </summary>
    public class ItemListRow
    {
        public string? Id { get; set; }
        public string? IconItemCode { get; set; }
        public string? IconUcontents { get; set; }
        public string? CustomIconKey { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public ItemRowStatus Status { get; set; }
        /// <summary>Optional tooltip text (rich text) shown on hover. If null/empty, no tooltip.</summary>
        public string? TooltipText { get; set; }
        /// <summary>Optional hex color for a highlight border around this row. If null/empty, no border.</summary>
        public string? BorderColor { get; set; }
        /// <summary>Thickness of the highlight border. Default 2.</summary>
        public double BorderThickness { get; set; } = 2.0;
    }

    /// <summary>
    /// A reusable vertical list GUI element with icon nodes on the left and text on the right.
    /// Each row: [circle with item icon] Title
    ///                                    Subtitle (smaller, grey)
    /// Click on a row fires the callback with the row Id.
    /// </summary>
    public class ItemListElement : GuiElement
    {
        private List<ItemListRow> rows;
        private readonly Action<string>? onRowClicked;
        private LoadedTexture listTexture;
        private LoadedTexture? _hoverOverlayTexture;
        private int _hoverOverlayW;
        private int _hoverOverlayH;
        private string? hoveredRowId;
        private readonly Dictionary<string, ItemStack> iconStacks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DummySlot> _slotCache = new(StringComparer.OrdinalIgnoreCase);

        // Tooltip background WITHOUT shade blur. GuiElementHoverText.Recompose() runs an expensive
        // full-surface BlurFull() on every SetNewText when Shade==true; sweeping the mouse across
        // rows changes the tooltip text constantly, so that blur was the real hover-lag source.
        private static readonly TextBackground TooltipBg = new TextBackground
        {
            Padding = 5,
            Radius = 1,
            FillColor = GuiStyle.DialogStrongBgColor,
            BorderColor = GuiStyle.DialogBorderColor,
            BorderWidth = 3,
            Shade = false
        };

        // Cached world bounds — CalcWorldBounds recurses up the parent chain and is expensive to
        // call every frame / on every mouse move. Bounds are stable between recomposes, so compute
        // once and reuse.
        private double _absX, _absY;
        private bool _boundsCached;

        private void EnsureBounds()
        {
            if (_boundsCached) return;
            Bounds.CalcWorldBounds();
            _absX = Bounds.absX;
            _absY = Bounds.absY;
            _boundsCached = true;
        }

        // Hover debounce.
        private string? pendingHoverId;
        private float pendingHoverAccum;
        private const float HoverDebounceSeconds = 0.08f;

        // Tooltip support - one cached element per distinct text. Reusing pre-composed tooltip
        // surfaces avoids the expensive richtext recompose that SetNewText triggers on every
        // hover change while sweeping the cursor across rows.
        private readonly Dictionary<string, GuiElementHoverText> tooltipCache =
            new(StringComparer.Ordinal);
        private GuiElementHoverText? activeTooltipElem;
        private string? lastTooltipText;

        // Scroll offset (logical units, scaled to world inside SetScroll).
        private double _scrollY;

        /// <summary>
        /// Optional fallback resolver for icon item codes that are not found as regular
        /// items or blocks. Consumers with custom item systems (e.g. action items) should
        /// set this to provide <see cref="ItemStack"/> instances for their custom codes.
        /// Parameters: (ICoreClientAPI capi, string itemCode) → ItemStack or null.
        /// </summary>
        public static System.Func<ICoreClientAPI, string, ItemStack>? IconStackFallbackResolver { get; set; }

        private double RowHeight => scaled(72.0);
        private double NodeRadius => scaled(24.0);
        private double IconSize => scaled(26.0);
        private double NodeCenterX => scaled(32.0);
        private double TextLeft => scaled(62.0);
        private double PadTop => scaled(8.0);

        public ItemListElement(ICoreClientAPI capi, ElementBounds bounds, List<ItemListRow> rows, Action<string> onRowClicked)
            : base(capi, bounds)
        {
            this.rows = rows ?? new List<ItemListRow>();
            this.onRowClicked = onRowClicked;
            listTexture = new LoadedTexture(capi);
        }

        public void SetData(List<ItemListRow> rows)
        {
            this.rows = rows ?? new List<ItemListRow>();
            hoveredRowId = null;
            iconStacks.Clear();
            _slotCache.Clear();
            _boundsCached = false;
            _scrollY = 0;
            ClearTooltipCache();
            WarmTooltips();
            RegenerateTexture();
        }

        /// <summary>Sets the vertical scroll offset in logical (unscaled) pixels.</summary>
        public void SetScroll(double value)
        {
            _scrollY = scaled(Math.Max(0.0, value));
        }

        /// <summary>Total height of the scrolled content in world pixels.</summary>
        private double ContentHeight => PadTop + (rows?.Count ?? 0) * RowHeight;

        public override void ComposeElements(Cairo.Context ctxStatic, Cairo.ImageSurface surfaceStatic)
        {
            _boundsCached = false;
            // Rows were passed via the constructor (no SetData call) - pre-compose the
            // tooltip surfaces now so hovering later is just a texture render.
            WarmTooltips();
            RegenerateTexture();
        }

        private void WarmTooltips()
        {
            if (rows == null) return;
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row?.TooltipText)) continue;
                try { GetTooltipElem(row.TooltipText); }
                catch (Exception ex) { api?.Logger?.Warning("[ItemListElement] non-critical GUI operation failed: {0}", ex.Message); }
            }
        }

        private void ClearTooltipCache()
        {
            foreach (var elem in tooltipCache.Values)
            {
                elem?.Dispose();
            }
            tooltipCache.Clear();
            activeTooltipElem = null;
            lastTooltipText = null;
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (listTexture == null || Bounds?.ParentBounds == null) return;

            EnsureBounds();

            // --- Hover detection (no texture regeneration) ---
            string newHover = null;
            try
            {
                newHover = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY)
                    ? GetRowIdAt(api.Input.MouseY)
                    : null;
            }
            catch { return; }

            if (!string.Equals(newHover, hoveredRowId, StringComparison.Ordinal))
            {
                if (!string.Equals(newHover, pendingHoverId, StringComparison.Ordinal))
                {
                    pendingHoverId = newHover;
                    pendingHoverAccum = 0f;
                }
                else
                {
                    pendingHoverAccum += deltaTime;
                    if (pendingHoverAccum >= HoverDebounceSeconds)
                    {
                        hoveredRowId = newHover;
                        pendingHoverId = null;
                        pendingHoverAccum = 0f;
                    }
                }
            }
            else if (pendingHoverId != null)
            {
                pendingHoverId = null;
                pendingHoverAccum = 0f;
            }

            // --- Render list texture ---
            api.Render.Render2DLoadedTexture(listTexture, (float)_absX, (float)(_absY - _scrollY));

            // --- Hover overlay (cheap texture render, no Cairo redraw) ---
            if (!string.IsNullOrWhiteSpace(hoveredRowId))
            {
                int hoveredIdx = rows.FindIndex(r => string.Equals(r?.Id, hoveredRowId, StringComparison.Ordinal));
                if (hoveredIdx >= 0)
                {
                    var hovered = rows[hoveredIdx];
                    if (hovered != null)
                    {
                        EnsureHoverOverlayTexture();
                        double rowTop = PadTop + hoveredIdx * RowHeight - _scrollY;
                        if (rowTop + RowHeight > 0 && rowTop < Bounds.InnerHeight)
                        {
                            api.Render.Render2DLoadedTexture(_hoverOverlayTexture,
                                (float)_absX, (float)(_absY + rowTop));
                        }
                    }
                }
            }

            RenderIcons();
            RenderTooltip(deltaTime);
        }

        private void RenderTooltip(float deltaTime)
        {
            // Find currently hovered row
            ItemListRow row = null;
            if (!string.IsNullOrWhiteSpace(hoveredRowId))
            {
                row = rows.Find(r => string.Equals(r?.Id, hoveredRowId, StringComparison.Ordinal));
            }

            string tip = row?.TooltipText;
            if (string.IsNullOrWhiteSpace(tip))
            {
                activeTooltipElem?.SetVisible(false);
                activeTooltipElem = null;
                lastTooltipText = null;
                return;
            }

            if (!string.Equals(lastTooltipText, tip, StringComparison.Ordinal))
            {
                activeTooltipElem?.SetVisible(false);
                lastTooltipText = tip;
                activeTooltipElem = GetTooltipElem(tip);
            }

            if (activeTooltipElem == null) return;
            activeTooltipElem.SetVisible(true);

            bool scissorWasEnabled = api.Render.ScissorStack.Count > 0;
            if (scissorWasEnabled) api.Render.GlScissorFlag(false);

            try { activeTooltipElem.RenderInteractiveElements(deltaTime); }
            catch (Exception ex) { api?.Logger?.Warning("[ItemListElement] non-critical GUI operation failed: {0}", ex.Message); }

            if (scissorWasEnabled) api.Render.GlScissorFlag(true);
        }

        private GuiElementHoverText GetTooltipElem(string text)
        {
            if (tooltipCache.TryGetValue(text, out var elem)) return elem;

            var bounds = ElementBounds.Fixed(0, 0, 1, 1);
            bounds.ParentBounds = ElementBounds.Empty;
            elem = new GuiElementHoverText(api, text, CairoFont.WhiteSmallText(), 420, bounds, TooltipBg);
            elem.SetAutoDisplay(false);
            elem.SetAutoWidth(true);
            elem.SetFollowMouse(true);
            elem.SetVisible(false);
            tooltipCache[text] = elem;
            return elem;
        }

        private void RenderIcons()
        {
            if (rows == null || rows.Count == 0) return;
            EnsureBounds();

            double rowH = RowHeight;
            double iconSz = IconSize;
            double nodeCX = NodeCenterX;
            double padTop = PadTop;
            double absX = _absX;
            double absY = _absY;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                // Skip custom icons (already drawn in RegenerateTexture via Cairo)
                if (!string.IsNullOrWhiteSpace(row.CustomIconKey)
                    && CustomIconRegistry.Has(row.CustomIconKey)) continue;
                if (string.IsNullOrWhiteSpace(row.IconItemCode)) continue;

                var stack = GetIconStack(row);
                if (stack == null) continue;

                double rowTop = padTop + i * rowH - _scrollY;
                if (rowTop + rowH <= 0 || rowTop >= Bounds.InnerHeight) continue;

                double nodeCY = rowTop + rowH / 2.0;

                double centerX = absX + nodeCX;
                double centerY = absY + nodeCY;
                string iconKey = MakeIconKey(row);
                if (!_slotCache.TryGetValue(iconKey, out var slot))
                {
                    slot = new DummySlot(stack);
                    _slotCache[iconKey] = slot;
                }
                api.Render.RenderItemstackToGui(slot, centerX, centerY, 500, (float)iconSz, -1, true, false, false);
            }
        }

        public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (Bounds?.ParentBounds == null) return;
            if (!Bounds.PointInside(args.X, args.Y)) return;

            string rowId = GetRowIdAt(args.Y);
            if (!string.IsNullOrWhiteSpace(rowId))
            {
                var row = rows.Find(r => string.Equals(r?.Id, rowId, StringComparison.Ordinal));
                if (row != null && row.Status == ItemRowStatus.Available)
                {
                    onRowClicked?.Invoke(rowId);
                    args.Handled = true;
                    return;
                }
            }

            base.OnMouseUpOnElement(api, args);
        }

        private string? GetRowIdAt(int mouseY)
        {
            if (rows == null || rows.Count == 0) return null;
            EnsureBounds();

            double relY = mouseY - _absY - PadTop + _scrollY;

            int index = (int)(relY / RowHeight);
            if (index < 0 || index >= rows.Count) return null;

            return rows[index]?.Id;
        }

        private void RegenerateTexture()
        {
            Bounds.CalcWorldBounds();

            int width = Math.Max(1, (int)Bounds.InnerWidth);
            int height = Math.Max(1, (int)ContentHeight);

            var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, width, height);
            var context = new Cairo.Context(surface);

            context.SetSourceRGBA(0, 0, 0, 0);
            context.Paint();

            if (rows == null || rows.Count == 0)
            {
                generateTexture(surface, ref listTexture);
                context.Dispose();
                surface.Dispose();
                return;
            }

            double rowH = RowHeight;
            double nodeCX = NodeCenterX;
            double nodeR = NodeRadius;
            double textX = TextLeft;
            double padTop = PadTop;

            context.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                double rowTop = padTop + i * rowH;
                // No hover state in static texture — hover is drawn as overlay.

                // Card body (rounded, with vertical gradient and silver inlay).
                double cardX = ArcanumGuiTheme.Snap(scaled(2.0)) - 0.5;
                double cardW = width - scaled(4.0);
                double cardR = scaled(ArcanumGuiTheme.Radius.Medium);
                double cardH = rowH - scaled(6.0);
                double drawTop = rowTop;

                var bg = ArcanumGuiTheme.SurfaceCard;

                ArcanumGuiTheme.FillRoundedRectVerticalGradient(context,
                    cardX, drawTop, cardW, cardH, cardR,
                    bg,
                    bg.Lerp(ArcanumGuiTheme.SurfaceBase, 0.35));

                ArcanumGuiTheme.StrokeRoundedRect(context,
                    cardX + 0.5, drawTop + 0.5, cardW - 1, cardH - 1, cardR,
                    ArcanumGuiTheme.BorderShadow.WithAlpha(0.6), scaled(1.0));
                ArcanumGuiTheme.StrokeRoundedRect(context,
                    cardX + scaled(2.0), drawTop + scaled(2.0),
                    cardW - scaled(4.0), cardH - scaled(4.0),
                    Math.Max(1.0, cardR - scaled(2.0)),
                    ArcanumGuiTheme.BorderSilver.WithAlpha(0.45), scaled(1.0));

                // Optional highlight border for special rows.
                if (!string.IsNullOrWhiteSpace(row.BorderColor)
                    && RGBA.ParseHexColor(row.BorderColor) is RGBA borderColor)
                {
                    double borderThick = scaled(row.BorderThickness);
                    ArcanumGuiTheme.StrokeRoundedRect(context,
                        cardX - borderThick * 0.5, drawTop - borderThick * 0.5,
                        cardW + borderThick, cardH + borderThick, cardR,
                        borderColor.WithAlpha(0.45), borderThick);
                }

                // Status color.
                var (sR, sG, sB) = GetNodeFillColor(row.Status);
                var statusRgba = new RGBA(sR, sG, sB, 1.0);

                double nodeCY = drawTop + cardH / 2.0;

                // Draw custom icon if present (Cairo glyph).
                if (!string.IsNullOrWhiteSpace(row.CustomIconKey)
                    && CustomIconRegistry.TryGet(row.CustomIconKey, out var customRenderer))
                {
                    customRenderer.Draw(context, nodeCX, nodeCY, nodeR * 0.85);
                }

                // Icon circle background.
                ArcanumGuiTheme.FillCircle(context, nodeCX, nodeCY, nodeR,
                    statusRgba.WithAlpha(0.18));
                ArcanumGuiTheme.StrokeCircle(context, nodeCX, nodeCY, nodeR,
                    statusRgba.WithAlpha(0.55), scaled(1.2));

                // Title text.
                var (tR, tG, tB, tA) = GetTitleColor(row.Status, false);
                context.SetSourceRGBA(tR, tG, tB, tA);
                context.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Bold);
                context.SetFontSize(scaled(15.0));
                context.MoveTo(textX, drawTop + scaled(26.0));
                context.ShowText(row.Title ?? "");

                // Subtitle text.
                if (!string.IsNullOrWhiteSpace(row.Subtitle))
                {
                    var (sR2, sG2, sB2, sA2) = GetSubtitleColor(row.Status);
                    context.SetSourceRGBA(sR2, sG2, sB2, sA2);
                    context.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
                    context.SetFontSize(scaled(12.5));
                    context.MoveTo(textX, drawTop + scaled(46.0));
                    context.ShowText(row.Subtitle);
                }
            }

            try { generateTexture(surface, ref listTexture); }
            catch (Exception ex) { api?.Logger?.Warning("[ItemListElement] Texture generation failed: {0}", ex.Message); }
            context.Dispose();
            surface.Dispose();
        }

        private void EnsureHoverOverlayTexture()
        {
            int width = Math.Max(1, (int)Bounds.InnerWidth);
            int height = Math.Max(1, (int)(RowHeight - scaled(6.0)));
            if (_hoverOverlayTexture != null && _hoverOverlayW == width && _hoverOverlayH == height) return;

            _hoverOverlayTexture?.Dispose();
            _hoverOverlayTexture = new LoadedTexture(api);
            _hoverOverlayW = width;
            _hoverOverlayH = height;

            using var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, width, height);
            using var ctx = new Cairo.Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            double cardX = ArcanumGuiTheme.Snap(scaled(2.0)) - 0.5;
            double cardW = width - scaled(4.0);
            double cardR = scaled(ArcanumGuiTheme.Radius.Medium);

            ArcanumGuiTheme.FillRoundedRect(ctx, cardX, 0, cardW, height, cardR,
                ArcanumGuiTheme.SurfaceCardHover.WithAlpha(0.35));
            ArcanumGuiTheme.StrokeRoundedRect(ctx, cardX + 0.5, 0.5, cardW - 1, height - 1, cardR,
                ArcanumGuiTheme.BorderSilver.WithAlpha(0.35), scaled(1.0));

            try { generateTexture(surface, ref _hoverOverlayTexture); }
            catch (Exception ex)
            {
                api?.Logger?.Warning("[ItemListElement] Hover overlay texture generation failed: {0}", ex.Message);
                _hoverOverlayTexture?.Dispose();
                _hoverOverlayTexture = new LoadedTexture(api);
            }
        }

        private static (double, double, double) GetNodeFillColor(ItemRowStatus status)
        {
            return status switch
            {
                ItemRowStatus.Owned => (
                    ArcanumGuiTheme.StatusComplete.R,
                    ArcanumGuiTheme.StatusComplete.G,
                    ArcanumGuiTheme.StatusComplete.B),
                ItemRowStatus.Available => (
                    ArcanumGuiTheme.AccentBright.R,
                    ArcanumGuiTheme.AccentBright.G,
                    ArcanumGuiTheme.AccentBright.B),
                _ => (
                    ArcanumGuiTheme.StatusLocked.R,
                    ArcanumGuiTheme.StatusLocked.G,
                    ArcanumGuiTheme.StatusLocked.B),
            };
        }

        private static (double, double, double, double) GetTitleColor(ItemRowStatus status, bool hovered)
        {
            return status switch
            {
                ItemRowStatus.Locked => (
                    ArcanumGuiTheme.TextSecondary.R,
                    ArcanumGuiTheme.TextSecondary.G,
                    ArcanumGuiTheme.TextSecondary.B,
                    0.7),
                ItemRowStatus.Owned => (
                    ArcanumGuiTheme.StatusComplete.R,
                    ArcanumGuiTheme.StatusComplete.G,
                    ArcanumGuiTheme.StatusComplete.B,
                    1.0),
                _ => (
                    ArcanumGuiTheme.TextPrimary.R,
                    ArcanumGuiTheme.TextPrimary.G,
                    ArcanumGuiTheme.TextPrimary.B,
                    1.0),
            };
        }

        private static (double, double, double, double) GetSubtitleColor(ItemRowStatus status)
        {
            return status switch
            {
                ItemRowStatus.Locked => (
                    ArcanumGuiTheme.TextMuted.R,
                    ArcanumGuiTheme.TextMuted.G,
                    ArcanumGuiTheme.TextMuted.B,
                    0.85),
                ItemRowStatus.Owned => (
                    ArcanumGuiTheme.StatusComplete.R,
                    ArcanumGuiTheme.StatusComplete.G,
                    ArcanumGuiTheme.StatusComplete.B,
                    0.85),
                _ => (
                    ArcanumGuiTheme.TextSecondary.R,
                    ArcanumGuiTheme.TextSecondary.G,
                    ArcanumGuiTheme.TextSecondary.B,
                    1.0),
            };
        }

        private string MakeIconKey(ItemListRow? row)
        {
            if (string.IsNullOrWhiteSpace(row?.IconUcontents)) return row?.IconItemCode ?? "";
            return $"{row.IconItemCode}|{row.IconUcontents}";
        }

        private ItemStack? GetIconStack(ItemListRow row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.IconItemCode)) return null;
            string iconKey = MakeIconKey(row);
            if (iconStacks.TryGetValue(iconKey, out var cached)) return cached;

            ItemStack stack = null;
            var loc = new AssetLocation(row.IconItemCode);
            var item = api.World.GetItem(loc);
            if (item != null)
            {
                stack = new ItemStack(item);
            }
            else
            {
                var block = api.World.GetBlock(loc);
                if (block != null)
                {
                    stack = new ItemStack(block);
                }
                else if (IconStackFallbackResolver != null)
                {
                    try { stack = IconStackFallbackResolver(api, row.IconItemCode); }
                    catch (Exception ex) { api?.Logger?.Warning("[ItemListElement] Icon stack fallback resolver failed: {0}", ex.Message); }
                }
            }

            if (stack != null && !string.IsNullOrWhiteSpace(row.IconUcontents))
            {
                try
                {
                    var contentTree = new TreeAttribute();
                    contentTree.SetString("type", "item");
                    contentTree.SetString("code", row.IconUcontents);
                    contentTree.SetFloat("quantity", 1f);
                    contentTree.SetBool("makefull", true);
                    stack.Attributes["ucontents"] = new TreeArrayAttribute(new TreeAttribute[] { contentTree });
                }
                catch (Exception ex) { api?.Logger?.Warning("[ItemListElement] Failed to set ucontents on icon stack: {0}", ex.Message); }
            }

            if (stack != null) iconStacks[iconKey] = stack;
            return stack;
        }

        public override void Dispose()
        {
            listTexture?.Dispose();
            listTexture = null;
            _hoverOverlayTexture?.Dispose();
            _hoverOverlayTexture = null;
            ClearTooltipCache();
            base.Dispose();
        }
    }
}
