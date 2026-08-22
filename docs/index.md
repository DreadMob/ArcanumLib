---
layout: default
title: Home
nav_order: 1
description: ArcanumLib documentation homepage
permalink: /
---

# ArcanumLib

A shared client/server utility library for [Vintage Story](https://www.vintagestory.at/) mods. It provides reusable GUI rendering, color handling, asset loading, scheduling, persistence, status effects, item charge/modes, and more.

---

## Browse by category

### GUI & Rendering
- [Arcanum GUI Toolkit]({% link ArcanumGui.md %})
- [ImageIconCache]({% link ImageIconCache.md %})
- [ModeIconBuilder]({% link ModeIconBuilder.md %})
- [RGBA]({% link RGBA.md %})
- [ShapeCloner]({% link ShapeCloner.md %})

### Items & Equipment
- [ItemCharge]({% link ItemCharge.md %})
- [ItemMode]({% link ItemMode.md %})
- [Inventory / ItemStack helpers]({% link InventoryHelpers.md %})

### Persistence & Progression
- [ModDataStore]({% link ModDataStore.md %})
- [PityTracker]({% link PityTracker.md %})
- [Status Effects]({% link StatusEffects.md %})

### Assets & Data
- [ModAssetLoader]({% link ModAssetLoader.md %})
- [ModAssetRegistry]({% link ModAssetRegistry.md %})
- [TagSetExtensions]({% link TagSetExtensions.md %})
- [ValidationResult]({% link ValidationResult.md %})

### Performance & Scheduling
- [DeferredWork]({% link DeferredWork.md %})
- [TimedCache]({% link TimedCache.md %})
- [CleanupScope]({% link CleanupScope.md %})

### Common & Utility
- [ApiExtensions]({% link ApiExtensions.md %})
- [CooldownTracker]({% link CooldownTracker.md %})
- [EntityHealthExtensions]({% link EntityHealthExtensions.md %})
- [EventScope]({% link EventScope.md %})
- [LoggerExtensions]({% link LoggerExtensions.md %})
- [PlayerExtensions]({% link PlayerExtensions.md %})
- [WatchedAttributes]({% link WatchedAttributes.md %})

### Randomization & Geometry
- [WeightedRandom]({% link WeightedRandom.md %})
- [ShapeCloner]({% link ShapeCloner.md %})

### Networking
- [TypedNetworkChannel]({% link TypedNetworkChannel.md %})

---

## Quick start

Add `ArcanumLib.csproj` as a project reference, set `VINTAGE_STORY` environment variable, and add `arcanumlib` to your `modinfo.json` dependencies:

```json
{
  "type": "mod",
  "modid": "mymod",
  "name": "MyMod",
  "dependson": [ { "modid": "arcanumlib" } ]
}
```

Example — read charge from an item:

```csharp
using ArcanumLib.Items;

float charge = ItemCharge.GetChargeValue(stack);
```

Example — persistent data:

```csharp
using ArcanumLib.Persistence;

var store = ModDataStore.GetOrCreate<MySaveData>(sapi, "mymod", "state", 1);
store.Data.Counter++;
store.Save();
```
