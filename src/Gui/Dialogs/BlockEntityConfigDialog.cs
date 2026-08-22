using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Theme;
using ArcanumLib.Persistence;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace ArcanumLib.Gui.Dialogs;

/// <summary>
/// Base class for a config dialog bound to a <see cref="BlockEntity"/> and a
/// <see cref="ModConfig{T}"/>. Subclasses implement <see cref="BuildBody"/> to
/// lay out config-specific controls and <see cref="ReadFields"/> to copy values
/// from the dialog back into the config before saving.
/// </summary>
/// <typeparam name="T">The config type.</typeparam>
public abstract class BlockEntityConfigDialog<T> : ArcanumGuiDialog where T : class, new()
{
    /// <summary>
    /// The block entity being configured.
    /// </summary>
    protected BlockEntity BlockEntity { get; }

    /// <summary>
    /// The config wrapper used for loading and saving.
    /// </summary>
    protected ModConfig<T> Config { get; }

    /// <summary>
    /// The currently edited config. Changes are applied on save.
    /// </summary>
    protected T Editing { get; private set; }

    private readonly string _titleKey;

    /// <summary>
    /// Creates a config dialog for the given block entity and config wrapper.
    /// </summary>
    /// <param name="capi">The client API.</param>
    /// <param name="blockEntity">The block entity being configured.</param>
    /// <param name="config">The config wrapper to load/save.</param>
    /// <param name="titleKey">Lang key or literal title for the dialog title bar.</param>
    protected BlockEntityConfigDialog(ICoreClientAPI capi, BlockEntity blockEntity, ModConfig<T> config, string titleKey)
        : base(capi)
    {
        BlockEntity = blockEntity ?? throw new ArgumentNullException(nameof(blockEntity));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        _titleKey = titleKey ?? throw new ArgumentNullException(nameof(titleKey));
        Editing = CloneConfig(config.Current);
    }

    /// <summary>
    /// Override to build the body of the dialog (between the title bar and the
    /// save/cancel buttons). The provided <paramref name="composer"/> is already
    /// inside the dialog's child elements.
    /// </summary>
    protected abstract GuiComposer BuildBody(GuiComposer composer);

    /// <summary>
    /// Override to read values from the dialog's input controls back into
    /// <see cref="Editing"/>. Called before saving.
    /// </summary>
    protected abstract void ReadFields();

    /// <summary>
    /// Called after the config is successfully saved. Override to apply the
    /// config to the block entity (e.g. mark watched attributes dirty).
    /// </summary>
    protected virtual void OnSaved() { }

    /// <summary>
    /// Returns the dialog title, resolving the lang key if applicable.
    /// </summary>
    protected virtual string GetTitle() => Lang.Get(_titleKey) ?? _titleKey;

    protected override void BuildComposer()
    {
        string title = GetTitle();
        ElementBounds bgBounds;

        var composer = capi.Gui.CreateCompo("blockentity-config-" + BlockEntity.Pos.X + "_" + BlockEntity.Pos.Y + "_" + BlockEntity.Pos.Z,
            ArcanumGuiTheme.ArcanumConfigDialogBounds());

        composer = BeginDialog(composer, title, OnCloseButton, out bgBounds);
        composer = BuildBody(composer);

        // Save / Cancel buttons at the bottom.
        int buttonY = (int)(bgBounds.fixedHeight - 30);
        composer = composer
            .AddButton(Lang.Get("cancel") ?? "Cancel", () => { OnCloseButton(); return true; },
                ElementBounds.Fixed(0, buttonY).WithFixedWidth(80))
            .AddButton(Lang.Get("save") ?? "Save", () => { OnSaveButton(); return true; },
                ElementBounds.Fixed((int)bgBounds.fixedWidth - 80, buttonY).WithFixedWidth(80));

        composer = EndDialog(composer);

        SingleComposer = composer;
    }

    /// <summary>
    /// Validates the edited config before saving. Override to add custom validation.
    /// Returns true by default.
    /// </summary>
    protected virtual bool Validate() => true;

    /// <summary>
    /// Called when the Save button is pressed. Reads fields, validates, saves,
    /// and closes the dialog.
    /// </summary>
    protected virtual void OnSaveButton()
    {
        ReadFields();

        if (!Validate())
        {
            capi.ShowChatMessage(Lang.Get("arcanumlib:config-validation-failed") ?? "Config validation failed.");
            return;
        }

        Config.Current = Editing;
        var result = Config.Save();
        if (result.IsSuccess)
        {
            OnSaved();
            TryClose();
        }
        else
        {
            capi.Logger?.Warning("[ArcanumLib] Failed to save block config at {0}: {1}",
                BlockEntity.Pos, result.Message);
            capi.ShowChatMessage(Lang.Get("arcanumlib:config-save-failed") ?? "Failed to save config.");
        }
    }

    /// <summary>
    /// Called when the Cancel button is pressed. Closes the dialog without saving.
    /// </summary>
    protected virtual void OnCloseButton()
    {
        TryClose();
    }

    /// <summary>
    /// Creates a deep clone of the config for editing. Override if T is not
    /// trivially copyable. The default implementation uses JSON round-trip.
    /// </summary>
    protected virtual T CloneConfig(T source)
    {
        if (source == null) return new T();
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    public override string ToggleKeyCombinationCode => "blockentityconfigdialog-" + BlockEntity.Pos;
}
