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
| [Arcanum GUI Toolkit]({{ site.baseurl }}{% link ArcanumGui.md %}) | Themed colour palette, layout helpers, composer, and reusable controls. |
| [ImageIconCache]({{ site.baseurl }}{% link ImageIconCache.md %}) | Load and draw `.png`/`.jpg`/`.webp` icons with clipping and tinting. |
| [ModeIconBuilder]({{ site.baseurl }}{% link ModeIconBuilder.md %}) | Build tool-mode and skill-bar icon entries with image, letter, or item stack rendering. |
| [RGBA]({{ site.baseurl }}{% link RGBA.md %}) | Cairo-friendly color struct with parsing, conversion, and lerping. |

## Items & Equipment

State and behaviour for items that need extra data on the stack.

| Page | Purpose |
|------|---------|
| [ItemCharge]({{ site.baseurl }}{% link ItemCharge.md %}) | Charge, drain, refuel, and stat-gating for any `ItemStack`. |
| [ItemMode]({{ site.baseurl }}{% link ItemMode.md %}) | Multi-mode items with F-key tool mode integration. |
| [Inventory / ItemStack helpers]({{ site.baseurl }}{% link InventoryHelpers.md %}) | Give, count, find, and consume items. |

## Persistence & Progression

Per-save data and long-term player systems.

| Page | Purpose |
|------|---------|
| [ModDataStore]({{ site.baseurl }}{% link ModDataStore.md %}) | Versioned per-savegame data with schema migration. |
| [PityTracker]({{ site.baseurl }}{% link PityTracker.md %}) | Per-player pity/guarantee counters for loot or reward systems. |
| [Status Effects]({{ site.baseurl }}{% link StatusEffects.md %}) | Timed buffs/debuffs with stack, refresh, and override modes. |

## Assets & Data

Loading, validating, and managing JSON assets from multiple mods.

| Page | Purpose |
|------|---------|
| [ModAssetLoader]({{ site.baseurl }}{% link ModAssetLoader.md %}) | Load and merge typed JSON assets from all loaded mods. |
| [ModAssetRegistry]({{ site.baseurl }}{% link ModAssetRegistry.md %}) | Build validated, keyed, source-tracked registries. |
| [TagSetExtensions]({{ site.baseurl }}{% link TagSetExtensions.md %}) | Set operations for Vintage Story `TagSet`. |
| [ValidationResult]({{ site.baseurl }}{% link ValidationResult.md %}) | Accumulate errors and warnings from validation pipelines. |

## Performance & Scheduling

Keep the server/client responsive.

| Page | Purpose |
|------|---------|
| [DeferredWork]({{ site.baseurl }}{% link DeferredWork.md %}) | Game-tick scheduler for one-shot and coalesced work. |
| [TimedCache]({{ site.baseurl }}{% link TimedCache.md %}) | Thread-safe cache with TTL eviction. |
| [CleanupScope]({{ site.baseurl }}{% link CleanupScope.md %}) | Cancel listeners, work, and nested disposables in one call. |

## Common & Utility

Small helpers that remove boilerplate. Some are thin syntactic sugar over existing Vintage Story APIs.

| Page | Purpose |
|------|---------|
| [ApiExtensions]({{ site.baseurl }}{% link ApiExtensions.md %}) | `IsClient` / `IsServer` checks for `ICoreAPI` and `IWorldAccessor` (sugar for `api.Side`). |
| [CooldownTracker]({{ site.baseurl }}{% link CooldownTracker.md %}) | Per-entity cooldowns in `WatchedAttributes`. |
| [EntityHealthExtensions]({{ site.baseurl }}{% link EntityHealthExtensions.md %}) | Read and scale entity health. |
| [EventScope]({{ site.baseurl }}{% link EventScope.md %}) | Disposable event subscription scope. |
| [LoggerExtensions]({{ site.baseurl }}{% link LoggerExtensions.md %}) | `SafeExecute` wrapper and non-critical warning helpers. |
| [PlayerExtensions]({{ site.baseurl }}{% link PlayerExtensions.md %}) | Player entity iteration and position checks. |
| [WatchedAttributes]({{ site.baseurl }}{% link WatchedAttributes.md %}) | Get-or-create and set helpers for `ITreeAttribute`. |

## Randomization & Geometry

| Page | Purpose |
|------|---------|
| [WeightedRandom]({{ site.baseurl }}{% link WeightedRandom.md %}) | Weighted random picks and reusable weighted tables. |
| [ShapeCloner]({{ site.baseurl }}{% link ShapeCloner.md %}) | Deep-clone `Shape` objects. |

## Networking

| Page | Purpose |
|------|---------|
| [TypedNetworkChannel]({{ site.baseurl }}{% link TypedNetworkChannel.md %}) | Typed send/receive wrapper for network channels. |

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
