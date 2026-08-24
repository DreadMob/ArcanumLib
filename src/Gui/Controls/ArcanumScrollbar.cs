using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed vertical scrollbar for the Arcanum GUI toolkit. Slim track + accent handle.
/// Mirrors the small subset of the standard <see cref="GuiElementScrollbar" /> API the
/// dialog actually uses: <see cref="SetHeights" />, <see cref="SetNewTotalHeight" />,
/// <see cref="SetScrollbarPosition" />, plus a value callback fired in content pixels
/// so existing scroll handlers keep working.
/// </summary>
public class ArcanumScrollbar : GuiElement
{
    private readonly Action<float> onNewValue;

    private float visibleHeight;     // pixels visible (clip viewport height)
    private float totalHeight;       // total content height
    private float currentValue;      // pixels scrolled (0..maxValue)

    private bool dragging;
    private double dragStartMouseY;
    private float dragStartValue;
    private bool hovered;

    // Auto-hide handle when content does not need scrolling.
    private bool ScrollNeeded => totalHeight > visibleHeight + 0.1f;

    /// <summary>
    /// Maximum scroll offset in content pixels.
    /// </summary>
    public float MaxValue => Math.Max(0f, totalHeight - visibleHeight);

    /// <summary>
    /// When false, the scrollbar does not react to the mouse wheel itself (useful when a paired list already handles scrolling).
    /// </summary>
    public bool HandleMouseWheel { get; set; } = true;

    /// <summary>
    /// Creates a new scrollbar. The <paramref name="onNewValue" /> callback receives the
    /// current scroll offset in content pixels.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="onNewValue">The callback to invoke.</param>
    public ArcanumScrollbar(ICoreClientAPI capi, ElementBounds bounds, Action<float> onNewValue)
        : base(capi, bounds)
    {
        this.onNewValue = onNewValue;
        visibleHeight = (float)bounds.fixedHeight;
        totalHeight = visibleHeight;
        trackTexture = new LoadedTexture(capi);
        handleTexture = new LoadedTexture(capi);
    }

    // --------------------------------------------------------------
    // Public API (mirrors the small surface used elsewhere).
    // --------------------------------------------------------------

    /// <summary>
    /// Sets the visible and total content heights, clamping the current value.
    /// </summary>
    /// <param name="visible">The visible value.</param>
    /// <param name="total">The total value.</param>
    public void SetHeights(float visible, float total)
    {
        visibleHeight = Math.Max(1f, visible);
        totalHeight = Math.Max(visibleHeight, total);
        currentValue = GameMath.Clamp(currentValue, 0f, MaxValue);
    }

    /// <summary>
    /// Updates the total content height while keeping the visible height.
    /// </summary>
    /// <param name="total">The total value.</param>
    public void SetNewTotalHeight(float total)
    {
        totalHeight = Math.Max(visibleHeight, total);
        currentValue = GameMath.Clamp(currentValue, 0f, MaxValue);
    }

    /// <summary>
    /// Sets the scroll position and notifies the callback.
    /// </summary>
    /// <param name="value">The value to set or compare.</param>
    public void SetScrollbarPosition(float value)
    {
        currentValue = GameMath.Clamp(value, 0f, MaxValue);
        onNewValue?.Invoke(currentValue);
    }

    /// <summary>
    /// Current scroll offset in content pixels.
    /// </summary>
    public float CurrentYPosition => currentValue;

    // --------------------------------------------------------------
    // Rendering.
    // --------------------------------------------------------------

    /// <summary>Performs the compose elements operation.</summary>
    /// <param name="ctxStatic">The ctx static value.</param>
    /// <param name="surfaceStatic">The surface static value.</param>
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <summary>Performs the render interactive elements operation.</summary>
    /// <param name="deltaTime">The delta time value.</param>
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        if (!ScrollNeeded) return;

        Bounds.CalcWorldBounds();

