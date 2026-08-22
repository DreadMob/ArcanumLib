using System;
using ArcanumLib.Gui.Theme;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Dialogs;

/// <summary>
/// Base class for Arcanum-themed <see cref="GuiDialog"/>s.
/// Handles recomposition on the main thread and provides common bounds helpers.
/// </summary>
public abstract class ArcanumGuiDialog : GuiDialog
{
    private bool _recomposeQueued;

    public override bool UnregisterOnClose => true;

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
    /// Disposes the existing composer and calls <see cref="BuildComposer"/>.
    /// </summary>
    protected void Recompose()
    {
        SingleComposer?.Dispose();
        Composers.Remove("single");
        BuildComposer();
        SingleComposer?.Compose();
    }

    /// <summary>
    /// Build the dialog composer. Implementers should assign <see cref="GuiDialog.SingleComposer"/>.
    /// </summary>
    protected abstract void BuildComposer();

    /// <summary>
    /// Standard Arcanum dialog background and title bar helper.
    /// </summary>
    protected GuiComposer BeginDialog(GuiComposer composer, string title, Action onClose, out ElementBounds bgBounds)
    {
        var dialogBounds = ArcanumGuiTheme.ArcanumConfigDialogBounds();
        bgBounds = ArcanumGuiTheme.ArcanumConfigBackgroundBounds();

        return composer
            .AddDialogTitleBar(title, onClose)
            .BeginChildElements(bgBounds);
    }

    protected GuiComposer EndDialog(GuiComposer composer)
    {
        return composer.EndChildElements();
    }
}
