---
layout: default
title: ItemMode
parent: "ItemCharge"
nav_order: 1
---

# ItemMode

Generic item mode data and F-key tool-mode integration.

## What is it for?

Use `ItemMode` when an item has multiple selectable configurations:

- A staff with fire, ice, and lightning modes.
- A tool that changes action set depending on the active mode.
- Effects that should only run in a specific mode.

It handles parsing mode JSON, reading/writing the active index, clamping out-of-range values, and building the vanilla `SkillItem[]` UI.

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

Modes are stored as a JSON array on `ItemStack.Attributes`:

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

| Property | What it controls |
|----------|-----------------|
| `ModesAttributeKey` | Where the mode JSON list is stored. |
| `ModeIndexAttributeKey` | Where the active mode index is stored. |
| `NameResolver` | Optional translation/localization of `Name`. |
| `ModesPerLine` | How many icons fit before a line break in the tool-mode UI. |

## Quick example

```csharp
using ArcanumLib.Items;

if (ItemModeManager.TryGetModes(stack.Attributes, out var modes, config))
{
    var active = ItemModeManager.GetActiveMode(stack.Attributes, modes, config);
    // active.Id, active.Actions
}
```

## Usage

### Get the active mode

```csharp
if (ItemModeManager.TryGetActiveModeId(stack.Attributes, out var activeId, config))
{
    // activeId is the current mode code
}
```

### Get the active mode's actions

```csharp
if (ItemModeManager.TryGetActiveModeActions(stack.Attributes, out var actions, config))
{
    foreach (var action in actions)
    {
        // execute action.Id with action.Args
    }
}
```

### Switch or cycle mode

```csharp
ItemModeManager.SetActiveModeIndex(stack.Attributes, 1, config);

// cycle forward by one
string? nextId = ItemModeManager.CycleActiveMode(stack.Attributes, 1, config);
```

### Gate an effect by active mode

```csharp
bool shouldRun = ItemModeManager.ShouldRunForMode(effectMode, activeModeId);
```

An empty `effectMode` means "runs in every mode".

### Tool mode UI

```csharp
SkillItem[]? skills = ItemModeManager.GetToolModeSkillItems(capi, modes, config);
```

Each `SkillItem` uses the mode `Name` and `Icon`, falling back to a letter icon if the icon is empty.