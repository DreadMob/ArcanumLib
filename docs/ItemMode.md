---
layout: default
title: ItemMode
---

# ItemMode

`ArcanumLib.Items.ItemMode` and `ItemModeManager` provide generic item mode data and helpers for switching, querying, and gating effects/actions by active mode.

## When to use it

Use `ItemMode` when your mod has items with:

- Multiple selectable modes (e.g. bound to the F-key tool-mode cycle).
- Mode-specific actions.
- Effects that should only run in a specific mode.

## Data model

```csharp
public class ItemMode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public List<ItemAction> Actions { get; set; }
}

public class ItemAction
{
    public string Id { get; set; }
    public string[] Args { get; set; }
}
```

Modes are normally stored as a JSON array on `ItemStack.Attributes`.

```json
[
  { "id": "retribution", "name": "Retribution", "icon": "", "actions": [{ "id": "setmode", "args": ["retribution"] }] },
  { "id": "scarab", "name": "Scarab", "icon": "", "actions": [{ "id": "setmode", "args": ["scarab"] }] }
]
```

## Configuration

```csharp
var config = new ItemModeConfig
{
    ModesAttributeKey = "mymod:modes",
    ModeIndexAttributeKey = "mymod:mode",
    NameResolver = name => Lang.Get(name) ?? name
};
```

## Usage

```csharp
// Parse modes from the stack
if (ItemModeManager.TryGetModes(stack.Attributes, out var modes, config))
{
    // Get current active mode
    var active = ItemModeManager.GetActiveMode(stack.Attributes, modes, config);

    // Get the active mode's id
    if (ItemModeManager.TryGetActiveModeId(stack.Attributes, out var activeId, config))
    {
        // gate logic/effects
    }

    // Get the active mode's actions
    if (ItemModeManager.TryGetActiveModeActions(stack.Attributes, out var actions, config))
    {
        // execute actions
    }
}

// Switch mode
ItemModeManager.SetActiveModeIndex(stack.Attributes, 1, config);

// Cycle forward/backward
string? nextMode = ItemModeManager.CycleActiveMode(stack.Attributes, 1, config);

// Check if an effect should run in the active mode
bool shouldRun = ItemModeManager.ShouldRunForMode(effectMode, activeMode);
```

## Tool mode UI

`ItemModeManager.GetToolModeSkillItems` returns a `SkillItem[]` for the vanilla F-key tool mode selector.

```csharp
SkillItem[]? skillItems = ItemModeManager.GetToolModeSkillItems(capi, modes, config);
```

Each `SkillItem` is created from the mode `Name` and `Icon` (or a letter icon fallback). You can call `GetToolModeIndex` and `SetActiveModeIndex` in your `CollectibleObject.GetToolMode` / `SetToolMode` Harmony patches.

## Effect gating

Effects can be gated by mode by setting their `mode` field. An empty or whitespace `mode` means "runs in every mode". Comparison is case-insensitive.

```csharp
bool shouldRun = ItemModeManager.ShouldRunForMode(effect.Mode, activeModeId);
```
