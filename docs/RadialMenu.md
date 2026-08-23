---
layout: default
title: Radial Menu
nav_order: 15
parent: Arcanum GUI Toolkit
---

# Radial Menu

## What is it for?

`ArcanumLib.Gui.RadialMenu` provides a generic Cairo-styled radial (pie) menu. Sectors are arranged in a circle around a central cancel button. Each sector has an icon, label, description, hover state, and an optional action callback. Items can also contain nested sub-items to open a sub-menu on click.

The visual appearance is pluggable through the `IRadialMenuStyle` interface and a string-keyed registry, so consumers can register their own themes without modifying the library.

## When to use it

- You want a quick-access radial menu bound to a hotkey (hold-to-activate or click-to-toggle).
- You want a generic, reusable widget that does not depend on any specific mod's concepts.
- You want to provide multiple visual themes (e.g. per faction, per attunement, per context) without subclassing the dialog.

## Quick example

```csharp
using ArcanumLib.Gui.RadialMenu;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

var items = new List<RadialMenuItem>
{
    new RadialMenuItem
    {
        Icon = "heart",
        Label = Lang.Get("mymod:heal"),
        Description = Lang.Get("mymod:heal-desc"),
        Action = () => capi.SendChatMessage("/heal")
    },
    new RadialMenuItem
    {
        Icon = "gear",
        Label = Lang.Get("mymod:settings"),
        Description = Lang.Get("mymod:settings-desc"),
        Action = () => OpenSettingsDialog()
    }
};

var gui = new RadialMenuGui(capi, "Quick Menu", items, outerRadius: 200f, innerRadius: 48f);
gui.SetHoldKey(GlKeys.R);
gui.SetStyle("default");
gui.TryOpen();
```

## Custom styles

Register an `IRadialMenuStyle` implementation with `RadialMenuStyleRegistry` and select it by key via `SetStyle(string)`.

```csharp
public class MysticRadialMenuStyle : IRadialMenuStyle
{
    public string Key => "mystic";

    public void DrawSector(Context ctx, float cx, float cy, float a0, float a1,
        bool hovered, bool isActive, bool disabled,
        float outerRadius, float innerRadius)
    {
        // Draw wedge background, borders, rim accents...
    }

    public void DrawCenterButton(Context ctx, float cx, float cy, float innerRadius)
    {
        // Draw the center cancel button...
    }

    public (float r, float g, float b, float a) GetIconColor(bool disabled)
        => disabled ? (0.35f, 0.35f, 0.35f, 0.50f) : (0.80f, 0.82f, 0.90f, 1.0f);
}

// During mod startup:
RadialMenuStyleRegistry.Register(new MysticRadialMenuStyle());

// Later:
gui.SetStyle("mystic");
```

## API overview

### Namespaces

| Namespace | Purpose |
|-----------|---------|
| `ArcanumLib.Gui.RadialMenu` | `RadialMenuGui`, `RadialMenuItem`, `IRadialMenuStyle`, `RadialMenuStyleRegistry`, `DefaultRadialMenuStyle`. |

### RadialMenuGui

A `GuiDialog` subclass that renders the radial menu.

| Member | Purpose |
|--------|---------|
| `RadialMenuGui(capi, title, items, outerRadius, innerRadius)` | Constructor. `title` is unused for rendering but kept for compatibility. |
| `SetHoldKey(GlKeys key)` | Sets the hold-to-activate key. On key-up, the hovered item's action fires and the menu closes. |
| `SetStyle(string key)` | Selects a registered style by key. Falls back to `"default"` if the key is not found. |
| `Style` | Gets/sets the current `IRadialMenuStyle` directly. |
| `Items` | Protected list of `RadialMenuItem`. Subclasses can rebuild it in `OnGuiOpened`. |

### RadialMenuItem

| Property | Purpose |
|----------|---------|
| `Label` | Hover label text. |
| `Description` | Hover description text (wrapped, max 3 lines). |
| `Icon` | Built-in icon key: `heart`, `reset`, `sword`, `reload`, `clear`, `bug`, `clock`, `fire`, `star`, `feather`, `shuffle`, `arrowhook`, `grab`, `dice`, `food`, `explosion`, `dash`, `skip`, `info`, `gear`, `shield`, `scroll`, `user`, `music`, `eye`, `rune`. Unknown keys render a small circle. |
| `CustomIconDraw` | Optional `Action<Context, float, float, float>` that overrides the string-based icon switch. |
| `Action` | Callback fired on click or hold-key release. |
| `CloseAfterClick` | If `true` (default), the menu closes after the action fires. |
| `IsActive` | Draws the sector with an active/toggled highlight. |
| `Disabled` | Draws the sector greyed out (e.g. on cooldown). |
| `SubItems` | If set, clicking opens a nested radial menu with these items instead of firing `Action`. |

### IRadialMenuStyle

| Member | Purpose |
|--------|---------|
| `Key` | Unique string key for registry lookup. |
| `DrawSector(...)` | Draws a single sector wedge. Receives center, angles, state flags, and radii. |
| `DrawCenterButton(...)` | Draws the center cancel button inside the inner radius. |
| `GetIconColor(bool disabled)` | Returns the RGBA tint for sector icons. |

### RadialMenuStyleRegistry

| Method | Purpose |
|--------|---------|
| `Register(IRadialMenuStyle)` | Registers a style under its `Key`. Overwrites existing. |
| `Unregister(string key)` | Removes a style. The `"default"` style cannot be removed. |
| `GetOrDefault(string? key)` | Returns the style for the key, or the built-in `DefaultRadialMenuStyle` if not found. |
| `IsRegistered(string key)` | Checks whether a style with the given key is registered. |

### DefaultRadialMenuStyle

A warm brown/gold theme. Always available as the fallback when a requested style key is not registered. Can be subclassed to override individual draw methods.

## Notes

- The menu auto-clamps radii to fit the screen at the current GUI scale.
- Hover detection accounts for a slightly enlarged center hit area (+12 px) for easier cancel access.
- Action callbacks are wrapped in try/catch and logged via `capi.Logger.Warning` on failure — exceptions do not crash the menu.
- The `"default"` style is built into ArcanumLib and always available. Consumer mods register additional styles during startup.
