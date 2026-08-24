---
layout: default
title: README
search_exclude: true
nav_exclude: true
---

# ArcanumLib Documentation

This folder contains API documentation for `ArcanumLib` — a shared Vintage Story modding library for client-side GUI rendering, color manipulation, geometry, caching, persistence, and other cross-mod utilities.

## Quick start

- If you are new, start with the [root README](../README.md) for installation, feature overview, and architecture.
- Browse by category below or use the module index for a single-page reference.

---

## GUI & Rendering

| Page | What it covers |
|------|----------------|
| [Arcanum GUI Toolkit](ArcanumGui.md) | Themed colour palette, `ArcanumGuiTheme`, `ArcanumComposer`, layout helpers, `ArcanumFont`, and controls (`ArcanumCard`, `ArcanumIcon`, `ArcanumButton`, `ArcanumScrollbar`, `ArcanumList<T>`). |
| [Radial Menu](RadialMenu.md) | Generic Cairo-styled radial (pie) menu with `RadialMenuGui`, `RadialMenuItem`, pluggable `IRadialMenuStyle` themes, and `RadialMenuStyleRegistry`. |
| [ItemListElement](ItemListElement.md) | Scrollable vertical list with icon nodes, status colours, tooltips, and `CustomIconRegistry` support. |
| [CustomTabContent](CustomTabContent.md) | Data-driven scrollable tab content with decorative Cairo icons, section layout, and `CustomTabData` model. |
| [GuiDateTimePicker](GuiDateTimePicker.md) | Reusable date/time picker for `GuiComposer` with Now/Clear buttons and parameterized lang keys. |
| [Custom Icons](CustomIcons.md) | `ICustomIconRenderer`, `CustomIconRegistry`, and `CustomTabIconRenderer` for custom Cairo-drawn GUI icons. |
| [ImageIconCache](ImageIconCache.md) | Load, cache and draw icon image surfaces with circle, hexagon, and diamond clipping plus optional tinting. |
| [ModeIconBuilder](ModeIconBuilder.md) | Factory for tool-mode icons (in-game icon, letter, or live `ItemStack` rendering). |

## Items & Equipment

| Page | What it covers |
|------|----------------|
| [ItemCharge](ItemCharge.md) | Generic charge, drain, refuel, and stat-gating helpers for any `ItemStack` with charge attributes. |
| [ItemMode](ItemMode.md) | Generic item mode data and F-key tool-mode integration (parsing, switching, effect gating). |
| [Inventory / ItemStack helpers](InventoryHelpers.md) | Give, count, find, and consume items. |

## Services & Lifecycle

| Page | What it covers |
|------|----------------|
| [ArcanumServices](ArcanumServices.md) | World-scoped service registry for cross-mod instance lookup and lifecycle. |
| [ArcanumLibModSystem](ArcanumLibModSystem.md) | Central lifecycle `ModSystem` that registers APIs and clears state on unload. |

## Events & Commands

| Page | What it covers |
|------|----------------|
| [EventBus](EventBus.md) | Typed publish/subscribe event bus for cross-mod communication. |
| [CommandBuilder](CommandBuilder.md) | Fluent command framework with typed arguments, permissions, and autocomplete. |

## Persistence

| Page | What it covers |
|------|----------------|
| [ModDataStore](ModDataStore.md) | Versioned per-savegame data persistence with dirty tracking and JSON migrations. |

## Progression & Status

| Page | What it covers |
|------|----------------|
| [PityTracker](PityTracker.md) | Thread-safe per-player pity counters with tiered guarantee rules, persistence, legacy migration, and `ArcanumServices` integration. |
| [Status Effects](StatusEffects.md) | Apply, tick, and remove timed status effects; `StatusEffectService` is exposed through the static `StatusEffectManager` facade. |

## Assets & Data

