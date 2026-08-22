---
layout: default
title: BlockEntityConfigDialog
---

# BlockEntityConfigDialog

Generic base dialog for editing block entity configuration.

## What is it for?

`BlockEntityConfigDialog<T>` is a base class for GUI dialogs that edit a typed configuration object associated with a block entity. It handles the save/cancel lifecycle, deep-clones the config for editing, and integrates with `ModConfig<T>` for persistence.

## When to use it

- You have a block entity with a config that should be editable in-game.
- You want Save/Cancel buttons wired up without rewriting the boilerplate.
- You want the editing copy to be discarded on cancel and applied on save.

## Quick example

```csharp
using ArcanumLib.Gui.Dialogs;
using ArcanumLib.Persistence;

public class MyBlockConfigDialog : BlockEntityConfigDialog<MyConfig>
{
    public MyBlockConfigDialog(ICoreClientAPI capi, ModConfig<MyConfig> config)
        : base(capi, config, "My Block Config") { }

    protected override void BuildBody(GuiComposer composer)
    {
        // Add your input fields here, reading from Editing.
        composer.AddTextInput(ElementBounds.Fixed(0, 0, 200, 30),
            val => Editing.DebugMode = bool.Parse(val));
    }

    protected override void ReadFields()
    {
        // Read values from GUI controls into Editing if needed.
    }
}
```

## API overview

| Member | Description |
|--------|-------------|
| `Editing` | The working copy of the config being edited. |
| `BuildBody(GuiComposer)` | Override to add dialog-specific input fields. |
| `ReadFields()` | Override to read values from controls into `Editing`. |
| `Validate()` | Override to return false when the edited config is invalid. |
| `OnSaved()` | Override for post-save side effects (e.g. mark attributes dirty). |
| `CloneConfig(T)` | Deep-clones the config via JSON round-trip. |

## Notes

- The dialog clones `Config.Current` into `Editing` on open. Cancel discards `Editing`.
- Save calls `Validate()`, then `Config.Save()`, then `OnSaved()`.
- The dialog is client-side only; persistence is handled by `ModConfig<T>`.
