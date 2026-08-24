# Arcanum Lib

**A shared client/server utility library for Vintage Story**

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Game Version](https://img.shields.io/badge/Vintage%20Story-1.22.1+-green.svg)](https://www.vintagestory.at/)
[![Version](https://img.shields.io/badge/Version-1.0.0--rc1-orange.svg)](resources/modinfo.json)
[![C#](https://img.shields.io/badge/C%23-.NET%2010-purple.svg)]()
[![Alegacy Quest Framework](https://img.shields.io/badge/Used%20by-Alegacy%20Quest%20Framework-2563eb?style=for-the-badge&logo=gitlab)](https://gitlab.com/DreadMob/Alegacy-Quest-Framework)

---

## About

Arcanum Lib is a shared library mod for [Vintage Story](https://www.vintagestory.at/). It provides reusable infrastructure that other mods would otherwise duplicate: a themed GUI toolkit, typed asset loading, status effects, action registry, particle builders, persistence, networking, and scheduling.

The library is built around a few headline features that have no vanilla equivalent:

- **Status Effects** — timed buffs/debuffs with stacking, refresh, override, and independent modes. No vanilla equivalent.
- **HUDs & Overlays** — generic `HudPanel`, `HudDialog`, and `HudClientSystem`, plus `TransientOverlay` for toasts, `PacketIconHud` for packet-driven icon bars, and `IHudElementRenderer` for reusable panel elements. No vanilla equivalent.
- **Holograms** — floating text labels above blocks with `SingleHologramRenderer` and `AreaHologramRenderer`, versioned texture caching, and 3D projection. No vanilla equivalent.
- **Action Registry** — JSON-declared actions with typed handlers, cooldowns, and permissions. Lets content packs add behaviour without recompiling.
- **EventBus** — typed publish/subscribe event bus for cross-mod communication. Mods publish events without knowing who subscribes.
- **CommandBuilder** — fluent command framework with typed arguments, permissions, and autocomplete. Replaces manual `CmdArgs` parsing.
- **Particle Effect Builder** — fluent builder with named presets (explosions, auras, impacts, shockwaves, ambient). Replaces 20-field `SimpleParticleProperties` setup with one line.
- **Radial Menu** — Cairo-styled pie menu with pluggable `IRadialMenuStyle` themes. No vanilla equivalent.
- **ModDataStore** — versioned per-savegame persistence with schema migrations. Replaces raw `StoreData` string dictionaries.
- **PityTracker** — per-player pity/guarantee counters for loot systems.

Beyond those, the library ships thinner helpers (color structs, wildcard matching, watched-attributes helpers, damage source factories, etc.) collected under [Misc Helpers](docs/MiscHelpers.md) so they don't dilute the main feature pages.

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
| **Radial Menu** | Generic Cairo-styled radial (pie) menu with pluggable `IRadialMenuStyle` themes and `RadialMenuStyleRegistry`. |
| **ItemListElement** | Scrollable vertical list with icon nodes, status colours, tooltips, and `CustomIconRegistry` support. |
| **CustomTabContent** | Data-driven scrollable tab content with decorative Cairo icons and `CustomTabData` model. |
| **GuiDateTimePicker** | Reusable date/time picker for `GuiComposer` with Now/Clear buttons. |
| **Custom Icons** | `ICustomIconRenderer`, `CustomIconRegistry`, and `CustomTabIconRenderer` for custom Cairo-drawn GUI icons. |
|| **HUDs & Overlays** | `HudPanel`/`HudDialog`/`HudClientSystem`, `TransientOverlay` for toasts, `PacketIconHud` for packet-driven icon bars, and `IHudElementRenderer` element registry. |
|| **Holograms** | `SingleHologramRenderer` and `AreaHologramRenderer` for floating block labels with `IHologramTextSource`, versioned texture caching, and 3D projection. |
| **ImageIconCache** | Load, cache and draw icon image surfaces with circle, hexagon, and diamond clipping plus optional tinting. |
| **ModeIconBuilder** | Factory for tool-mode icons (in-game icon, letter, or live `ItemStack` rendering). |

### Items & Equipment

| Module | Description |
|--------|-------------|
| **ItemCharge** | Generic charge, drain, refuel, and stat-gating helpers for any `ItemStack` with charge attributes. |
| **ItemModeManager** | Generic item mode data and F-key tool-mode integration (parsing, switching, effect gating). |
| **Inventory / ItemStack helpers** | Give, count, find, and consume items. |

### Services & Lifecycle

| Module | Description |
|--------|-------------|
| **ArcanumServices** | World-scoped service registry (`Register<T>` / `Get<T>` / `Shutdown`) for cross-mod instances. |
| **ArcanumLibModSystem** | Central `ModSystem` that registers the client/server API and clears caches on unload. |
| **ActionRegistry / ActionExecutor** | Typed action registry with JSON-declared actions, cooldowns, and permissions. |
| **CategorizedLogger** | File and console logger with per-category files and throttled debug output. |

### Events & Commands

| Module | Description |
|--------|-------------|
| **EventBus** | Typed publish/subscribe event bus for cross-mod communication. Mods publish events without knowing who subscribes. |
| **CommandBuilder** | Fluent command framework with typed arguments, permission gating, and autocomplete. |

### Persistence & Progression

| Module | Description |
|--------|-------------|
| **ModDataStore** | Versioned per-savegame data persistence with dirty tracking and JSON migrations. |
| **PityTracker** | Thread-safe per-player pity counters with tiered guarantee rules, persistence, and legacy migration. |

### Status & Effects

| Module | Description |
|--------|-------------|
| **StatusEffectManager / StatusEffectService** | Apply, tick, and remove timed status effects with refresh, stack, override, and independent modes. |
| **StatModifierEffect** | Reusable effect that adds or removes values from an `EntityStats` category. |

### Assets & Data

| Module | Description |
|--------|-------------|
| **ModAssetLoader** | Loads and merges typed JSON assets from all loaded mods, supporting multi-pack content definitions. |
| **ModAssetRegistry** | Builds validated, keyed, source-tracked registries from `ModAssetLoader` output. |
| **TagSetExtensions** | Set operations and readable aliases for `Vintagestory.API.Datastructures.TagSet`. |
| **ValidationResult** | Immutable result object that accumulates errors and warnings from validation pipelines. |

### Caching & Performance

| Module | Description |
|--------|-------------|
| **TimedCache<TKey, TValue>** | Thread-safe cache with TTL eviction and optional size limit. |
| **DeferredWork** | Game-tick scheduler for one-shot, coalesced and end-of-tick work. |
| **GameTimeScheduler** | Daily/hourly/after-hours scheduling based on `World.Calendar.TotalHours`. |
| **CleanupScope** | Cancels `DeferredWork` keys, tick listeners, and nested disposables in one `Dispose()`. |
| **StatCoalescingEngine** | Batches rapid `EntityStats.Set` calls into a single network sync. |

### Randomization & Geometry

| Module | Description |
|--------|-------------|
| **WeightedRandom / WeightedTable** | Weighted random picks and reusable weighted tables with merge strategies. |
| **LootTable** | JSON-friendly loot tables with tiers, weighted entries, and luck multipliers. |
| **PositionUtils** | Random horizontal offsets and ground-level position finding around entities. |
| **BlockEntitySearchUtils** | Chunk-based block entity counting within a region. |

### Particle Effects

| Module | Description |
|--------|-------------|
| **ParticleEffectBuilder** | Fluent builder with named color presets and ready-to-use effect presets (explosions, auras, impacts, shockwaves, ambient). |

### Networking & Inventory

| Module | Description |
|--------|-------------|
| **TypedNetworkChannel** | Typed network channel wrapper with duplicate message-type protection. |
| **ServerBroadcaster** | Snapshot-based packet broadcast to all or filtered online players. |
| **InventoryChangeTracker** | Fingerprint-based inventory change detection with disconnect cleanup. |

### Common & Utility

| Module | Description |
|--------|-------------|
| **ChatFormatUtil** | Colorize chat and HUD text with `<font>` tags; alert-prefixed messages. |
| **DamageHelper** | Factory for `DamageSource` with common field combinations (Entity/Player/Weather/Internal). |
| **EventScope** | Disposable event subscription scope with automatic unsubscription. |
| **PlaytimeTracker** | Per-player online time tracking, login streaks, real-time cooldowns, and combat-state checks. |
| **WatchedAttributesExtensions** | Get-or-create, set-if-missing, and set-and-mark-dirty helpers for `ITreeAttribute`. |
| **CooldownTracker** | Per-entity cooldown state in `WatchedAttributes` with readiness, remaining, and progress checks. |
| **Misc Helpers** | Thin sugar: `IsClient`/`IsServer`, `RGBA`, `Pretty`, `Wildcard`, `CollectibleNameResolver`, `EntityHealthExtensions`, `PlayerExtensions`, `ShapeCloner`. |

---

## Architecture

```
ArcanumLib/
├── ArcanumLibModSystem.cs    — Vintage Story entry point, lifecycle, and API registration
├── src/
│   ├── Core/                  — ArcanumServices, ArcanumLibModSystem
│   ├── Gui/                   — theme, composer, controls, layout, icons, radial menu, huds, holograms
│   ├── Geometry/              — PositionUtils, BlockEntitySearchUtils
│   ├── Caching/               — TimedCache and SimpleLRUCache
│   ├── Common/                — EventScope, CleanupScope, PlaytimeTracker, PlaytimeCooldownManager
│   ├── Events/                — EventBus, IEvent
│   ├── Commands/              — CommandBuilder
│   ├── Data/                  — TagSet, WatchedAttributes, and CooldownTracker
│   ├── Validation/            — ValidationResult
│   ├── Assets/                — ModAssetLoader, ModAssetRegistry
│   ├── Performance/           — DeferredWork, GameTimeScheduler, StatCoalescingEngine
│   ├── Random/                — WeightedRandom, WeightedTable, LootTable
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

API documentation also lives in the [`docs/`](docs) folder. See [docs/README.md](docs/README.md) for the complete category index.

---

## Authors

- **[DreadMob](https://gitlab.com/DreadMob)**

---

## License

This project is licensed under the [MIT License](LICENSE).

You are free to use, modify, and distribute this library, provided that the original copyright notice and license text remain intact in any copy or substantial modification.
