using System;
using ArcanumLib.Diagnostics;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Delegate that wraps <see cref="GuiElement.generateTexture" /> so the renderer
/// can bake a Cairo surface into a texture without inheriting from GuiElement.
/// </summary>
/// <param name="surface">The source image surface.</param>
/// <param name="intoTexture">The loaded texture to overwrite.</param>
internal delegate void GenerateListTexture(ImageSurface surface, ref LoadedTexture intoTexture);

/// <summary>
/// Immutable snapshot of the dynamic state <see cref="ArcanumListRenderer{T}" /> needs
/// to draw a single frame: scroll position, hover/selection, scrollbar drag and the
/// geometry derived from the current bounds.
/// </summary>
/// <typeparam name="T">The element type of the owning list.</typeparam>
internal readonly struct ArcanumListRenderState<T>
{
    public readonly IReadOnlyList<T> Items;
    public readonly ElementBounds Bounds;
    public readonly float ScrollY;
    public readonly int HoveredIndex;
    public readonly int SelectedIndex;
    public readonly bool Dragging;
    public readonly double ScaledRowHeight;
    public readonly float TotalHeight;
    public readonly float VisibleHeight;
    public readonly float MaxScroll;
    public readonly bool ScrollNeeded;
    /// <summary>When true, rows are baked into a scrollable buffer texture (viewport + overscan) instead of a fixed viewport texture.</summary>
    public readonly bool Virtualized;
    /// <summary>Extra rows baked beyond each edge of the viewport in virtualized mode.</summary>
    public readonly int OverscanRows;
    /// <summary>Optional override for the row-buffer size in virtualized mode; 0 = size to viewport + overscan.</summary>
    public readonly int MaxRenderedRows;

    public ArcanumListRenderState(
        IReadOnlyList<T> items,
        ElementBounds bounds,
        float scrollY,
        int hoveredIndex,
        int selectedIndex,
        bool dragging,
        double scaledRowHeight,
        float totalHeight,
        float visibleHeight,
        float maxScroll,
        bool scrollNeeded,
        bool virtualized = false,
        int overscanRows = 2,
        int maxRenderedRows = 0)
    {
        Items = items;
        Bounds = bounds;
        ScrollY = scrollY;
        HoveredIndex = hoveredIndex;
        SelectedIndex = selectedIndex;
        Dragging = dragging;
        ScaledRowHeight = scaledRowHeight;
        TotalHeight = totalHeight;
        VisibleHeight = visibleHeight;
        MaxScroll = maxScroll;
        ScrollNeeded = scrollNeeded;
        Virtualized = virtualized;
        OverscanRows = overscanRows;
        MaxRenderedRows = maxRenderedRows;
    }
}

/// <summary>
/// Renders the cached texture for an <see cref="ArcanumList{T}" />: row backgrounds,
/// zebra striping, hover/selection colors, row text and the scrollbar. The owning
/// control feeds in a fresh <see cref="ArcanumListRenderState{T}" /> each frame and
/// the renderer regenerates the texture only when something visible changed.
/// </summary>
/// <typeparam name="T">The element type of the owning list.</typeparam>
internal sealed class ArcanumListRenderer<T> : IDisposable
{
    private const double ScrollbarWidth = 8.0;
    private const double ScrollbarPadding = 3.0;
    private const double MinHandleHeight = 24.0;

    private readonly System.Func<T, string> _label;
    private readonly CairoFont _font;
    private readonly double _textPadding;
    private readonly bool _drawZebra;
    private readonly System.Func<double, double> _scaled;
    private readonly GenerateListTexture _generateTexture;

    private LoadedTexture _texture;
    private string? _textureKey;
    private bool _dirty = true;

    // --- Virtualized mode ---------------------------------------------------
    // Instead of one viewport-sized texture rebaked on every scroll pixel, the
    // list is split into three cached textures:
    //   _chromeTexture : viewport-sized card + scrollbar track (re-baked only on resize)
    //   _rowsTexture   : (visibleRows + 2*overscan)-sized row buffer, drawn offset
    //                    by (bufferTop - scrollY) under a scissor clip. While the
    //                    buffer still covers the viewport it is reused as-is, so
    //                    scrolling costs zero Cairo rebakes until the overscan
    //                    band is crossed.
    //   _handleTexture : scrollbar handle, drawn at a computed offset (re-baked
    //                    only when its size/color state changes).
    private LoadedTexture _chromeTexture;
    private string? _chromeKey;
    private LoadedTexture _rowsTexture;
    private string? _rowsKey;
    private int _bufFirstRow = -1;
    private double _bufTop;         // content-space Y of the rows texture top edge
    private double _bufBottom = -1; // content-space Y of the rows texture bottom edge
    private int _bufWidth;          // texture width the buffer was baked at
    private double _bufRowH;        // row height the buffer was baked at
    private LoadedTexture _handleTexture;
    private string? _handleKey;

