---
layout: default
title: Home
nav_order: 1
description: ArcanumLib documentation homepage
permalink: /
---

# ArcanumLib

A shared client/server utility library for [Vintage Story](https://www.vintagestory.at/) mods.

## Why use it?

Writing a Vintage Story mod usually means solving the same problems again and again: loading JSON assets, drawing icons, scheduling work, tracking cooldowns, saving per-world data, applying status effects, or handling item charges and modes. `ArcanumLib` extracts those patterns into reusable, mod-agnostic helpers so you can focus on gameplay instead of boilerplate.

## What is inside?

The library is split into clear areas. Each page answers three questions: **what is it for**, **when to use it**, and **how to use it** with a minimal code example.

---

## GUI & Rendering

Everything you need for custom in-game UI without fighting the low-level API.

| Page | Purpose |
|------|---------|
| [Arcanum GUI Toolkit]({% link ArcanumGui.md %}) | Themed colour palette, layout helpers, composer, and reusable controls. |
| [ImageIconCache]({% link ImageIconCache.md %}) | Load and draw `.webp`/`.png`/`.jpg` icons with clipping and tinting. |
| [ModeIconBuilder]({% link ModeIconBuilder.md %}) | Build `SkillItem` icons for tool modes, skill bars, and item stacks. |
| [RGBA]({% link RGBA.md %}) | Cairo-friendly color struct with parsing, conversion, and lerping. |
| [ShapeCloner]({% link ShapeCloner.md %}) | Deep-clone `Shape` objects for safe runtime mutation. |

## Items & Equipment

State and behaviour for items that need extra data on the stack.

| Page | Purpose |
|------|---------|
| [ItemCharge]({% link ItemCharge.md %}) | Charge, drain, refuel, and stat-gating for any `ItemStack`. |
| [ItemMode]({% link ItemMode.md %}) | Multi-mode items with F-key tool mode integration. |
| [Inventory / ItemStack helpers]({% link InventoryHelpers.md %}) | Give, count, find, and consume items. |

## Persistence & Progression

Per-save data and long-term player systems.

| Page | Purpose |
|------|---------|
| [ModDataStore]({% link ModDataStore.md %}) | Versioned per-savegame data with schema migration. |
| [PityTracker]({% link PityTracker.md %}) | Per-player pity/guarantee counters for loot or reward systems. |
| [Status Effects]({% link StatusEffects.md %}) | Timed buffs/debuffs with stack, refresh, and override modes. |

## Assets & Data

Loading, validating, and managing JSON assets from multiple mods.

| Page | Purpose |
|------|---------|
| [ModAssetLoader]({% link ModAssetLoader.md %}) | Load and merge typed JSON assets from all loaded mods. |
| [ModAssetRegistry]({% link ModAssetRegistry.md %}) | Build validated, keyed, source-tracked registries. |
| [TagSetExtensions]({% link TagSetExtensions.md %}) | Set operations for Vintage Story `TagSet`. |
| [ValidationResult]({% link ValidationResult.md %}) | Accumulate errors and warnings from validation pipelines. |

## Performance & Scheduling

Keep the server/client responsive.

| Page | Purpose |
|------|---------|
| [DeferredWork]({% link DeferredWork.md %}) | Game-tick scheduler for one-shot and coalesced work. |
| [TimedCache]({% link TimedCache.md %}) | Thread-safe cache with TTL eviction. |
| [CleanupScope]({% link CleanupScope.md %}) | Cancel listeners, work, and nested disposables in one call. |

## Common & Utility

Small helpers that remove boilerplate.

| Page | Purpose |
|------|---------|
| [ApiExtensions]({% link ApiExtensions.md %}) | `IsClient` / `IsServer` checks for API objects. |
| [CooldownTracker]({% link CooldownTracker.md %}) | Per-entity cooldowns in `WatchedAttributes`. |
| [EntityHealthExtensions]({% link EntityHealthExtensions.md %}) | Read and scale entity health. |
| [EventScope]({% link EventScope.md %}) | Disposable event subscription scope. |
| [LoggerExtensions]({% link LoggerExtensions.md %}) | Safe logging and non-critical warning helpers. |
| [PlayerExtensions]({% link PlayerExtensions.md %}) | Player entity iteration and position checks. |
| [WatchedAttributes]({% link WatchedAttributes.md %}) | Get-or-create and set helpers for `ITreeAttribute`. |

## Randomization & Geometry

| Page | Purpose |
|------|---------|
| [WeightedRandom]({% link WeightedRandom.md %}) | Weighted random picks and reusable weighted tables. |
| [ShapeCloner]({% link ShapeCloner.md %}) | Deep-clone `Shape` objects. |

## Networking

| Page | Purpose |
|------|---------|
| [TypedNetworkChannel]({% link TypedNetworkChannel.md %}) | Typed send/receive wrapper for network channels. |

---

## Quick start

Add `ArcanumLib.csproj` as a project reference, set `VINTAGE_STORY`, and add `arcanumlib` to `modinfo.json`:

```json
{
  "type": "mod",
  "modid": "mymod",
  "name": "MyMod",
  "dependson": [ { "modid": "arcanumlib" } ]
}
```

Two common examples:

```csharp
using ArcanumLib.Items;

float charge = ItemCharge.GetChargeValue(stack);
```

```csharp
using ArcanumLib.Persistence;

var store = ModDataStore.GetOrCreate<MySaveData>(sapi, "mymod", "state", 1);
store.Data.Counter++;
store.Save();
```
