using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// A reusable, scrollable and selectable list of text rows.
/// </summary>
/// <typeparam name="T">The type of the t value.</typeparam>
public class ArcanumList<T> : GuiElement
{
    private readonly List<T> _items = new();
    private readonly System.Func<T, string> _label;
    private readonly double _rowHeight;
    private readonly System.Action<T, int>? _onSelected;
    private readonly CairoFont _font;
    private readonly double _textPadding;
    private readonly bool _drawZebra;

    private float _scrollY;
    private int _hoveredIndex = -1;
    private int _selectedIndex = -1;

    private LoadedTexture _texture;
    private string? _textureKey;
    private bool _dirty = true;

    private bool _dragging;
    private double _dragStartMouseY;
    private float _dragStartScroll;

    private const double ScrollbarWidth = 8.0;
    private const double ScrollbarPadding = 3.0;
    private const double MinHandleHeight = 24.0;

    /// <summary>
    /// Creates a new Arcanum list with the given row factory and selection callback.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="labelSelector">The label selector value.</param>
    /// <param name="rowHeight">The row height value.</param>
    /// <param name="onSelected">The callback to invoke.</param>
    /// <param name="font">The font value.</param>
    /// <param name="textPadding">The text padding value.</param>
    /// <param name="drawZebra">The draw zebra value.</param>
    public ArcanumList(
        ICoreClientAPI capi,
        ElementBounds bounds,
        System.Func<T, string> labelSelector,
        double rowHeight,
        System.Action<T, int>? onSelected = null,
        CairoFont? font = null,
        double textPadding = 10.0,
        bool drawZebra = true)
        : base(capi, bounds)
    {
        _label = labelSelector;
        _rowHeight = Math.Max(8.0, rowHeight);
        _onSelected = onSelected;
        _font = font ?? ArcanumFont.Body;
        _textPadding = textPadding;
        _drawZebra = drawZebra;
        _texture = new LoadedTexture(capi);
    }

    /// <summary>
    /// Replaces the current items and resets the scroll/selection state.
    /// </summary>
    /// <param name="items">The collection of items values.</param>
    /// <returns>The set items.</returns>
    public ArcanumList<T> SetItems(IEnumerable<T> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _scrollY = 0;
        _hoveredIndex = -1;
        _selectedIndex = -1;
        MarkDirty();
        return this;
    }

    /// <summary>
    /// Scrolls the list to the given vertical offset in content pixels.
    /// </summary>
    /// <param name="y">The Y coordinate.</param>
    public void ScrollTo(float y)
    {
        _scrollY = GameMath.Clamp(y, 0f, MaxScroll);
        MarkDirty();
    }