        // Hover detection.
        try
        {
            hovered = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        }
        catch (Exception ex)
        {
            hovered = false;
            api?.Logger?.Warning("[ArcanumScrollbar] Hover detection failed: {0}", ex);
        }

        // Track.
        double trackX = Bounds.absX + scaled(3.0);
        double trackY = Bounds.absY;
        double trackW = Math.Max(2.0, Bounds.InnerWidth - scaled(6.0));
        double trackH = Bounds.InnerHeight;

        api?.Render?.Render2DLoadedTexture(GetOrCreateTrack(), (float)trackX, (float)trackY);

        // Handle.
        (double handleY, double handleH) = ComputeHandleRect();
        api?.Render?.Render2DLoadedTexture(GetOrCreateHandle(), (float)Bounds.absX, (float)handleY);
    }

    private (double y, double h) ComputeHandleRect()
    {
        double trackH = Bounds.InnerHeight;
        double ratio = visibleHeight / Math.Max(1f, totalHeight);
        double handleH = Math.Max(scaled(28.0), trackH * ratio);

        double range = trackH - handleH;
        double t = MaxValue > 0.001 ? currentValue / MaxValue : 0.0;
        double y = Bounds.absY + range * t;
        return (y, handleH);
    }

    // --------------------------------------------------------------
    // Cached textures (track / handle).  Recreated when Bounds change.
    // --------------------------------------------------------------

    private LoadedTexture? trackTexture;
    private LoadedTexture? handleTexture;
    private (int w, int h) trackSize;
    private (int w, int h) handleSize;
    private bool handleHoveredCache;

    private LoadedTexture GetOrCreateTrack()
    {
        int w = Math.Max(2, (int)Math.Round(Bounds.InnerWidth - scaled(6.0)));
        int h = Math.Max(2, (int)Math.Round(Bounds.InnerHeight));

        if (trackTexture == null || trackTexture.TextureId == 0 || trackSize != (w, h))
        {
            trackTexture?.Dispose();
            trackTexture = new LoadedTexture(api!);
            trackSize = (w, h);

            if (api?.Render == null) return trackTexture!;

            using var surface = new ImageSurface(Format.Argb32, w, h);
            using var ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            double r = w / 2.0;
            ArcanumGuiTheme.FillRoundedRect(ctx, 0, 0, w, h, r,
                ArcanumGuiTheme.SurfaceDeepest.WithAlpha(0.65));
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0, 0, w, h, r,
                ArcanumGuiTheme.BorderSubtle, 1.0);

            try
            {
                generateTexture(surface, ref trackTexture);
            }
            catch (Exception ex)
            {
                trackTexture?.Dispose();
                trackTexture = new LoadedTexture(api!);
                api?.Logger?.Warning("[ArcanumScrollbar] Track texture generation failed: {0}", ex);
            }
        }
        return trackTexture!;
    }

    private LoadedTexture GetOrCreateHandle()
    {
        int w = Math.Max(2, (int)Math.Round(Bounds.InnerWidth));
        (double _, double hd) = ComputeHandleRect();
        int h = Math.Max(8, (int)Math.Round(hd));
        bool hov = hovered || dragging;

        if (handleTexture == null || handleTexture.TextureId == 0 || handleSize != (w, h) || handleHoveredCache != hov)
        {
            handleTexture?.Dispose();
            handleTexture = new LoadedTexture(api!);
            handleSize = (w, h);
            handleHoveredCache = hov;

            if (api?.Render == null) return handleTexture!;

            using var surface = new ImageSurface(Format.Argb32, w, h);
            using var ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            double r = w / 2.0;
            RGBA fill = hov
                ? ArcanumGuiTheme.Accent.WithAlpha(0.85)
                : ArcanumGuiTheme.AccentDim.Lerp(ArcanumGuiTheme.Accent, 0.55);

            ArcanumGuiTheme.FillRoundedRect(ctx, 0, 0, w, h, r, fill);
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0, 0, w, h, r,
                ArcanumGuiTheme.Accent.WithAlpha(hov ? 0.95 : 0.6), 1.0);

            try
            {
                generateTexture(surface, ref handleTexture);
            }
            catch (Exception ex)
            {
                handleTexture?.Dispose();
                handleTexture = new LoadedTexture(api!);
                api?.Logger?.Warning("[ArcanumScrollbar] Handle texture generation failed: {0}", ex);
            }
        }
        return handleTexture!;
    }

    // --------------------------------------------------------------
    // Mouse interaction.
    // --------------------------------------------------------------

    /// <summary>Performs the on mouse down on element operation.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!ScrollNeeded) return;
        if (Bounds?.ParentBounds == null) return;
        if (!Bounds.PointInside(args.X, args.Y)) return;

        (double handleY, double handleH) = ComputeHandleRect();

        // Click on track outside the handle - jump.
        if (args.Y < handleY || args.Y > handleY + handleH)
        {
            double centerYT = (args.Y - Bounds.absY - handleH / 2.0) / Math.Max(1.0, Bounds.InnerHeight - handleH);
            centerYT = GameMath.Clamp(centerYT, 0.0, 1.0);
            currentValue = (float)(centerYT * MaxValue);
            onNewValue?.Invoke(currentValue);
        }

        dragging = true;
        dragStartMouseY = args.Y;
        dragStartValue = currentValue;
        args.Handled = true;
    }

    /// <summary>Performs the on mouse move operation.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!dragging) return;

        (double _, double handleH) = ComputeHandleRect();
        double pixelRange = Math.Max(1.0, Bounds.InnerHeight - handleH);
        double dy = args.Y - dragStartMouseY;
        double tDelta = dy / pixelRange;
        float newValue = dragStartValue + (float)(tDelta * MaxValue);
        newValue = GameMath.Clamp(newValue, 0f, MaxValue);

        if (Math.Abs(newValue - currentValue) > 0.5f)
        {
            currentValue = newValue;
            onNewValue?.Invoke(currentValue);
        }
        args.Handled = true;
    }

    /// <summary>Performs the on mouse up operation.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (dragging)
        {
            dragging = false;
            args.Handled = true;
        }
    }

    /// <summary>Performs the on mouse wheel operation.</summary>
    /// <param name="api">The client API instance.</param>
    /// <param name="args">The arguments.</param>
    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (!HandleMouseWheel) return;
        if (args.IsHandled) return;
        if (!ScrollNeeded) return;
        if (Bounds?.ParentBounds == null) return;

        // Allow wheel scrolling when mouse is inside scrollbar OR inside its parent (the body area).
        int mx = api.Input.MouseX;
        int my = api.Input.MouseY;
        bool inside = false;
        try
        {
            inside = Bounds.PointInside(mx, my)
                || (Bounds.ParentBounds != null && Bounds.ParentBounds.PointInside(mx, my));
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumScrollbar] Wheel input test failed: {0}", ex);
        }
        if (!inside) return;

        float step = visibleHeight * 0.18f;
        currentValue = GameMath.Clamp(currentValue - args.deltaPrecise * step, 0f, MaxValue);
        onNewValue?.Invoke(currentValue);
        args.SetHandled(true);
    }

    /// <summary>Releases all resources used by the current object.</summary>
    public override void Dispose()
    {
        trackTexture?.Dispose();
        handleTexture?.Dispose();
        trackTexture = null;
        handleTexture = null;
        base.Dispose();
    }
}

/// <summary>
/// Composer extension methods for adding <see cref="ArcanumScrollbar" /> elements.
/// </summary>
public static class ArcanumScrollbarComposerHelpers
{
    /// <summary>
    /// Adds a themed vertical scrollbar to the composer.
    /// </summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="onNewValue">The callback to invoke.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add arcanum scrollbar.</returns>
    public static GuiComposer AddArcanumScrollbar(
        this GuiComposer composer,
        Action<float> onNewValue,
        ElementBounds bounds,
        string? key = null)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(new ArcanumScrollbar(composer.Api, bounds, onNewValue), key);
        }
        return composer;
    }
}
