---
layout: default
title: Arcanum GUI Toolkit
nav_order: 10
has_children: true
---

# Arcanum GUI Toolkit

## What is it for?

`ArcanumLib.Gui` is a small toolkit for building Vintage Story GUIs with less boilerplate and a consistent look. It provides a shared colour palette, ready-to-use controls, layout helpers, and a fluent dialog builder.

## When to use it

- You want a consistent, themeable look for mod dialogs without hand-tweaking every colour and bound.
- You need vertical or horizontal layout helpers to avoid repeated `ElementBounds.Fixed` calculations.
- You want reusable, pre-styled controls such as icons, cards, buttons, scrollbars, and lists.
- You are implementing a dialog and want a fluent `ArcanumComposer` builder.
- You need a self-contained scrollable list with selection, hover, and zebra-row rendering.

## Quick example

```csharp
using ArcanumLib.Gui.Controls;
using ArcanumLib.Gui.Layout;
using ArcanumLib.Gui.Theme;

SingleComposer = ArcanumComposer
    .Create(capi, "my-dialog")
    .WithTitleBar(Lang.Get("mydomain:my-dialog-title"), TryClose)
    .BeginVertical(padding: 20, gap: 12)
        .AddText("Dialog header", ArcanumFont.Title)
        .AddCard(card =>
        {
            card.BeginHorizontal(gap: 12)
                .AddIcon("mydomain:textures/icons/star.webp", 48,
                    color: ArcanumGuiTheme.TextPrimary,
                    fit: IconFit.Circle)
                .BeginVertical(gap: 4)
                    .AddText("Primary label", ArcanumFont.Body)
                    .AddText("Secondary caption", ArcanumFont.Caption)
                .EndVertical()
            .EndHorizontal();
        })
        .AddList(
            _data.Items,
            item => item.DisplayName,
            rowHeight: 40,
            onSelected: OnItemSelected,
            height: 240)
        .AddButtonRow("Cancel", OnCancel, "Confirm", OnConfirm)
    .EndVertical()
    .Compose();
```

## API overview

The toolkit is organised into the following namespaces. The snippets below show common tasks.

### Namespaces

| Namespace | Purpose |
|-----------|---------|
| `ArcanumLib.Gui.Theme` | The `ArcanumGuiTheme` colour palette, `RGBA`, `ArcanumFont`, and Cairo drawing helpers. |
| `ArcanumLib.Gui.Icons` | `ImageIconCache` for loading and drawing PNG/JPEG/GIF/BMP/ICO/WebP/HEIF icons and other `SKCodec` formats. Also contains `CustomIconRegistry` and `ICustomIconRenderer` for custom Cairo-drawn icons, and `CustomTabIconRenderer` for decorative tab icons. |
| `ArcanumLib.Gui.Controls` | Ready-to-use elements: `ArcanumIcon`, `ArcanumCard`, `ArcanumButton`, `ArcanumScrollbar`, `ArcanumDialogBackground`, `ArcanumList<T>`, `ItemListElement`, `GuiElementCustomTabContent`, `GuiDateTimePicker`. |
| `ArcanumLib.Gui.Layout` | `ArcanumLayout` helpers for vertical/horizontal stacks and the `ArcanumComposer` fluent builder. |
| `ArcanumLib.Gui.Dialogs` | `ArcanumGuiDialog` base class. |
| `ArcanumLib.Gui.RadialMenu` | `RadialMenuGui`, `RadialMenuItem`, `IRadialMenuStyle`, and `RadialMenuStyleRegistry`. See the [Radial Menu](RadialMenu.md) page for details. |
| `ArcanumLib.Gui` | Extension methods for `GuiComposer` such as `AddItemSlot`. |

### Theme

```csharp
using ArcanumLib.Gui.Theme;

var fill = ArcanumGuiTheme.SurfaceCard;
var border = ArcanumGuiTheme.BorderDefault;
var textColor = ArcanumGuiTheme.TextPrimary;
```

### Fonts

```csharp
var title = ArcanumFont.Title;
var body = ArcanumFont.Body;
var caption = ArcanumFont.Caption;
```

### Layout

Instead of manually computing `ElementBounds.Fixed` coordinates:

