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
| [Radial Menu]({{ site.baseurl }}{% link RadialMenu.md %}) | Generic Cairo-styled radial (pie) menu with pluggable styles via `IRadialMenuStyle` registry. |
| [ItemListElement]({{ site.baseurl }}{% link ItemListElement.md %}) | Scrollable vertical list with icon nodes, status colours, tooltips, and custom icon support. |
| [CustomTabContent]({{ site.baseurl }}{% link CustomTabContent.md %}) | Data-driven scrollable tab content with decorative Cairo icons and section layout. |
| [GuiDateTimePicker]({{ site.baseurl }}{% link GuiDateTimePicker.md %}) | Reusable date/time picker for `GuiComposer` with Now/Clear buttons. |
| [Custom Icons]({{ site.baseurl }}{% link CustomIcons.md %}) | Registry and interface for custom Cairo-drawn GUI icons keyed by string. |
|| [HUDs & Overlays]({{ site.baseurl }}{% link Huds.md %}) | Generic HUD panels, client systems, packet icon HUDs, and transient overlays. |
|| [Holograms]({{ site.baseurl }}{% link Holograms.md %}) | Floating text labels above blocks with texture caching and 3D projection. |
| [BlockEntityConfigDialog]({{ site.baseurl }}{% link BlockEntityConfigDialog.md %}) | Generic base dialog for editing block entity configuration with typed config and save/cancel. |
| [ImageIconCache]({{ site.baseurl }}{% link ImageIconCache.md %}) | Load and draw `.png`/`.jpg`/`.webp` icons with clipping and tinting. |

## Items & Equipment

State and behaviour for items that need extra data on the stack.

| Page | Purpose |
|------|---------|
| [ItemCharge]({{ site.baseurl }}{% link ItemCharge.md %}) | Charge, drain, refuel, and stat-gating for any `ItemStack`. |
| [ItemMode]({{ site.baseurl }}{% link ItemMode.md %}) | Multi-mode items with F-key tool mode integration. |
| [ItemStackBuilder]({{ site.baseurl }}{% link ItemStackBuilder.md %}) | Fluent builder for constructing `ItemStack` instances with attributes and durability. |
| [Inventory / ItemStack helpers]({{ site.baseurl }}{% link InventoryHelpers.md %}) | Give, count, find, and consume items. |
| [InventoryChangeTracker]({{ site.baseurl }}{% link InventoryChangeTracker.md %}) | Throttled inventory fingerprinting to skip expensive recalculations when nothing changed. |

## Actions

A reusable action registry for executing JSON-declared actions through typed handlers.

| Page | Purpose |
|------|---------|
| [ActionRegistry]({{ site.baseurl }}{% link ActionRegistry.md %}) | Register typed handlers and execute JSON action descriptors with validation, cooldowns, and permissions. |

## Persistence & Progression

Per-save data and long-term player systems.

| Page | Purpose |
|------|---------|
| [ModDataStore]({{ site.baseurl }}{% link ModDataStore.md %}) | Versioned per-savegame data with schema migration. |
| [ModConfig]({{ site.baseurl }}{% link ModConfig.md %}) | Typed config wrapper with load, save, validation, and defaults. |
| [PityTracker]({{ site.baseurl }}{% link PityTracker.md %}) | Per-player pity/guarantee counters for loot or reward systems. |
| [Status Effects]({{ site.baseurl }}{% link StatusEffects.md %}) | Timed buffs/debuffs with stack, refresh, and override modes. |

## Assets & Data

Loading, validating, and managing JSON assets from multiple mods.

| Page | Purpose |
|------|---------|
| [ModAssetLoader]({{ site.baseurl }}{% link ModAssetLoader.md %}) | Load and merge typed JSON assets from all loaded mods. |
| [ModAssetRegistry]({{ site.baseurl }}{% link ModAssetRegistry.md %}) | Build validated, keyed, source-tracked registries. |
| [TagMatcher]({{ site.baseurl }}{% link TagMatcher.md %}) | Match collectibles and item stacks against include/exclude tag-sets and code patterns. |
| [ValidationResult]({{ site.baseurl }}{% link ValidationResult.md %}) | Accumulate errors and warnings from validation pipelines. |

