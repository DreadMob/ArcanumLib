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
| [ImageIconCache](ImageIconCache.md) | Load, cache and draw icon image surfaces with circle, hexagon, and diamond clipping plus optional tinting. |
| [ModeIconBuilder](ModeIconBuilder.md) | Factory for `SkillItem` tool-mode icons (in-game icon, letter, or live `ItemStack` rendering). |
| [RGBA](RGBA.md) | Lightweight Cairo-friendly color struct with hex parsing, ARGB conversion, lerping, and alpha overrides. |
| [ShapeCloner](ShapeCloner.md) | Deep-clones Vintage Story `Shape` objects (textures, faces, attachment points) for safe mutation. |

## Items & Equipment

| Page | What it covers |
|------|----------------|
| [ItemCharge](ItemCharge.md) | Generic charge, drain, refuel, and stat-gating helpers for any `ItemStack` with charge attributes. |
| [ItemMode](ItemMode.md) | Generic item mode data and F-key tool-mode integration (parsing, switching, effect gating). |
| [Inventory / ItemStack helpers](InventoryHelpers.md) | Give, count, find, and consume items. |

## Persistence

| Page | What it covers |
|------|----------------|
| [ModDataStore](ModDataStore.md) | Versioned per-savegame data persistence with schema versioning. |

## Progression & Status

| Page | What it covers |
|------|----------------|
| [PityTracker](PityTracker.md) | Per-player pity counters with tiered guarantee rules, persistence, and legacy savegame migration. |
| [Status Effects](StatusEffects.md) | Apply, tick, and remove timed status effects with refresh, stack, override, and independent modes. |

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
| [CleanupScope](CleanupScope.md) | Cancels `DeferredWork` keys, tick listeners, and nested disposables in one `Dispose()`. |

## Randomization & Geometry

| Page | What it covers |
|------|----------------|
| [WeightedRandom](WeightedRandom.md) | Weighted random picks and reusable weighted tables with merge strategies. |
| [ShapeCloner](ShapeCloner.md) | Deep-clones Vintage Story `Shape` objects for safe mutation. |

## Common & Utility

| Page | What it covers |
|------|----------------|
| [ApiExtensions](ApiExtensions.md) | `IsClient` / `IsServer` helpers for `ICoreAPI`, `IWorldAccessor`. |
| [EntityHealthExtensions](EntityHealthExtensions.md) | Read and scale entity health through `WatchedAttributes` or `EntityBehaviorHealth`. |
| [PlayerExtensions](PlayerExtensions.md) | `HasValidPosition`, `GetAliveEntities`, and `GetAliveServerEntities`. |
| [LoggerExtensions](LoggerExtensions.md) | Non-critical warning logging and `SafeExecute` wrappers. |
| [EventScope](EventScope.md) | Disposable event subscription scope with automatic unsubscription. |
| [WatchedAttributes](WatchedAttributes.md) | Get-or-create, set-if-missing, and set-and-mark-dirty helpers for `ITreeAttribute`. |
| [CooldownTracker](CooldownTracker.md) | Per-entity cooldown state in `WatchedAttributes` with readiness, remaining, and progress checks. |

## Networking

| Page | What it covers |
|------|----------------|
| [TypedNetworkChannel](TypedNetworkChannel.md) | Typed network channel wrapper for send/receive. |

---

## Target Framework

- .NET 10
- Vintage Story 1.22.1+
