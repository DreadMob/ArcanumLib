# Arcanum Lib

**A shared client/server utility library for Vintage Story**

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Game Version](https://img.shields.io/badge/Vintage%20Story-1.22.1+-green.svg)](https://www.vintagestory.at/)
[![Version](https://img.shields.io/badge/Version-1.0.0-orange.svg)](resources/modinfo.json)
[![C#](https://img.shields.io/badge/C%23-.NET%2010-purple.svg)]()
[![Alegacy Quest Framework](https://img.shields.io/badge/Used%20by-Alegacy%20Quest%20Framework-2563eb?style=for-the-badge&logo=gitlab)](https://gitlab.com/DreadMob/Alegacy-Quest-Framework)

---

## About

Arcanum Lib is a shared library mod for [Vintage Story](https://www.vintagestory.at/). It provides reusable GUI, rendering, and color helpers that can be consumed by other mods without each mod duplicating the same infrastructure.

One of its main goals is making compressed icon assets practical: you can use `.webp`, `.png`, `.jpg` and other Skia-decoded formats in the asset tree and draw them through the normal GUI pipeline. No manual conversion and no texture-atlas changes are required, because `ImageIconCache` routes the load through Vintage Story's built-in SkiaSharp image loader. AVIF and JPEG XL are not included in the Vintage Story `libSkiaSharp.dll`.

The library is currently used by the **[Alegacy Quest Framework](https://gitlab.com/DreadMob/Alegacy-Quest-Framework)**.

---

## Key Features

| Module | Description |
|--------|-------------|
| **ImageIconCache** | Load, cache and draw icon image surfaces with circle, hexagon, and diamond clipping plus optional tinting. |
| **RGBA** | Lightweight Cairo-friendly color struct with hex parsing, ARGB conversion, lerping, and alpha overrides. |
| **ArcanumGuiTheme** | Shared colour palette, radii, spacing and Cairo drawing helpers for consistent GUI styling. |
| **ArcanumComposer / ArcanumList<T>** | Fluent GUI builder and reusable scrollable list with selection and scrollbar. |
| **ArcanumFont / ArcanumLayout** | Font presets and vertical/horizontal layout helpers to reduce manual `ElementBounds` math. |
| **ArcanumCard / ArcanumIcon / ArcanumButton / ArcanumScrollbar / ArcanumList<T>** | Ready-to-use, themed Vintage Story `GuiElement` controls. |
| **ModeIconBuilder** | Factory for `SkillItem` tool-mode icons (in-game icon, letter, or live `ItemStack` rendering). |
| **ShapeCloner** | Deep-clones Vintage Story `Shape` objects (textures, faces, attachment points) for safe mutation. |
| **TimedCache<TKey, TValue>** | Thread-safe cache with TTL eviction and optional size limit. |
| **TagSetExtensions** | Set operations and readable aliases for `Vintagestory.API.Datastructures.TagSet`. |
| **ValidationResult** | Immutable result object that accumulates errors and warnings from validation pipelines. |
| **Pretty** | Converts raw asset codes into readable, title-cased display strings and sanitizes names. |
| **CollectibleNameResolver** | Resolves item, block and entity display names, wildcard matches and icon codes with caching. |
| **Wildcard** | Fast case-insensitive wildcard matching for asset codes. |
| **ModAssetLoader** | Loads and merges typed JSON assets from all loaded mods, supporting multi-pack content definitions. |
| **ModAssetRegistry** | Builds validated, keyed, source-tracked registries from `ModAssetLoader` output. |
| **WeightedRandom / WeightedTable** | Weighted random picks and reusable weighted tables with merge strategies. |
| **DeferredWork** | Game-tick scheduler for one-shot, coalesced and end-of-tick work. |

More cross-mod utilities will be added as they are extracted from the consuming mods.

---

## Architecture

```
ArcanumLib/
├── ArcanumLibModSystem.cs    — Vintage Story entry point
├── src/
│   ├── Gui/
│   │   ├── Controls/          — ArcanumButton, ArcanumCard, ArcanumIcon, ArcanumScrollbar, ArcanumDialogBackground, ArcanumList<T>
│   │   ├── Dialogs/           — ArcanumGuiDialog base
│   │   ├── Icons/             — ImageIconCache and IconFit
│   │   ├── Layout/            — ArcanumLayout helpers
│   │   ├── ModeIconBuilder.cs — tool-mode icon factory
│   │   └── Theme/             — ArcanumGuiTheme, ArcanumFont, RGBA
│   ├── Geometry/              — ShapeCloner
│   ├── Caching/               — SimpleLRUCache and TimedCache<TKey, TValue>
│   ├── Data/                  — TagSetExtensions, WatchedAttributesExtensions
│   ├── Validation/            — ValidationResult
│   ├── Assets/                — ModAssetLoader, ModAssetRegistry
│   ├── Performance/           — DeferredWork, StatCoalescingEngine
│   ├── Random/                — WeightedRandom, WeightedTable
│   ├── Text/                  — Pretty, Wildcard
│   └── Helpers/               — CollectibleNameResolver
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

API documentation lives in the [`docs/`](docs) folder:

- [ImageIconCache](docs/ImageIconCache.md)
- [RGBA](docs/RGBA.md)
- [Arcanum GUI Toolkit](docs/ArcanumGui.md)
- [ModAssetLoader](docs/ModAssetLoader.md)
- [WeightedRandom](docs/WeightedRandom.md)
- [WatchedAttributes](docs/WatchedAttributes.md)
- [EventScope](docs/EventScope.md)
- [Inventory / ItemStack helpers](docs/InventoryHelpers.md)
- [TypedNetworkChannel](docs/TypedNetworkChannel.md)

---

## Authors

- **[DreadMob](https://gitlab.com/DreadMob)** — Lead developer

---

## License

This project is licensed under the [MIT License](LICENSE).

You are free to use, modify, and distribute this library, provided that the original copyright notice and license text remain intact in any copy or substantial modification.