    /// <summary>
    /// Creates a renderer bound to the given label selector, font and scaling/texture hooks.
    /// </summary>
    /// <param name="capi">The client API used to allocate the cached texture.</param>
    /// <param name="label">The function mapping an item to its display text.</param>
    /// <param name="font">The font used for row text.</param>
    /// <param name="textPadding">The left padding of row text in unscaled pixels.</param>
    /// <param name="drawZebra">Whether to draw alternating row backgrounds.</param>
    /// <param name="scaled">The GuiElement scaling hook (GuiElement.scaled).</param>
    /// <param name="generateTexture">The GuiElement texture-baking hook.</param>
    public ArcanumListRenderer(
        ICoreClientAPI capi,
        System.Func<T, string> label,
        CairoFont font,
        double textPadding,
        bool drawZebra,
        System.Func<double, double> scaled,
        GenerateListTexture generateTexture)
    {
        _label = label;
        _font = font;
        _textPadding = textPadding;
        _drawZebra = drawZebra;
        _scaled = scaled;
        _generateTexture = generateTexture;
        _texture = new LoadedTexture(capi);
        _chromeTexture = new LoadedTexture(capi);
        _rowsTexture = new LoadedTexture(capi);
        _handleTexture = new LoadedTexture(capi);
        GuiTextureTracker.Register(nameof(ArcanumListRenderer<T>));
    }

    /// <summary>Marks the cached texture as stale so it is regenerated on the next render.</summary>
    public void MarkDirty()
    {
        _dirty = true;
        _textureKey = null;
        _chromeKey = null;
        _rowsKey = null;
        _handleKey = null;
    }

    /// <summary>
    /// Draws the cached texture(s) at the list's screen position. Call this every frame
    /// regardless of whether the texture was regenerated.
    /// </summary>
    /// <param name="api">The client API used to access the 2D renderer.</param>
    /// <param name="state">The current dynamic render state (same snapshot passed to <see cref="Render" />).</param>
    public void Draw(ICoreClientAPI api, in ArcanumListRenderState<T> state)
    {
        if (api?.Render == null || state.Bounds == null) return;

        if (!state.Virtualized)
        {
            if (_texture.TextureId > 0)
            {
                api.Render.Render2DLoadedTexture(_texture, (float)state.Bounds.absX, (float)state.Bounds.absY);
            }
            return;
        }

        double ax = state.Bounds.absX;
        double ay = state.Bounds.absY;

        if (_chromeTexture.TextureId > 0)
        {
            api.Render.Render2DLoadedTexture(_chromeTexture, (float)ax, (float)ay);
        }

        if (_rowsTexture.TextureId > 0)
        {
            // The rows buffer starts at content-Y _bufTop; shift it by the current
            // scroll offset and clip to the viewport so the overscan band is hidden.
            float rowsY = (float)(ay + _bufTop - state.ScrollY);
            api.Render.PushScissor(state.Bounds, true);
            try
            {
                api.Render.Render2DLoadedTexture(_rowsTexture, (float)ax, rowsY);
            }
            finally
            {
                api.Render.PopScissor();
            }
        }

        if (state.ScrollNeeded && _handleTexture.TextureId > 0)
        {
            var (hX, hY, _, _) = ScrollbarHandleRect(state);
            api.Render.Render2DLoadedTexture(_handleTexture, (float)(ax + hX), (float)(ay + hY));
        }
    }

    /// <summary>
    /// Regenerates the cached texture when the visible state has changed, then it is
    /// ready to be drawn via <see cref="Draw" />. Safe to call every frame.
    /// </summary>
    /// <param name="api">The client API used for logging on failure.</param>
    /// <param name="state">The current dynamic render state.</param>
    public void Render(ICoreClientAPI api, in ArcanumListRenderState<T> state)
    {
        if (state.Bounds == null || api?.Render == null) return;

        if (state.Virtualized)
        {
            // Capture and clear the global dirty flag up front; each Ensure* uses it
            // as "force regen" and re-arms _dirty itself when its bake fails.
            bool wasDirty = _dirty;
            _dirty = false;
            RenderVirtualized(api, state, wasDirty);
            return;
        }

        ReleaseVirtualizedTextures(api);
        RenderLegacy(api, state);
    }

