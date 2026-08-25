using System;
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
        bool scrollNeeded)
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
    }

    /// <summary>Marks the cached texture as stale so it is regenerated on the next render.</summary>
    public void MarkDirty()
    {
        _dirty = true;
        _textureKey = null;
    }

    /// <summary>
    /// Draws the cached texture at the list's screen position. Call this every frame
    /// regardless of whether the texture was regenerated.
    /// </summary>
    /// <param name="api">The client API used to access the 2D renderer.</param>
    /// <param name="bounds">The list bounds providing the screen origin.</param>
    public void Draw(ICoreClientAPI api, ElementBounds bounds)
    {
        if (_texture.TextureId > 0)
        {
            api?.Render?.Render2DLoadedTexture(_texture, (float)bounds.absX, (float)bounds.absY);
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

        int width = Math.Max(1, (int)state.Bounds.OuterWidth);
        int height = Math.Max(1, (int)state.Bounds.OuterHeight);

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

    /// <summary>Releases the cached list texture.</summary>
    public void Dispose()
    {
        _texture?.Dispose();
    }
}
