---
layout: default
title: GUI Controls
parent: "Arcanum GUI Toolkit"
nav_order: 11
---

# GUI Controls

Themed input controls, ornaments and item slots in `ArcanumLib.Gui.Controls`, plus the theme helpers they share. Every control reads `ArcanumGuiTheme.Palette` while it composes **and** while it renders, so it follows the palette of the dialog it sits in.

## Inputs

| Add | Get by key | Notes |
|-----|-----------|-------|
| `AddArcanumCheckbox(label, initial, onToggle, bounds, key)` | `GetArcanumCheckbox` | `IsChecked`, `SetChecked`, `Enabled`. |
| `AddToggle(bounds, initial, onToggle, key)` | `GetElement(key) as ArcanumToggle` | Lever-style switch. |
| `AddArcanumSpinner(initial, min, max, step, onChange, bounds, key)` | `GetArcanumSpinner` | `GetValue`, `SetValue`, `SetRange`. |
| `AddArcanumSlider(onNewValue, bounds, key, unitName)` | `GetArcanumSlider` | `SetValues(value, min, max, step, unitSuffix)`; `ReadoutWidth` puts the value beside the track instead of above its right end. |
| `AddArcanumDropdown(values, names, selectedIndex, onSelected, bounds, font, key, palette)` | `GetArcanumDropdown` | `SelectedValue`, `SelectedIndex`, `SetSelectedIndex`, `SetSelectedValue`. The list opens as its own dialog in the right spot at any GUI scale. |
| `AddArcanumTabStrip(names, selected, onTab, bounds, key)` | `GetArcanumTabStrip` | Tab labels share one baseline. |
| `AddArcanumTextInput(bounds, onChanged, font, key, maxLength)`, `AddArcanumTextArea(...)` | `GetArcanumTextInput` | `GetText`, `SetValue(text, caretToEnd)`. While focused it keeps every key, so typing does not walk the player. |
| `AddArcanumProgressBar(bounds, initial, key)` | `GetArcanumProgressBar` | `SetValue(0…1)`, `ShowLabel`. |
| `AddTooltip(targetBounds or element, text, key, font)` | `GetTooltip` | Hover tooltip; `ArcanumTooltip.Set(composer, key, text)` updates it. |

```csharp
SingleComposer = capi.Gui.CreateCompo("mymod-settings", dialogBounds)
    .AddArcanumDialogBackground(bgBounds)
    .AddArcanumCheckbox("Show particles", cfg.Particles, v => cfg.Particles = v, ElementBounds.Fixed(20, 50, 200, 24), "particles")
    .AddArcanumSlider(v => { cfg.Volume = v; return true; }, ElementBounds.Fixed(20, 90, 240, 24), "volume", "%")
    .AddArcanumDropdown(codes, names, 0, (code, _) => cfg.Mode = code, ElementBounds.Fixed(20, 130, 200, 26), key: "mode")
    .Compose();

SingleComposer.GetArcanumSlider("volume")!.SetValues(cfg.Volume, 0, 100, 5, "%");
```

## Ornaments

`AddRivetFrame(bounds, inset, corners)`, `AddPlaque(bounds)`, `AddDialGauge(bounds, value, caption, key)` and `AddLiquidDialGauge(bounds, value, caption, liquidColor, key)` (`GuiElementDialGauge.SetValue(0…1)`) — the brass-and-rivets dressing used by the machine dialogs.

## Item slots

- `GuiElementSlotWell` — a recessed well drawn behind a slot.
- `GuiElementFittedSlot(capi, bounds, inv, slotId, sendPacket, ghost, iconFrac, quota)` — one slot of a block-entity inventory, the item fitted to the cell by its real model size; `ghost` draws the wanted item dimmed while the slot is empty, `quota` shows "have/quota".
- `GuiItemFit.Render(capi, slot, box, frac, color, dt, shading, spin, showStackSize)` — draws any stack fitted into a box. Vanilla `RenderItemstackToGui` only scales by the guiTransform, so a multi-block model spills out of its slot and a flat plate looks tiny; this projects the model's vertices through the GUI transform and fills the box, clipped. With `spin` the outline covers every turn angle so it never outgrows the box mid-turn.
- `GuiOccluders.Add(bounds)` / `Remove(bounds)` — floating layers (dropdown lists) that 3D item icons must not draw through; `GuiItemFit` skips icons under them.

## Dialog bases

- `ArcanumGuiDialog` — recompose on the main thread (`Recompose()` is safe from any thread); override `DialogPalette` to install a palette only for the duration of each compose and render call, so overlapping dialogs never leak their theme into each other.
- `BrassDialog` / `BrassTheme` — the brass-and-walnut skin (dark iron, walnut, brass rims and rivets, parchment text) shared by the steam-age mods; `BrassTheme.DrawRecessedWell` for sunk housings.

## Theme helpers

- `ArcanumGuiTheme.WithPalette(palette)` → `PaletteScope`: replaces the palette until disposed (LIFO). On dialogs prefer overriding `DialogPalette`.
- `GlyphText.Show(ctx, text)` / `GlyphText.Extents(ctx, text)` — drop-in for `ShowText` / `TextExtents` that draws symbols the game fonts lack (★ ⚙ ● ○ ∞ ✓ ✕ − ◀ ▶ ▲ ▼) as Cairo shapes in the current colour. `ArcanumList`, `ArcanumDropdown` and `ArcanumButton` labels already use it.
- `ArcanumTexturePool.GetOrCreate(capi, key, bake)` — one baked `LoadedTexture` per unique key, shared by every control that draws the same surface (each toggle plate, each list-row frame). Keys must describe the content fully; never dispose a pooled texture, use `Invalidate(key)`.