    /// <summary>
    /// Selects an item by index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    public void Select(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            _selectedIndex = -1;
        }
        else
        {
            _selectedIndex = index;
        }
        MarkDirty();
    }

    private int RowCount => _items.Count;
    private double ScaledRowHeight => scaled(_rowHeight);
    private float TotalHeight => (float)(RowCount * ScaledRowHeight);
    private float VisibleHeight => Bounds == null ? 0f : (float)Bounds.OuterHeight;
    private float MaxScroll => Math.Max(0f, TotalHeight - VisibleHeight);
    private bool ScrollNeeded => TotalHeight > VisibleHeight + 0.1f;

    /// <summary>Skips static composition; the list renders dynamically in <see cref="RenderInteractiveElements" />.</summary>
    /// <param name="ctxStatic">The ctx static value.</param>
    /// <param name="surfaceStatic">The surface static value.</param>
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
    {
        // The list renders dynamically in RenderInteractiveElements.
    }

    /// <summary>Updates hover state, regenerates the cached texture if needed, and renders the list.</summary>
    /// <param name="deltaTime">The delta time value.</param>
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        if (!Bounds.Initialized) Bounds.CalcWorldBounds();
        if (Bounds.OuterWidth <= 0 || Bounds.OuterHeight <= 0) return;

        UpdateHover();
        RegenerateIfNeeded();

        if (_texture.TextureId > 0)
        {
            api?.Render?.Render2DLoadedTexture(_texture, (float)Bounds.absX, (float)Bounds.absY);
        }
    }

    private void UpdateHover()
    {
        if (Bounds == null) return;

        int mx = api?.Input?.MouseX ?? 0;
        int my = api?.Input?.MouseY ?? 0;

        bool inside = false;
        try
        {
            inside = Bounds.PointInside(mx, my);
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumList] Hover detection failed: {0}", ex);
        }

        if (!inside)
        {
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                MarkDirty();
            }
            return;
        }

        int row = HitTest(mx, my);
        if (row != _hoveredIndex)
        {
            _hoveredIndex = row;
            MarkDirty();
        }
    }

    private int HitTest(int mouseX, int mouseY)
    {
        if (Bounds == null) return -1;
        double localY = mouseY - Bounds.absY + _scrollY;
        int row = (int)(localY / ScaledRowHeight);
        return GameMath.Clamp(row, -1, RowCount - 1);
    }

    private bool IsOnScrollbar(int mouseX, int mouseY)
    {
        if (Bounds == null || !ScrollNeeded) return false;
        double localX = mouseX - Bounds.absX;
        double trackX = Bounds.OuterWidth - scaled(ScrollbarWidth) - scaled(ScrollbarPadding);
        return localX >= trackX;
    }

    private (double x, double y, double w, double h) ScrollbarHandleRect()
    {
        double trackH = Bounds.OuterHeight;
        double ratio = VisibleHeight / Math.Max(1f, TotalHeight);
        double handleH = Math.Max(scaled(MinHandleHeight), trackH * ratio);
        double range = trackH - handleH;
        double t = MaxScroll > 0.001f ? _scrollY / MaxScroll : 0.0;
        double trackX = Bounds.OuterWidth - scaled(ScrollbarWidth) - scaled(ScrollbarPadding);
        double handleY = range * t;
        return (trackX, handleY, scaled(ScrollbarWidth), handleH);
    }

    private void MarkDirty()
    {
        _dirty = true;
        _textureKey = null;
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;

        int width = Math.Max(1, (int)Bounds.OuterWidth);
        int height = Math.Max(1, (int)Bounds.OuterHeight);

        // Round scrollY to whole pixels so smooth wheel scrolling does not regenerate
        // the texture on every fractional change (was :F2, causing ~100 regen/s).
        string newKey = $"{width}|{height}|{(int)Math.Round(_scrollY)}|{_hoveredIndex}|{_selectedIndex}|{RowCount}";
        if (!_dirty && string.Equals(_textureKey, newKey, StringComparison.Ordinal) && _texture.TextureId > 0)
            return;

        _textureKey = newKey;
        _dirty = false;

        ImageSurface? surface = null;
        Context? ctx = null;

        try
        {
            _texture?.Dispose();
            _texture = new LoadedTexture(api);

            surface = new ImageSurface(Format.Argb32, width, height);
            ctx = new Context(surface);

            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            // Background
            ArcanumGuiTheme.FillRoundedRect(
                ctx, 0, 0, width, height,
                GuiElement.scaled(ArcanumGuiTheme.Radius.Medium),
                ArcanumGuiTheme.SurfaceDeepest.WithAlpha(0.65));
            ArcanumGuiTheme.StrokeRoundedRect(
                ctx, 0, 0, width, height,
                GuiElement.scaled(ArcanumGuiTheme.Radius.Medium),
                ArcanumGuiTheme.BorderSubtle, GuiElement.scaled(1.0));

            // Clip to the list area
            ctx.Rectangle(0, 0, width, height);
            ctx.Clip();

            double contentW = ScrollNeeded ? width - scaled(ScrollbarWidth) - scaled(ScrollbarPadding) * 2.0 : width;

            if (RowCount > 0)
            {
                int firstRow = Math.Max(0, (int)(_scrollY / ScaledRowHeight));
                int lastRow = Math.Min(RowCount - 1, (int)((_scrollY + height) / ScaledRowHeight) + 1);

                for (int i = firstRow; i <= lastRow; i++)
                {
                    double rowY = i * ScaledRowHeight - _scrollY;

                    // Row background
                    RGBA bgColor;
                    if (i == _selectedIndex)
                    {
                        bgColor = ArcanumGuiTheme.StatusActive.WithAlpha(0.85);
                    }
                    else if (i == _hoveredIndex)
                    {
                        bgColor = ArcanumGuiTheme.SurfaceCardHover;
                    }
                    else if (_drawZebra && i % 2 == 1)
                    {
                        bgColor = ArcanumGuiTheme.SurfaceCard.WithAlpha(0.18);
                    }
                    else
                    {
                        bgColor = default;
                    }

                    if (bgColor.A > 0.001)
                    {
                        ctx.Rectangle(0, rowY, contentW, ScaledRowHeight);
                        bgColor.Apply(ctx);
                        ctx.Fill();
                    }

                    // Row text
                    string? label = _label(_items[i]) ?? "";
                    if (string.IsNullOrWhiteSpace(label)) continue;

                    _font.SetupContext(ctx);

                    RGBA textColor = i == _selectedIndex || i == _hoveredIndex
                        ? ArcanumGuiTheme.TextPrimary
                        : ArcanumGuiTheme.TextSecondary;
                    textColor.Apply(ctx);

                    var ext = ctx.TextExtents(label);
                    double x = scaled(_textPadding) - ext.XBearing;
                    double y = rowY + (ScaledRowHeight - ext.Height) / 2.0 - ext.YBearing;

                    ctx.MoveTo(x, y);
                    ctx.ShowText(label);
                }
            }

            ctx.ResetClip();

            // Scrollbar
            if (ScrollNeeded)
            {
                double trackX = Bounds.OuterWidth - scaled(ScrollbarWidth) - scaled(ScrollbarPadding);
                ArcanumGuiTheme.FillRoundedRect(
                    ctx, trackX, 0, scaled(ScrollbarWidth), height,
                    scaled(ScrollbarWidth / 2.0),
                    ArcanumGuiTheme.SurfaceDeepest.WithAlpha(0.85));
                ArcanumGuiTheme.StrokeRoundedRect(
                    ctx, trackX, 0, scaled(ScrollbarWidth), height,
                    scaled(ScrollbarWidth / 2.0),
                    ArcanumGuiTheme.BorderSubtle, GuiElement.scaled(1.0));

                var (hX, hY, hW, hH) = ScrollbarHandleRect();
                RGBA handleColor = _dragging || _hoveredIndex == -2
                    ? ArcanumGuiTheme.Accent.WithAlpha(0.95)
                    : ArcanumGuiTheme.AccentDim.Lerp(ArcanumGuiTheme.Accent, 0.55);
                ArcanumGuiTheme.FillRoundedRect(
                    ctx, hX, hY, hW, hH,
                    hW / 2.0,
                    handleColor);
            }

            generateTexture(surface, ref _texture);

        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumList] Failed to generate texture: {0}", ex);
            _textureKey = null;
            _dirty = true;
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
        }
    }

    /// <summary>Handles mouse down for scrollbar dragging and row selection.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (Bounds == null) return;
        if (!Bounds.PointInside(args.X, args.Y)) return;

        if (IsOnScrollbar(args.X, args.Y))
        {
            var (hX, hY, hW, hH) = ScrollbarHandleRect();
            double localY = args.Y - Bounds.absY;

            if (localY < hY || localY > hY + hH)
            {
                double trackH = Bounds.OuterHeight - hH;
                double centerT = (localY - hH / 2.0) / Math.Max(1.0, trackH);
                centerT = GameMath.Clamp(centerT, 0.0, 1.0);
                _scrollY = (float)(centerT * MaxScroll);
                MarkDirty();
            }

            _dragging = true;
            _dragStartMouseY = args.Y;
            _dragStartScroll = _scrollY;
            args.Handled = true;
            return;
        }

        int row = HitTest(args.X, args.Y);
        if (row >= 0 && row < RowCount)
        {
            _selectedIndex = row;
            try
            {
                _onSelected?.Invoke(_items[row], row);
            }
            catch (Exception ex)
            {
                api?.Logger?.Warning("[ArcanumList] Selection callback failed: {0}", ex);
            }
            MarkDirty();
            args.Handled = true;
        }
    }

    /// <summary>Handles mouse move for scrollbar dragging and hover state.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (Bounds == null) return;

        if (_dragging)
        {
            var (_, _, _, hH) = ScrollbarHandleRect();
            double pixelRange = Math.Max(1.0, Bounds.OuterHeight - hH);
            double dy = args.Y - _dragStartMouseY;
            double tDelta = dy / pixelRange;
            _scrollY = GameMath.Clamp(_dragStartScroll + (float)(tDelta * MaxScroll), 0f, MaxScroll);
            MarkDirty();
            args.Handled = true;
            return;
        }

        // Hover is updated in RenderInteractiveElements, but set handled if inside to prevent pass-through.
        if (Bounds.PointInside(args.X, args.Y))
        {
            args.Handled = true;
        }
    }

    /// <summary>Handles mouse up to end scrollbar dragging.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (_dragging)
        {
            _dragging = false;
            MarkDirty();
            args.Handled = true;
        }
    }

    /// <summary>Handles mouse wheel scrolling inside the list.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (Bounds == null || !ScrollNeeded) return;
        if (args.IsHandled) return;

        int mx = api?.Input?.MouseX ?? 0;
        int my = api?.Input?.MouseY ?? 0;

        bool inside = false;
        try
        {
            inside = Bounds.PointInside(mx, my);
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumList] Wheel input test failed: {0}", ex);
        }
        if (!inside) return;

        double step = Math.Max(ScaledRowHeight * 3.0, VisibleHeight * 0.12);
        _scrollY = GameMath.Clamp(_scrollY - args.deltaPrecise * (float)step, 0f, MaxScroll);
        MarkDirty();
        args.SetHandled(true);
    }

    /// <summary>Releases the cached list texture.</summary>
    public override void Dispose()
    {
        _texture?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Composer helper for adding <see cref="ArcanumList{T}" /> instances to a <see cref="GuiComposer" />.
/// </summary>
public static class ArcanumListComposerHelpers
{
    /// <summary>
    /// Adds an Arcanum scrollable list to the composer.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="composer">The composer value.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="labelSelector">The label selector value.</param>
    /// <param name="rowHeight">The row height value.</param>
    /// <param name="onSelected">The callback to invoke.</param>
    /// <param name="font">The font value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add arcanum list.</returns>
    public static GuiComposer AddArcanumList<T>(
        this GuiComposer composer,
        ElementBounds bounds,
        System.Func<T, string> labelSelector,
        double rowHeight,
        System.Action<T, int>? onSelected = null,
        CairoFont? font = null,
        string? key = null)
    {
        if (!composer.Composed)
        {
            var list = new ArcanumList<T>(composer.Api, bounds, labelSelector, rowHeight, onSelected, font);
            composer.AddInteractiveElement(list, key);
        }
        return composer;
    }

    /// <summary>
    /// Adds an Arcanum scrollable list with an initial set of items.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="composer">The composer value.</param>
    /// <param name="items">The collection of items values.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="labelSelector">The label selector value.</param>
    /// <param name="rowHeight">The row height value.</param>
    /// <param name="onSelected">The callback to invoke.</param>
    /// <param name="font">The font value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add arcanum list.</returns>
    public static GuiComposer AddArcanumList<T>(
        this GuiComposer composer,
        IEnumerable<T> items,
        ElementBounds bounds,
        System.Func<T, string> labelSelector,
        double rowHeight,
        System.Action<T, int>? onSelected = null,
        CairoFont? font = null,
        string? key = null)
    {
        if (!composer.Composed)
        {
            var list = new ArcanumList<T>(composer.Api, bounds, labelSelector, rowHeight, onSelected, font);
            list.SetItems(items);
            composer.AddInteractiveElement(list, key);
        }
        return composer;
    }

    /// <summary>
    /// Retrieves an Arcanum list from the composer by key.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="composer">The composer value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The arcanum list, or null if none is found.</returns>
    public static ArcanumList<T>? GetArcanumList<T>(this GuiComposer composer, string key)
    {
        return composer.GetElement(key) as ArcanumList<T>;
    }
}