```csharp
var rows = ArcanumLayout.VerticalFill(bgBounds, gap: 12, padding: 20,
    headerH: 64,
    bodyH: 120,
    footerH: 36);

var headerBounds = rows[0];
var bodyBounds = rows[1];
var footerBounds = rows[2];
```

### Icon

```csharp
var iconBounds = ElementBounds.Fixed(0, 0, 48, 48);
composer.AddArcanumIcon(
    "mydomain:textures/icons/gear.webp",
    iconBounds,
    color: ArcanumGuiTheme.TextPrimary,
    fit: IconFit.Circle);
```

### Card

```csharp
composer.AddArcanumCard(
    bodyBounds,
    fill: ArcanumGuiTheme.SurfaceCard.WithAlpha(0.45),
    border: ArcanumGuiTheme.BorderShadow.WithAlpha(0.55),
    accent: ArcanumGuiTheme.StatusActive);
```

### Dialog base

```csharp
public class MyDialog : ArcanumGuiDialog
{
    protected override void BuildComposer()
    {
        var bgBounds = ArcanumGuiTheme.ArcanumConfigBackgroundBounds();
        var bounds = ArcanumGuiTheme.ArcanumConfigDialogBounds();

        SingleComposer = capi.Gui.CreateCompo("MyDialog", bounds)
            .AddArcanumDialogBackground(bgBounds)
            .AddDialogTitleBar("My Title", TryClose)
            .BeginChildElements(bgBounds)
            .AddArcanumCard(bodyBounds)
            .EndChildElements();
    }
}
```

### ArcanumComposer

`ArcanumComposer` is a thin, fluent wrapper around `GuiComposer`. It keeps a stack of vertical and horizontal containers and a current cursor, so common dialogs can be built without manually stacking `ElementBounds.Fixed` calls. See the Quick example for a complete dialog.

| Method | Purpose |
|--------|---------|
| `BeginVertical` / `EndVertical` | Stack children top-to-bottom. Auto-sizes height unless a height is given. |
| `BeginHorizontal` / `EndHorizontal` | Stack children left-to-right. Fits width when not explicitly set. |
| `AddText` | Single- or multi-line static text with automatic height. |
| `AddButton` / `AddButtonRow` | Themed Arcanum buttons. |
| `AddIcon` | Draw a WebP/PNG icon with optional clip/tint. |
| `AddCard` | Rounded themed card background with nested child content. |
| `AddList<T>` | Add an `ArcanumList<T>` with selection and scrolling. |
| `AddTextInput` / `AddNumberInput` / `AddDropdown` | Standard Vintage Story inputs. |
| `AddScrollbar` | A standalone `ArcanumScrollbar`. |

`ArcanumComposer.Compose()` finalises the stack and returns the underlying `GuiComposer`.

### ArcanumList

`ArcanumList<T>` is a self-contained scrollable list of selectable text rows.

```csharp
var listBounds = ElementBounds.Fixed(0, 0, 400, 240);

SingleComposer = capi.Gui.CreateCompo("my-list-dialog", dialogBounds)
    .AddArcanumDialogBackground(bgBounds)
    .AddArcanumList(
        myItems,
        listBounds,
        item => item.Name,
        rowHeight: 36,
        onSelected: (item, index) => api.Logger.Notification($"Selected {item.Name} at {index}"),
        font: ArcanumFont.Body,
        key: "myList")
    .Compose();
```

The list is added as an interactive element. You can retrieve it later through the composer:

```csharp
var list = SingleComposer.GetArcanumList<MyItem>("myList");
list.SetItems(newItems);
list.ScrollTo(120f);
list.Select(3);
```

- Built-in selection, hover, and zebra-row rendering.
- Mouse wheel and draggable scrollbar.
- `SetItems`, `ScrollTo`, and `Select` methods for dynamic content.
- Optional selection callback with `(T item, int index)`.

## Notes

- `ArcanumComposer` is a convenience wrapper, not a full layout engine. Complex absolute-positioned or overlapping dialogs may still need direct `GuiComposer` calls.
- `ArcanumList<T>` draws simple text rows. For rich per-row layouts (icons, multiple columns, etc.), build a custom `GuiElement` or use `ArcanumComposer` inside a scrollable `GuiElementContainer`.
- `ArcanumList<T>` does not support multi-select in this first version.