## Performance & Scheduling

Keep the server/client responsive.

| Page | Purpose |
|------|---------|
| [DeferredWorkService]({{ site.baseurl }}{% link DeferredWork.md %}) | Game-tick scheduler for one-shot, callback, and coalesced work. |
| [StatCoalescingEngine]({{ site.baseurl }}{% link StatCoalescingEngine.md %}) | Batch `EntityStats.Set` calls into a single network sync. |
| [GameTimeScheduler]({{ site.baseurl }}{% link GameTimeScheduler.md %}) | Schedule recurring actions by in-game time (daily, hourly, after N hours). |
| [TimedCache]({{ site.baseurl }}{% link TimedCache.md %}) | Thread-safe cache with TTL eviction. |
| [CleanupScope]({{ site.baseurl }}{% link CleanupScope.md %}) | Cancel listeners, work, and nested disposables in one call. |

## Events & Commands

| Page | Purpose |
|------|---------|
| [EventBusService]({{ site.baseurl }}{% link EventBus.md %}) | Typed publish/subscribe event bus for cross-mod communication. |


## Diagnostics

Runtime validation and monitoring for ArcanumLib modules and dependent mods.

| Page | Purpose |
|------|---------|
| [Diagnostics]({{ site.baseurl }}{% link Diagnostics.md %}) | Automatic startup checks for services, ModSystems, EventBus health, dependency versions, and runtime monitoring (tick time, memory, entities). |

## Logging

Categorized file logging for any mod.

| Page | Purpose |
|------|---------|
| [CategorizedLogger]({{ site.baseurl }}{% link CategorizedLogger.md %}) | Categorized file logger with modes, throttling, auto-flush, and configurable folder/prefix. |

## Common & Utility

Helpers that remove boilerplate for common Vintage Story patterns.

| Page | Purpose |
|------|---------|

| [Misc Helpers]({{ site.baseurl }}{% link MiscHelpers.md %}) | Thin sugar and helper factories: chat formatting, damage sources, side checks, health scaling, player filters. |

| [CooldownTracker]({{ site.baseurl }}{% link CooldownTracker.md %}) | Per-entity cooldowns in `WatchedAttributes`. |

| [EventScope]({{ site.baseurl }}{% link EventScope.md %}) | Disposable event subscription scope. |
| [PlaytimeTracker]({{ site.baseurl }}{% link PlaytimeTracker.md %}) | Per-player online time tracking, login streaks, real-time cooldowns, and combat-state checks. |
| [WatchedAttributes]({{ site.baseurl }}{% link WatchedAttributes.md %}) | Get-or-create and set helpers for `ITreeAttribute`. |


## Randomization & Geometry

| Page | Purpose |
|------|---------|
| [WeightedRandom]({{ site.baseurl }}{% link WeightedRandom.md %}) | Weighted random picks and reusable weighted tables. |
| [LootTable]({{ site.baseurl }}{% link LootTable.md %}) | JSON-friendly loot tables with tiers, weighted entries, and luck multipliers. |
| [PositionUtils]({{ site.baseurl }}{% link PositionUtils.md %}) | Random horizontal offsets and ground-level position finding around entities. |
| [BlockEntitySearchUtils]({{ site.baseurl }}{% link BlockEntitySearchUtils.md %}) | Chunk-based block entity counting within a region. |

## Particle Effects

| Page | Purpose |
|------|---------|
| [Particle Effects]({{ site.baseurl }}{% link ParticleEffects.md %}) | Fluent `ParticleEffectBuilder`, named color presets, and ready-to-use effect presets (explosions, auras, impacts, shockwaves, ambient). |

## Networking

| Page | Purpose |
|------|---------|
| [TypedNetworkChannel]({{ site.baseurl }}{% link TypedNetworkChannel.md %}) | Typed send/receive wrapper for network channels. |
| [ServerBroadcaster]({{ site.baseurl }}{% link ServerBroadcaster.md %}) | Broadcast packets to all online players with predicate, radius, and exclusion filters. |

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
