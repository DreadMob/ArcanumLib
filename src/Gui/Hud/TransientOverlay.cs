using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic full-screen transient overlay (toast, achievement, combat, milestone, etc.).
/// Manages auto-open, optional sound, elapsed-time tracking and auto-close.
/// Derived types implement <see cref="OnDrawContent" /> and set <see cref="DurationSeconds" />.
/// </summary>
/// <typeparam name="TModel">The type of the tmodel value.</typeparam>
public abstract class TransientOverlay<TModel> : GuiDialog
    where TModel : class
{
    /// <summary>Dialog should not block the rest of the UI.</summary>
    public override string ToggleKeyCombinationCode => null!;

    /// <summary>Overlay draws above the world.</summary>
    public override EnumDialogType DialogType => EnumDialogType.HUD;

    /// <summary>Should not grab the mouse.</summary>
    public override bool PrefersUngrabbedMouse => false;

    /// <summary>Should not receive mouse events.</summary>
    /// <returns>true if the operation should receive mouse events; otherwise, false.</returns>
    public override bool ShouldReceiveMouseEvents() => false;

    /// <summary>Current data to display.</summary>
    protected TModel? _data;

    /// <summary>Elapsed time since the overlay opened.</summary>
    protected float _elapsed;

    /// <summary>Total duration before the overlay closes.</summary>
    protected virtual float DurationSeconds => 5f;

    /// <summary>Optional sound to play when the overlay opens. Empty = no sound.</summary>
    protected virtual string? OpenSound => null;

    /// <summary>Unique composer name. Override per mod.</summary>
    protected abstract string OverlayName { get; }

    /// <summary>Custom draw key used in the composer. Override if the derived type uses a different name.</summary>
    protected virtual string DrawKey => "overlay";

    /// <summary>Creates the overlay.</summary>
    /// <param name="capi">The client API instance.</param>
    protected TransientOverlay(ICoreClientAPI capi) : base(capi) { }

    /// <summary>Shows the overlay with the given data, recomposing if already open.</summary>
    /// <param name="data">The associated data.</param>
    public virtual void Show(TModel? data)
    {
        _data = data;
        _elapsed = 0f;
        Compose();
        if (!IsOpened()) TryOpen();
    }

    /// <summary>Plays the configured open sound.</summary>
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        if (!string.IsNullOrWhiteSpace(OpenSound) && capi != null)
            capi.Gui.PlaySound(new AssetLocation(OpenSound));
    }

    /// <summary>
    /// Composes the full-screen overlay with a dynamic custom draw callback.
    /// </summary>
    protected virtual void Compose()
    {
        if (capi?.Gui == null) return;

        var dialogBounds = ElementBounds.Fill;
        var drawBounds = ElementBounds.Fill;
        drawBounds.WithParent(dialogBounds);

        SingleComposer = capi.Gui
            .CreateCompo(OverlayName, dialogBounds)
            .AddDynamicCustomDraw(drawBounds, OnDrawInternal, DrawKey)
            .Compose();
    }

    /// <summary>
    /// Draw callback passed to Vintage Story. Forwards to <see cref="OnDrawContent" />.
    /// </summary>
    /// <param name="ctx">The ctx value.</param>
    /// <param name="surface">The surface value.</param>
    /// <param name="bounds">The bounds value.</param>
    protected virtual void OnDrawInternal(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        OnDrawContent(ctx, surface, bounds, _elapsed, _data);
    }

    /// <summary>
    /// Draws the overlay content. <paramref name="elapsed" /> is in seconds.
    /// </summary>
    /// <param name="ctx">The ctx value.</param>
    /// <param name="surface">The surface value.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <param name="elapsed">The elapsed value.</param>
    /// <param name="data">The associated data.</param>
    protected abstract void OnDrawContent(Context ctx, ImageSurface surface, ElementBounds bounds, float elapsed, TModel? data);

    /// <summary>Returns true when the overlay should be redrawn this frame. Override to reduce CPU.</summary>
    /// <param name="elapsed">The elapsed value.</param>
    /// <returns>true if the operation should redraw; otherwise, false.</returns>
    protected virtual bool ShouldRedraw(float elapsed) => true;

    /// <summary>Updates elapsed time, redraws and closes the overlay when the duration expires.</summary>
    /// <param name="deltaTime">The delta time value.</param>
    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);

        _elapsed += deltaTime;

        if (_elapsed >= DurationSeconds)
        {
            TryClose();
            return;
        }

        if (ShouldRedraw(_elapsed))
            SingleComposer?.GetCustomDraw(DrawKey)?.Redraw();
    }

    /// <summary>Resets data and closes.</summary>
    /// <returns>true if the operation succeeded; otherwise, false.</returns>
    public override bool TryClose()
    {
        _data = null;
        return base.TryClose();
    }
}