    /// <summary>
    /// Legacy (non-virtualized) path: one viewport-sized texture containing card,
    /// rows and scrollbar. Re-baked whenever the key changes (scroll pixel, hover,
    /// selection, resize, item count).
    /// </summary>
    private void RenderLegacy(ICoreClientAPI api, in ArcanumListRenderState<T> state)
    {
        int width = Math.Max(4, (int)state.Bounds.OuterWidth);
        int height = Math.Max(4, (int)state.Bounds.OuterHeight);

        // Round scrollY to whole pixels so smooth wheel scrolling does not regenerate
        // the texture on every fractional change (was :F2, causing ~100 regen/s).
        string newKey = $"{width}|{height}|{(int)Math.Round(state.ScrollY)}|{state.HoveredIndex}|{state.SelectedIndex}|{state.Items.Count}";
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

            double contentW = state.ScrollNeeded
                ? width - _scaled(ScrollbarWidth) - _scaled(ScrollbarPadding) * 2.0
                : width;

            if (state.Items.Count > 0)
            {
                int firstRow = Math.Max(0, (int)(state.ScrollY / state.ScaledRowHeight));
                int lastRow = Math.Min(state.Items.Count - 1, (int)((state.ScrollY + height) / state.ScaledRowHeight) + 1);

                for (int i = firstRow; i <= lastRow; i++)
                {
                    double rowY = i * state.ScaledRowHeight - state.ScrollY;

                    // Row background
                    RGBA bgColor;
                    if (i == state.SelectedIndex)
                    {
                        bgColor = ArcanumGuiTheme.StatusActive.WithAlpha(0.85);
                    }
                    else if (i == state.HoveredIndex)
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
                        ctx.Rectangle(0, rowY, contentW, state.ScaledRowHeight);
                        bgColor.Apply(ctx);
                        ctx.Fill();
                    }

                    // Row text
                    string? label = _label(state.Items[i]) ?? "";
                    if (string.IsNullOrWhiteSpace(label)) continue;

                    _font.SetupContext(ctx);

                    RGBA textColor = i == state.SelectedIndex || i == state.HoveredIndex
                        ? ArcanumGuiTheme.TextPrimary
                        : ArcanumGuiTheme.TextSecondary;
                    textColor.Apply(ctx);

                    var ext = ctx.TextExtents(label);
                    double x = _scaled(_textPadding) - ext.XBearing;
                    double y = rowY + (state.ScaledRowHeight - ext.Height) / 2.0 - ext.YBearing;

                    ctx.MoveTo(x, y);
                    ctx.ShowText(label);
                }
            }

            ctx.ResetClip();

            // Scrollbar
            if (state.ScrollNeeded)
            {
                double trackX = state.Bounds.OuterWidth - _scaled(ScrollbarWidth) - _scaled(ScrollbarPadding);
                ArcanumGuiTheme.FillRoundedRect(
                    ctx, trackX, 0, _scaled(ScrollbarWidth), height,
                    _scaled(ScrollbarWidth / 2.0),
                    ArcanumGuiTheme.SurfaceDeepest.WithAlpha(0.85));
                ArcanumGuiTheme.StrokeRoundedRect(
                    ctx, trackX, 0, _scaled(ScrollbarWidth), height,
                    _scaled(ScrollbarWidth / 2.0),
                    ArcanumGuiTheme.BorderSubtle, GuiElement.scaled(1.0));

                var (hX, hY, hW, hH) = ScrollbarHandleRect(state);
                RGBA handleColor = state.Dragging || state.HoveredIndex == -2
                    ? ArcanumGuiTheme.Accent.WithAlpha(0.95)
                    : ArcanumGuiTheme.AccentDim.Lerp(ArcanumGuiTheme.Accent, 0.55);
                ArcanumGuiTheme.FillRoundedRect(
                    ctx, hX, hY, hW, hH,
                    hW / 2.0,
                    handleColor);
            }

            _generateTexture(surface, ref _texture);
            GuiTextureTracker.Regen(nameof(ArcanumListRenderer<T>));
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

    // ====================================================================
    //  Virtualized rendering path
    // ====================================================================