| Page | What it covers |
|------|----------------|
| [ModAssetLoader](ModAssetLoader.md) | Loads and merges typed JSON assets from all loaded mods, supporting multi-pack content definitions. |
| [ModAssetRegistry](ModAssetRegistry.md) | Builds validated, keyed, source-tracked registries from `ModAssetLoader` output. |
| [TagSetExtensions](TagSetExtensions.md) | Set operations and readable aliases for `Vintagestory.API.Datastructures.TagSet`. |
| [ValidationResult](ValidationResult.md) | Immutable result object that accumulates errors and warnings from validation pipelines. |

## Caching & Performance

| Page | What it covers |
|------|----------------|
| [TimedCache](TimedCache.md) | Thread-safe cache with TTL eviction and optional size limit. |
| [DeferredWork](DeferredWork.md) | Game-tick scheduler for one-shot, coalesced and end-of-tick work. |
| [GameTimeScheduler](GameTimeScheduler.md) | Daily/hourly/after-hours scheduling based on `World.Calendar.TotalHours`. |
| [StatCoalescingEngine](StatCoalescingEngine.md) | Batches rapid `EntityStats.Set` calls into a single network sync. |
| [CleanupScope](CleanupScope.md) | Cancels `DeferredWork` keys, tick listeners, and nested disposables in one `Dispose()`. |

## Randomization & Geometry

| Page | What it covers |
|------|----------------|
| [WeightedRandom](WeightedRandom.md) | Weighted random picks and reusable weighted tables with merge strategies. |
| [PositionUtils](PositionUtils.md) | Random horizontal offsets and ground-level position finding around entities. |
| [BlockEntitySearchUtils](BlockEntitySearchUtils.md) | Chunk-based block entity counting within a region. |

## Particle Effects

| Page | What it covers |
|------|----------------|
| [Particle Effects](ParticleEffects.md) | Fluent `ParticleEffectBuilder`, named color presets, and ready-to-use effect presets (explosions, auras, impacts, shockwaves, ambient). |

## Common & Utility

| Page | What it covers |
|------|----------------|
| [ChatFormatUtil](ChatFormatUtil.md) | Colorize chat and HUD text with `<font>` tags; alert-prefixed messages. |
| [DamageHelper](DamageHelper.md) | Factory for `DamageSource` with common field combinations (Entity/Player/Weather/Internal). |
| [LoggerExtensions](LoggerExtensions.md) | Non-critical warning logging and `SafeExecute` wrappers. |
| [EventScope](EventScope.md) | Disposable event subscription scope with automatic unsubscription. |
| [PlaytimeTracker](PlaytimeTracker.md) | Per-player online time tracking, login streaks, real-time cooldowns, and combat-state checks. |
| [WatchedAttributes](WatchedAttributes.md) | Get-or-create, set-if-missing, and set-and-mark-dirty helpers for `ITreeAttribute`. |
| [CooldownTracker](CooldownTracker.md) | Per-entity cooldown state in `WatchedAttributes` with readiness, remaining, and progress checks. |
| [Misc Helpers](MiscHelpers.md) | Thin sugar: `IsClient`/`IsServer`, `RGBA`, `Pretty`, `Wildcard`, `CollectibleNameResolver`, `EntityHealthExtensions`, `PlayerExtensions`, `ShapeCloner`. |

## Networking

| Page | What it covers |
|------|----------------|
| [TypedNetworkChannel](TypedNetworkChannel.md) | Typed network channel wrapper with duplicate message-type protection. |
| [ServerBroadcaster](ServerBroadcaster.md) | Snapshot-based packet broadcast to all or filtered online players. |

## Inventory

| Page | What it covers |
|------|----------------|
| [InventoryChangeTracker](InventoryChangeTracker.md) | Fingerprint-based inventory change detection with disconnect cleanup. |
| [Inventory / ItemStack helpers](InventoryHelpers.md) | Give, count, find, and consume items. |

## Logging

| Page | What it covers |
|------|----------------|
| [CategorizedLogger](CategorizedLogger.md) | File and console logger with per-category files and throttled debug output. |

---

## Target Framework

- .NET 10
- Vintage Story 1.22.1+
