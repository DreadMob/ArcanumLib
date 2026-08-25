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
    private readonly double _rowHeight;

    private readonly ArcanumListSelection<T> _selection;
    private readonly ArcanumListRenderer<T> _renderer;

    private float _scrollY;
    private int _hoveredIndex = -1;

    private bool _dragging;
    private double _dragStartMouseY;
    private float _dragStartScroll;

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
        _rowHeight = Math.Max(8.0, rowHeight);
        _selection = new ArcanumListSelection<T>(onSelected);
        _renderer = new ArcanumListRenderer<T>(
            capi, labelSelector, font ?? ArcanumFont.Body, textPadding, drawZebra,
            scaled, GenerateTextureImpl);
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
        _selection.Reset();
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
        _selection.Set(index, _items.Count);
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
        _renderer.Render(api, BuildRenderState());
        _renderer.Draw(api, Bounds);
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

    private void MarkDirty() => _renderer.MarkDirty();

    private ArcanumListRenderState<T> BuildRenderState() => new(
        _items,
        Bounds!,
        _scrollY,
        _hoveredIndex,
        _selection.SelectedIndex,
        _dragging,
        ScaledRowHeight,
        TotalHeight,
        VisibleHeight,
        MaxScroll,
        ScrollNeeded);

    private void GenerateTextureImpl(ImageSurface surface, ref LoadedTexture texture) =>
        generateTexture(surface, ref texture);

    /// <summary>Handles mouse down for scrollbar dragging and row selection.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (Bounds == null) return;
        if (!Bounds.PointInside(args.X, args.Y)) return;

        if (_renderer.IsOnScrollbar(args.X, Bounds, ScrollNeeded))
        {
            var (_, hY, _, hH) = _renderer.ScrollbarHandleRect(BuildRenderState());
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
            _selection.SelectByClick(row, _items, api?.Logger);
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
            var (_, _, _, hH) = _renderer.ScrollbarHandleRect(BuildRenderState());
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
        _renderer.Dispose();
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