    /// <summary>
    /// Virtualized path: maintains a chrome texture (card + scrollbar track), a
    /// scrollable rows buffer (viewport + overscan rows) and a handle texture.
    /// While the rows buffer still covers the viewport it is drawn offset-only,
    /// so scrolling re-bakes nothing until the overscan band is crossed.
    /// </summary>
    private void RenderVirtualized(ICoreClientAPI api, in ArcanumListRenderState<T> state, bool wasDirty)
    {
        // Free the legacy texture if we just switched into virtualized mode.
        if (_texture.TextureId > 0)
        {
            _texture.Dispose();
            _texture = new LoadedTexture(api);
            _textureKey = null;
        }

        int width = Math.Max(4, (int)state.Bounds.OuterWidth);
        int vh = Math.Max(4, (int)state.Bounds.OuterHeight);
        double rowH = Math.Max(1.0, state.ScaledRowHeight);
        int count = state.Items.Count;

        EnsureChrome(api, state, width, vh, wasDirty);
        EnsureRows(api, state, width, vh, rowH, count, wasDirty);
        EnsureHandle(api, state, wasDirty);
    }

    /// <summary>Disposes virtualized-mode textures; called when leaving virtualized mode.</summary>
    private void ReleaseVirtualizedTextures(ICoreClientAPI api)
    {
        if (_chromeTexture.TextureId > 0) { _chromeTexture.Dispose(); _chromeTexture = new LoadedTexture(api); }
        if (_rowsTexture.TextureId > 0) { _rowsTexture.Dispose(); _rowsTexture = new LoadedTexture(api); }
        if (_handleTexture.TextureId > 0) { _handleTexture.Dispose(); _handleTexture = new LoadedTexture(api); }
        _chromeKey = _rowsKey = _handleKey = null;
        _bufFirstRow = -1;
        _bufTop = 0;
        _bufBottom = -1;
    }

