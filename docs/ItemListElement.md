---
layout: default
title: ItemListElement
nav_order: 12
parent: Arcanum GUI Toolkit
---

# ItemListElement

## What is it for?

`ItemListElement` is a reusable vertical list `GuiElement` with icon nodes on the left and text on the right. Each row displays a circular icon (item stack, custom Cairo glyph, or status-coloured circle), a title, and an optional subtitle. Rows can have a status (`Locked`, `Available`, `Owned`) that controls colours, optional tooltip text on hover, and an optional highlight border.

## When to use it

- You need a scrollable list of items with icons and status indicators.
- You want hover tooltips per row without the performance cost of recomposing tooltips on every mouse move.
- You need custom Cairo-drawn icons for some rows (via `CustomIconRegistry`).
- You want status-coloured rows (locked / available / owned) with consistent theming.

## Quick example

```csharp
using ArcanumLib.Gui.Controls;

var rows = new List<ItemListRow>
{
    new ItemListRow
    {
        Id = "item-1",
        Title = "Sword of Flames",
        Subtitle = "Epic · 2h",
        IconItemCode = "mydomain:sword-flames",
        Status = ItemRowStatus.Available
    },
    new ItemListRow
    {
        Id = "item-2",
        Title = "Locked Chest",
        Subtitle = "Requires key",
        CustomIconKey = "mydomain:lock-icon",
        Status = ItemRowStatus.Locked,
        TooltipText = "Find the golden key to unlock."
    }
};

var listEl = new ItemListElement(capi, listBounds, rows, OnRowClicked);
composer.AddInteractiveElement(listEl, "itemlist");
```

```csharp
private void OnRowClicked(string rowId)
{
    api.Logger.Notification("Clicked: {0}", rowId);
}
```

## API overview

### ItemListRow

Data model for a single row.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string?` | Unique identifier passed to the click callback. |
| `IconItemCode` | `string?` | Item/block code to render as the icon. Resolved via `api.World.GetItem` / `GetBlock`. |
| `IconUcontents` | `string?` | Optional `ucontents` tree attribute for the icon stack. |
| `CustomIconKey` | `string?` | Key into `CustomIconRegistry` for a Cairo-drawn icon. Takes priority over `IconItemCode`. |
| `Title` | `string?` | Main row label. |
| `Subtitle` | `string?` | Secondary line, smaller and muted. |
| `Status` | `ItemRowStatus` | `Locked`, `Available`, or `Owned`. Controls colours. |
| `TooltipText` | `string?` | Rich-text tooltip shown on hover. |
| `BorderColor` | `string?` | Hex colour for an optional highlight border. |
| `BorderThickness` | `double` | Thickness of the highlight border. Default 2. |

### ItemListElement

```csharp
public ItemListElement(ICoreClientAPI capi, ElementBounds bounds, List<ItemListRow> rows, Action<string>? onRowClicked)
```

| Method / Property | Description |
|-------------------|-------------|
| `SetData(List<ItemListRow> rows)` | Replace all rows and regenerate textures. |
| `SetScroll(double value)` | Set vertical scroll offset in logical pixels. |

### Icon stack fallback

If an `IconItemCode` is not found as a regular item or block, the element calls the static `IconStackFallbackResolver` delegate. Consumers with custom item systems should set this at startup:

```csharp
ItemListElement.IconStackFallbackResolver = (capi, itemCode) =>
{
    // Return an ItemStack for custom item codes, or null.
    return TryResolveCustomItem(capi, itemCode);
};
```

## Notes

- Tooltips are cached per distinct text to avoid expensive richtext recompose on every hover change.
- Hover detection is debounced (80 ms) to prevent flicker when sweeping the cursor.
- World bounds are cached after compose for performance; call `SetData` to invalidate.
- Only `Available` rows fire the click callback.
