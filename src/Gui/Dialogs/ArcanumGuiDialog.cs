using System;
using ArcanumLib.Gui.Theme;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Dialogs;

/// <summary>
/// Base class for Arcanum-themed <see cref="GuiDialog" />s.
/// Handles recomposition on the main thread and provides common bounds helpers.
/// </summary>
public abstract class ArcanumGuiDialog : GuiDialog
{
    private bool _recomposeQueued;

    /// <summary>Always unregister this dialog from the GUI when closed so it does not linger.</summary>
    public override bool UnregisterOnClose => true;

    /// <summary>Creates a base dialog bound to the given client API.</summary>
    /// <param name="capi">The client API instance.</param>
    protected ArcanumGuiDialog(ICoreClientAPI capi) : base(capi) { }

    /// <summary>
    /// Optional per-dialog theme. When non-null it is installed via
    /// <see cref="ArcanumGuiTheme.WithPalette"/> for the duration of each
    /// compose (<see cref="Recompose"/>) and render (<see cref="OnRenderGUI"/>)
    /// call — controls read the palette during BOTH passes, so a compose-only
    /// scope is not enough. The scope never outlives a single call, so close
    /// order and nested dialogs can never leave a foreign palette installed.
    /// </summary>
    protected virtual GuiThemePalette? DialogPalette => null;

    /// <summary>
    /// Triggers a recompose on the main thread. Safe to call from background threads.
    /// </summary>
    protected void RequestRecompose()
    {
        if (_recomposeQueued) return;
        _recomposeQueued = true;

        capi.Event.EnqueueMainThreadTask(() =>
        {
            _recomposeQueued = false;
            Recompose();
        }, $"{GetType().Name}-recompose");
    }

    /// <summary>
    /// Disposes the existing composer and calls <see cref="BuildComposer" />.
    /// </summary>
    protected void Recompose()
    {
        if (DialogPalette is { } palette)
            using (ArcanumGuiTheme.WithPalette(palette)) RecomposeCore();
        else RecomposeCore();
    }

    private void RecomposeCore()
    {
        SingleComposer?.Dispose();
        Composers.Remove("single");
        BuildComposer();
        SingleComposer?.Compose();
    }

    /// <inheritdoc />
    public override void OnRenderGUI(float deltaTime)
    {
        if (DialogPalette is { } palette)
            using (ArcanumGuiTheme.WithPalette(palette)) { base.OnRenderGUI(deltaTime); }
        else base.OnRenderGUI(deltaTime);
    }

    /// <summary>
    /// Build the dialog composer. Implementers should assign <see cref="GuiDialog.SingleComposer" />.
    /// </summary>
    protected abstract void BuildComposer();

    /// <summary>
    /// Standard Arcanum dialog background and title bar helper.
    /// </summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="title">The title value.</param>
    /// <param name="onClose">The on close value.</param>
    /// <param name="bgBounds">When this method returns, contains the <paramref name="bgBounds" /> value.</param>
    /// <returns>The begin dialog.</returns>
    protected GuiComposer BeginDialog(GuiComposer composer, string title, Action onClose, out ElementBounds bgBounds)
    {
        var dialogBounds = ArcanumGuiTheme.ArcanumConfigDialogBounds();
        bgBounds = ArcanumGuiTheme.ArcanumConfigBackgroundBounds();

        return composer
            .AddDialogTitleBar(title, onClose)
            .BeginChildElements(bgBounds);
    }

    /// <summary>Performs the end dialog operation.</summary>
    /// <param name="composer">The composer value.</param>
    /// <returns>The end dialog.</returns>
    protected GuiComposer EndDialog(GuiComposer composer)
    {
        return composer.EndChildElements();
    }
}
