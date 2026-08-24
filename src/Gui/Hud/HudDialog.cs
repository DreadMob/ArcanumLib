using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic client-side HUD overlay that holds a <see cref="HudPanel{TSnapshot, THudDefinition, TTheme}" /> and manages
/// open/close, theme resolution, and panel (re)creation.
/// </summary>
/// <typeparam name="TSnapshot">The type of the tsnapshot value.</typeparam>
/// <typeparam name="THudDefinition">The type of the thuddefinition value.</typeparam>
/// <typeparam name="TTheme">The type of the ttheme value.</typeparam>
/// <typeparam name="TPanel">The type of the tpanel value.</typeparam>
public abstract class HudDialog<TSnapshot, THudDefinition, TTheme, TPanel> : HudElement
    where TSnapshot : class, IHudSnapshot
    where THudDefinition : class
    where TTheme : HudTheme
    where TPanel : HudPanel<TSnapshot, THudDefinition, TTheme>
{
    /// <summary>Override in the derived class to hide the dialog by default.</summary>
    public override string ToggleKeyCombinationCode => null!;

    /// <summary>The dialog should not block mouse events for the rest of the UI.</summary>
    /// <returns>true if the operation should receive mouse events; otherwise, false.</returns>
    public override bool ShouldReceiveMouseEvents() => false;

    /// <summary>Current snapshot to display.</summary>
    protected TSnapshot? _currentSnapshot;

    /// <summary>Definition that drives layout and element data.</summary>
    protected THudDefinition _definition = null!;

    /// <summary>Currently rendered panel.</summary>
    protected TPanel? _panel;

    /// <summary>Last measured panel width, used to recreate the composer on size changes.</summary>
    protected int _lastPanelWidth;

    /// <summary>Last measured panel height, used to recreate the composer on size changes.</summary>
    protected int _lastPanelHeight;

    /// <summary>Dialog compositor name, used to create a <c>GuiComposer</c>.</summary>
    protected readonly string _dialogName;

    /// <summary>
    /// Creates the dialog. The <paramref name="dialogName" /> must be unique per mod.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="dialogName">The dialog name value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dialogName" /> is <see langword="null" />.</exception>
    protected HudDialog(ICoreClientAPI capi, string dialogName) : base(capi)
    {
        _dialogName = dialogName ?? throw new ArgumentNullException(nameof(dialogName));
    }

    /// <summary>Called when the definition for this HUD is available or changes.</summary>
    /// <param name="definition">The definition value.</param>
    public virtual void SetDefinition(THudDefinition definition)
    {
        _definition = definition;
        if (_panel != null && _currentSnapshot != null)
            _panel.Update(_currentSnapshot, _definition, ResolveTheme());
    }

    /// <summary>Called when a new snapshot is received from the server.</summary>
    /// <param name="snapshot">The snapshot value.</param>
    public virtual void OnSnapshotReceived(TSnapshot snapshot)
    {
        if (snapshot == null) return;

        if (snapshot.IsRemoved() || !ShouldShow(snapshot))
        {
            _currentSnapshot = null;
            TryClose();
            return;
        }

        _currentSnapshot = snapshot;

        if (!IsOpened())
            TryOpen();

        EnsureComposer();
    }

    /// <summary>Resolves the active theme for the current definition.</summary>
    /// <returns>The resolve theme.</returns>
    protected abstract TTheme ResolveTheme();

    /// <summary>Returns true when the HUD should be visible for the given snapshot.</summary>
    /// <param name="snapshot">The snapshot value.</param>
    /// <returns>true if the operation should show; otherwise, false.</returns>
    protected abstract bool ShouldShow(TSnapshot snapshot);

    /// <summary>Measures the desired panel size for the current state.</summary>
    /// <param name="snapshot">The snapshot value.</param>
    /// <param name="definition">The definition value.</param>
    /// <param name="theme">The theme value.</param>
    /// <returns>The measure.</returns>
    protected abstract (int width, int height) Measure(TSnapshot snapshot, THudDefinition definition, TTheme theme);

    /// <summary>Creates a new panel instance with the given bounds.</summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value.</param>
    /// <returns>The panel.</returns>
    protected abstract TPanel CreatePanel(ICoreClientAPI capi, ElementBounds bounds);

    /// <summary>Builds or reuses the GUI composer and refreshes the panel.</summary>
    protected virtual void EnsureComposer()
    {
        if (_currentSnapshot == null) return;

        var theme = ResolveTheme();
        var (width, height) = Measure(_currentSnapshot, _definition, theme);

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedAlignmentOffset(10, 10);

        var bgBounds = ElementBounds.Fixed(0, 0, width, height);

        if (SingleComposer == null || !SingleComposer.Composed || _lastPanelWidth != width || _lastPanelHeight != height)
        {
            _lastPanelWidth = width;
            _lastPanelHeight = height;
            ClearComposers();
            _panel = CreatePanel(capi, ElementBounds.Fixed(0, 0, width, height));
            SingleComposer = capi.Gui.CreateCompo(_dialogName, dialogBounds)
                .BeginChildElements(bgBounds)
                .AddInteractiveElement(_panel, "hudPanel")
                .EndChildElements()
                .Compose();
        }

        if (_panel == null) return;
        _panel.Update(_currentSnapshot, _definition, theme);
    }

    /// <summary>Disposes the panel and base resources.</summary>
    public override void Dispose()
    {
        _currentSnapshot = null;
        _panel?.Dispose();
        _panel = null;
        base.Dispose();
    }
}