    /// <summary>View-size card background + scrollbar track. Only re-baked on resize or scroll-need change.</summary>
    private void EnsureChrome(ICoreClientAPI api, in ArcanumListRenderState<T> state, int width, int vh, bool wasDirty)
    {
        string key = $"{width}|{vh}|{state.ScrollNeeded}|{GuiElement.scaled(1.0):F2}";
        if (!wasDirty && string.Equals(_chromeKey, key, StringComparison.Ordinal) && _chromeTexture.TextureId > 0)
            return;
        _chromeKey = key;

        ImageSurface? surface = null;
        Context? ctx = null;
        try
        {
            _chromeTexture.Dispose();
            _chromeTexture = new LoadedTexture(api);

            surface = new ImageSurface(Format.Argb32, width, vh);
            ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            ArcanumGuiTheme.FillRoundedRect(
                ctx, 0, 0, width, vh,
                GuiElement.scaled(ArcanumGuiTheme.Radius.Medium),
                ArcanumGuiTheme.SurfaceDeepest.WithAlpha(0.65));
            ArcanumGuiTheme.StrokeRoundedRect(
                ctx, 0, 0, width, vh,
                GuiElement.scaled(ArcanumGuiTheme.Radius.Medium),
                ArcanumGuiTheme.BorderSubtle, GuiElement.scaled(1.0));

            if (state.ScrollNeeded)
            {
                double trackX = state.Bounds.OuterWidth - _scaled(ScrollbarWidth) - _scaled(ScrollbarPadding);
                ArcanumGuiTheme.FillRoundedRect(
                    ctx, trackX, 0, _scaled(ScrollbarWidth), vh,
                    _scaled(ScrollbarWidth / 2.0),
                    ArcanumGuiTheme.SurfaceDeepest.WithAlpha(0.85));
                ArcanumGuiTheme.StrokeRoundedRect(
                    ctx, trackX, 0, _scaled(ScrollbarWidth), vh,
                    _scaled(ScrollbarWidth / 2.0),
                    ArcanumGuiTheme.BorderSubtle, GuiElement.scaled(1.0));
            }

            _generateTexture(surface, ref _chromeTexture);
            GuiTextureTracker.Regen(nameof(ArcanumListRenderer<T>));
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumList] Failed to generate chrome texture: {0}", ex);
            _chromeKey = null;
            _dirty = true;
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
        }
    }

    /// <summary>
    /// Row buffer covering <c>bufferRows</c> starting at <c>_bufFirstRow</c>. Re-baked
    /// when the buffer no longer covers the viewport, or when hover/selection/items
    /// change — but NOT on every scroll pixel.
    /// </summary>
    private void EnsureRows(ICoreClientAPI api, in ArcanumListRenderState<T> state, int width, int vh, double rowH, int count, bool wasDirty)
    {
        if (count <= 0)
        {
            if (_rowsTexture.TextureId > 0)
            {
                _rowsTexture.Dispose();
                _rowsTexture = new LoadedTexture(api);
            }
            _rowsKey = null;
            _bufFirstRow = -1;
            _bufTop = 0;
            _bufBottom = -1;
            return;
        }

        int overscan = Math.Max(0, state.OverscanRows);

        // Rows needed to fill the viewport, incl. the partially visible bottom row.
        int minNeeded = (int)Math.Ceiling(vh / rowH) + 1;

        int bufferRows = Math.Min(count, minNeeded + 2 * overscan);
        if (state.MaxRenderedRows > 0)
        {
            // Explicit buffer-size override; never allowed to under-fill the viewport.
            bufferRows = Math.Min(count, Math.Max(state.MaxRenderedRows, minNeeded));
        }
        // Texture safety cap (~32k px Cairo surface limit; ~24 px floor per row).
        bufferRows = Math.Min(bufferRows, Math.Max(minNeeded, (int)(30000.0 / rowH)));
        bufferRows = Math.Max(1, bufferRows);

        // Does the current buffer still cover the viewport? If so keep _bufFirstRow
        // so the texture key stays stable and the buffer is reused offset-only.
        // Width/rowH must match too — a resize invalidates the coverage math.
        bool covered = _rowsTexture.TextureId > 0
            && _bufWidth == width
            && Math.Abs(_bufRowH - rowH) < 0.001
            && _bufTop <= state.ScrollY + 0.5
            && _bufBottom >= state.ScrollY + vh - 0.5;

        int firstRow;
        if (covered)
        {
            firstRow = _bufFirstRow;
        }
        else
        {
            int firstVisible = Math.Max(0, (int)(state.ScrollY / rowH));
            firstRow = Math.Max(0, Math.Min(firstVisible - overscan, count - bufferRows));

            // Bottom-coverage guard for tight buffers: slide the window down if needed.
            int needTop = Math.Max(0, (int)Math.Ceiling((state.ScrollY + vh) / rowH) - bufferRows);
            if (firstRow < needTop)
            {
                firstRow = Math.Min(needTop, Math.Max(0, count - bufferRows));
            }
        }

        string key = $"{width}|{bufferRows}|{firstRow}|{state.HoveredIndex}|{state.SelectedIndex}|{count}|{rowH:F2}";
        if (!wasDirty && covered && string.Equals(_rowsKey, key, StringComparison.Ordinal))
            return;
        _rowsKey = key;

        int bufCount = Math.Max(0, Math.Min(bufferRows, count - firstRow));
        int texH = Math.Max(4, (int)Math.Ceiling(bufCount * rowH));
        _bufFirstRow = firstRow;
        _bufTop = firstRow * rowH;
        _bufBottom = (firstRow + bufCount) * rowH;
        _bufWidth = width;
        _bufRowH = rowH;

        double contentW = state.ScrollNeeded
            ? width - _scaled(ScrollbarWidth) - _scaled(ScrollbarPadding) * 2.0
            : width;

        ImageSurface? surface = null;
        Context? ctx = null;
        try
        {
            _rowsTexture.Dispose();
            _rowsTexture = new LoadedTexture(api);

            surface = new ImageSurface(Format.Argb32, width, texH);
            ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            for (int i = firstRow; i < firstRow + bufCount; i++)
            {
                double rowY = (i - firstRow) * rowH;

                // Row background
                RGBA bgColor;
                if (i == state.SelectedIndex)
                {
                    bgColor = ArcanumGuiTheme.StatusActive.WithAlpha(0.85);
                }
                else if (i == state.HoveredIndex)
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
                    ctx.Rectangle(0, rowY, contentW, rowH);
                    bgColor.Apply(ctx);
                    ctx.Fill();
                }

                // Row text
                string? label = _label(state.Items[i]) ?? "";
                if (string.IsNullOrWhiteSpace(label)) continue;

                _font.SetupContext(ctx);

                RGBA textColor = i == state.SelectedIndex || i == state.HoveredIndex
                    ? ArcanumGuiTheme.TextPrimary
                    : ArcanumGuiTheme.TextSecondary;
                textColor.Apply(ctx);

                var ext = ctx.TextExtents(label);
                double x = _scaled(_textPadding) - ext.XBearing;
                double y = rowY + (rowH - ext.Height) / 2.0 - ext.YBearing;

                ctx.MoveTo(x, y);
                ctx.ShowText(label);
            }

            _generateTexture(surface, ref _rowsTexture);
            GuiTextureTracker.Regen(nameof(ArcanumListRenderer<T>));
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumList] Failed to generate rows texture: {0}", ex);
            _rowsKey = null;
            _dirty = true;
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
        }
    }

    /// <summary>Scrollbar handle as its own small texture; re-baked only when size or color state changes.</summary>
    private void EnsureHandle(ICoreClientAPI api, in ArcanumListRenderState<T> state, bool wasDirty)
    {
        if (!state.ScrollNeeded)
        {
            if (_handleTexture.TextureId > 0)
            {
                _handleTexture.Dispose();
                _handleTexture = new LoadedTexture(api);
            }
            _handleKey = null;
            return;
        }

        var (_, _, hW, hH) = ScrollbarHandleRect(state);
        int w = Math.Max(2, (int)Math.Ceiling(hW));
        int h = Math.Max(2, (int)Math.Ceiling(hH));

        string key = $"{w}|{h}|{state.Dragging}|{state.HoveredIndex == -2}";
        if (!wasDirty && string.Equals(_handleKey, key, StringComparison.Ordinal) && _handleTexture.TextureId > 0)
            return;
        _handleKey = key;

        ImageSurface? surface = null;
        Context? ctx = null;
        try
        {
            _handleTexture.Dispose();
            _handleTexture = new LoadedTexture(api);

            surface = new ImageSurface(Format.Argb32, w, h);
            ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            RGBA handleColor = state.Dragging || state.HoveredIndex == -2
                ? ArcanumGuiTheme.Accent.WithAlpha(0.95)
                : ArcanumGuiTheme.AccentDim.Lerp(ArcanumGuiTheme.Accent, 0.55);
            ArcanumGuiTheme.FillRoundedRect(ctx, 0, 0, w, h, w / 2.0, handleColor);

            _generateTexture(surface, ref _handleTexture);
            GuiTextureTracker.Regen(nameof(ArcanumListRenderer<T>));
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumList] Failed to generate handle texture: {0}", ex);
            _handleKey = null;
            _dirty = true;
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
        }
    }

    /// <summary>
    /// Determines whether the given screen-space mouse X falls within the scrollbar
    /// track area. Used by the owning control to decide between scrollbar dragging and
    /// row selection on mouse down.
    /// </summary>
    /// <param name="mouseX">The screen-space mouse X coordinate.</param>
    /// <param name="bounds">The list bounds.</param>
    /// <param name="scrollNeeded">Whether a scrollbar is currently shown.</param>
    /// <returns>True if the mouse is over the scrollbar track.</returns>
    public bool IsOnScrollbar(int mouseX, ElementBounds bounds, bool scrollNeeded)
    {
        if (bounds == null || !scrollNeeded) return false;
        double localX = mouseX - bounds.absX;
        double trackX = bounds.OuterWidth - _scaled(ScrollbarWidth) - _scaled(ScrollbarPadding);
        return localX >= trackX;
    }

    /// <summary>
    /// Computes the scrollbar handle rectangle for the given state. Exposed so the
    /// owning control can hit-test and drag the handle consistently with rendering.
    /// </summary>
    /// <param name="state">The current dynamic render state.</param>
    /// <returns>The (x, y, width, height) handle rectangle in list-local pixels.</returns>
    public (double x, double y, double w, double h) ScrollbarHandleRect(in ArcanumListRenderState<T> state)
    {
        double trackH = state.Bounds.OuterHeight;
        double ratio = state.VisibleHeight / Math.Max(1f, state.TotalHeight);
        double handleH = Math.Max(_scaled(MinHandleHeight), trackH * ratio);
        double range = trackH - handleH;
        double t = state.MaxScroll > 0.001f ? state.ScrollY / state.MaxScroll : 0.0;
        double trackX = state.Bounds.OuterWidth - _scaled(ScrollbarWidth) - _scaled(ScrollbarPadding);
        double handleY = range * t;
        return (trackX, handleY, _scaled(ScrollbarWidth), handleH);
    }

    /// <summary>Releases the cached list textures.</summary>
    public void Dispose()
    {
        _texture?.Dispose();
        _chromeTexture?.Dispose();
        _rowsTexture?.Dispose();
        _handleTexture?.Dispose();
        GuiTextureTracker.Unregister(nameof(ArcanumListRenderer<T>));
    }
}
