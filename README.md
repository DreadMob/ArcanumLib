# Arcanum Lib

**A shared client/server utility library for Vintage Story**

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Game Version](https://img.shields.io/badge/Vintage%20Story-1.22.1+-green.svg)](https://www.vintagestory.at/)
[![Version](https://img.shields.io/badge/Version-1.0.0-orange.svg)](resources/modinfo.json)
[![C#](https://img.shields.io/badge/C%23-.NET%2010-purple.svg)]()
[![Alegacy Quest Framework](https://img.shields.io/badge/Used%20by-Alegacy%20Quest%20Framework-2563eb?style=for-the-badge&logo=gitlab)](https://gitlab.com/DreadMob/Alegacy-Quest-Framework)

---

## About

Arcanum Lib is a shared library mod for [Vintage Story](https://www.vintagestory.at/). It provides reusable GUI, rendering, color, asset, and scheduling helpers that can be consumed by other mods without each mod duplicating the same infrastructure.

One of its main goals is making compressed icon assets practical: you can use `.webp`, `.png`, `.jpg` and other Skia-decoded formats in the asset tree and draw them through the normal GUI pipeline. No manual conversion and no texture-atlas changes are required, because `ImageIconCache` routes the load through Vintage Story's built-in SkiaSharp image loader. AVIF and JPEG XL are not included in the Vintage Story `libSkiaSharp.dll`.

The library is currently used by the **[Alegacy Quest Framework](https://gitlab.com/DreadMob/Alegacy-Quest-Framework)** and is designed to be reusable by any Vintage Story mod that wants the same infrastructure.

---

## Quick Start

### Installation

1. Add `ArcanumLib.csproj` as a project reference in your mod project.
2. Set the `VINTAGE_STORY` environment variable to your Vintage Story installation directory.
3. Add `arcanumlib` to the `dependson` list in your `modinfo.json`.

### Example: charge-gated item

```csharp
using ArcanumLib.Items;

float charge = ItemCharge.GetChargeValue(stack);
if (ItemCharge.TryConsumeCharge(stack, 1f))
{
    // consume one use and proceed
}
```

### Example: per-save data

```csharp
using ArcanumLib.Persistence;

var store = ModDataStore.GetOrCreate<MySaveData>(sapi, "mymod", "state", 1);
store.Data.Counter++;
store.MarkDirty();
store.Save(); // no-op if nothing changed
```

See the [`docs/`](docs) folder for full API documentation and examples.

---

## Key Features

### GUI & Rendering

| Module | Description |
|--------|-------------|
| **Arcanum GUI Toolkit** | Themed colour palette, `ArcanumGuiTheme`, `ArcanumComposer`, layout helpers, `ArcanumFont`, and controls (`ArcanumCard`, `ArcanumIcon`, `ArcanumButton`, `ArcanumScrollbar`, `ArcanumList<T>`). |
| **ImageIconCache** | Load, cache and draw icon image surfaces with circle, hexagon, and diamond clipping plus optional tinting. |
| **RGBA** | Lightweight Cairo-friendly color struct with hex parsing, ARGB conversion, lerping, and alpha overrides. |
| **ModeIconBuilder** | Factory for `SkillItem` tool-mode icons (in-game icon, letter, or live `ItemStack` rendering). |

### Assets & Data

| Module | Description |
|--------|-------------|
| **ModAssetLoader** | Loads and merges typed JSON assets from all loaded mods, supporting multi-pack content definitions. |
| **ModAssetRegistry** | Builds validated, keyed, source-tracked registries from `ModAssetLoader` output. |
| **Pretty** | Converts raw asset codes into readable, title-cased display strings and sanitizes names. |
| **CollectibleNameResolver** | Resolves item, block and entity display names, wildcard matches and icon codes with caching. |
| **Wildcard** | Fast case-insensitive wildcard matching for asset codes. |
| **TagSetExtensions** | Set operations and readable aliases for `Vintagestory.API.Datastructures.TagSet`. |
| **WatchedAttributesExtensions** | Get-or-create, set-if-missing, and set-and-mark-dirty helpers for `ITreeAttribute`. |
| **CooldownTracker** | Per-entity cooldown state in `WatchedAttributes` with readiness, remaining, and progress checks. |
| **ModDataStore** | Versioned per-savegame data persistence with JSON migrations. |
| **ValidationResult** | Immutable result object that accumulates errors and warnings from validation pipelines. |

### Caching & Performance

| Module | Description |
|--------|-------------|
| **TimedCache<TKey, TValue>** | Thread-safe cache with TTL eviction and optional size limit. |
| **DeferredWork** | Game-tick scheduler for one-shot, coalesced and end-of-tick work. |
|| **CleanupScope** | Cancels `DeferredWork` keys, tick listeners, and nested disposables in one `Dispose()`. |
| **StatCoalescingEngine** | Batches rapid value changes into a single delayed update. |

### Services & Lifecycle

| Module | Description |
|--------|-------------|
| **ArcanumServices** | World-scoped service registry (`Register<T>` / `Get<T>` / `Shutdown`) for cross-mod instances. |
| **ArcanumLibModSystem** | Central `ModSystem` that registers the client/server API and clears caches on unload. |
| **ActionExecutorService / ActionRegistryService** | Instance-backed action execution behind the static `ActionExecutor` / `ActionRegistry` facades. |
| **StatusEffectService** | Instance-backed status effects behind the static `StatusEffectManager` facade. |
| **CategorizedLogger** | File and console logger with per-category files and throttled debug output. |
| **PityTracker** | Per-player pity counters; resolved through `ArcanumServices` or `PityTracker.Current`. |

### Randomization & Geometry

| Module | Description |
|--------|-------------|
| **WeightedRandom / WeightedTable** | Weighted random picks and reusable weighted tables with merge strategies. |
| **ShapeCloner** | Deep-clones Vintage Story `Shape` objects (textures, faces, attachment points) for safe mutation. |

### Common & Utility

| Module | Description |
|--------|-------------|
| **ApiExtensions** | `IsClient` / `IsServer` helpers for `ICoreAPI`, `IWorldAccessor`. |
| **EntityHealthExtensions** | Read and scale entity health through `WatchedAttributes` or `EntityBehaviorHealth`. |
| **PlayerExtensions** | `HasValidPosition`, `GetAliveEntities`, and `GetAliveServerEntities`. |
| **LoggerExtensions** | `LogNonCriticalWarning`, `LogGuiWarning`, and `SafeExecute` wrappers. |

### Status & Effects

| Module | Description |
|--------|-------------|
| **StatusEffectManager / StatusEffectService** | Apply, tick, and remove timed status effects with refresh, stack, override, and independent modes. The instance `StatusEffectService` is exposed through the static facade. |
| **StatModifierEffect** | Reusable effect that adds or removes values from an `EntityStats` category. |

### Networking & Inventory

| Module | Description |
|--------|-------------|
| **TypedNetworkChannel** | Typed network channel wrapper for send/receive with duplicate message-type protection. |
| **ServerBroadcaster** | Snapshot-based packet broadcast to all or filtered online players. |
| **InventoryChangeTracker** | Fingerprint-based inventory change detection with automatic disconnect cleanup. |
| **Inventory / ItemStack helpers** | Give, count, find, and consume items. |

### Progression

| Module | Description |
|--------|-------------|
| **PityTracker** | Thread-safe per-player pity counters with tiered guarantee rules, persistence, legacy savegame migration, and `ArcanumServices` integration. |

### Items & Equipment

| Module | Description |
|--------|-------------|
| **ItemCharge** | Generic charge, drain, refuel, and stat-gating helpers for any `ItemStack` with charge attributes. |
| **ItemModeManager** | Generic item mode data and F-key tool-mode integration (parsing, switching, effect gating). |

---

## Architecture

```
ArcanumLib/
├── ArcanumLibModSystem.cs    — Vintage Story entry point, lifecycle, and API registration
├── src/
│   ├── Core/                  — ArcanumServices, ArcanumLibModSystem
│   ├── Gui/                   — theme, composer, controls, layout, icons
│   ├── Geometry/              — ShapeCloner
│   ├── Caching/               — TimedCache and SimpleLRUCache
│   ├── Common/                — EventScope, CleanupScope, PlaytimeTracker, PlaytimeCooldownManager
│   ├── Data/                  — TagSet, WatchedAttributes, and CooldownTracker
│   ├── Validation/            — ValidationResult
│   ├── Assets/                — ModAssetLoader, ModAssetRegistry
│   ├── Performance/           — DeferredWork, GameTimeScheduler, StatCoalescingEngine
│   ├── Random/                — WeightedRandom, WeightedTable
│   ├── Text/                  — Pretty, Wildcard
│   ├── Network/               — TypedNetworkChannel, ServerBroadcaster
│   ├── Helpers/               — CollectibleNameResolver
│   ├── Persistence/           — ModDataStore
│   ├── Actions/               — ActionRegistry, ActionExecutor, ActionRegistryService, ActionExecutorService
│   ├── Effects/               — StatusEffectManager, StatusEffectService
│   ├── Progression/           — PityTracker
│   ├── Inventory/             — InventoryChangeTracker, InventoryFingerprint, InventoryHelpers
│   ├── Logging/               — CategorizedLogger
│   └── Items/                 — ItemCharge, ItemMode, ItemModeManager
├── docs/                      — API documentation
├── resources/
│   └── modinfo.json           — mod metadata
├── LICENSE                    — MIT License
└── README.md                  — this file
```

---

## Building

```bash
dotnet build
```

Requires:

- .NET 10 SDK
- Vintage Story 1.22.1+ (the build expects the `VINTAGE_STORY` environment variable to point to the game directory)

---

## Documentation

Full documentation site: **https://dreadmob.github.io/ArcanumLib/**

API documentation also lives in the [`docs/`](docs) folder:

### GUI & Rendering
- [Arcanum GUI Toolkit](docs/ArcanumGui.md)
- [ImageIconCache](docs/ImageIconCache.md)
- [ModeIconBuilder](docs/ModeIconBuilder.md)
- [RGBA](docs/RGBA.md)
- [ShapeCloner](docs/ShapeCloner.md)

### Items & Equipment
- [ItemCharge](docs/ItemCharge.md)
- [ItemMode](docs/ItemMode.md)
- [Inventory / ItemStack helpers](docs/InventoryHelpers.md)

### Persistence & Progression
- [ModDataStore](docs/ModDataStore.md)
- [PityTracker](docs/PityTracker.md)
- [Status Effects](docs/StatusEffects.md)

### Services & Lifecycle
- [ArcanumServices](docs/ArcanumServices.md)
- [ArcanumLibModSystem](docs/ArcanumLibModSystem.md)
- [ActionRegistry](docs/ActionRegistry.md)

### Assets & Data
- [ModAssetLoader](docs/ModAssetLoader.md)
- [ModAssetRegistry](docs/ModAssetRegistry.md)
- [TagSetExtensions](docs/TagSetExtensions.md)
- [ValidationResult](docs/ValidationResult.md)

### Performance & Scheduling
- [DeferredWork](docs/DeferredWork.md)
- [TimedCache](docs/TimedCache.md)
- [CleanupScope](docs/CleanupScope.md)

### Common & Utility
- [ApiExtensions](docs/ApiExtensions.md)
- [CooldownTracker](docs/CooldownTracker.md)
- [EntityHealthExtensions](docs/EntityHealthExtensions.md)
- [EventScope](docs/EventScope.md)
- [LoggerExtensions](docs/LoggerExtensions.md)
- [PlayerExtensions](docs/PlayerExtensions.md)
- [WatchedAttributes](docs/WatchedAttributes.md)

### Randomization
- [WeightedRandom](docs/WeightedRandom.md)

### Networking
- [TypedNetworkChannel](docs/TypedNetworkChannel.md)

---

## Authors

- **[DreadMob](https://gitlab.com/DreadMob)**

---

## License

This project is licensed under the [MIT License](LICENSE).

You are free to use, modify, and distribute this library, provided that the original copyright notice and license text remain intact in any copy or substantial modification.
