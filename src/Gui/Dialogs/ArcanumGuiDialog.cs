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

    /// <summary>Gets a value indicating whether the unregister on close is enabled.</summary>
    public override bool UnregisterOnClose => true;

    /// <summary>Performs the arcanum gui dialog operation.</summary>
    /// <param name="capi">The client API instance.</param>
    protected ArcanumGuiDialog(ICoreClientAPI capi) : base(capi) { }

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
        SingleComposer?.Dispose();
        Composers.Remove("single");
        BuildComposer();
        SingleComposer?.Compose();
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
