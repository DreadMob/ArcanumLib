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

One of its main goals is making compressed icon assets practical: you can use `.webp` files in the asset tree and draw them through the normal GUI pipeline. No manual PNG conversion and no texture-atlas changes are required, because `ImageIconCache` routes the load through Vintage Story's built-in SkiaSharp image loader.

The library is currently used by the **[Alegacy Quest Framework](https://gitlab.com/DreadMob/Alegacy-Quest-Framework)**.

---

## Key Features

| Module | Description |
|--------|-------------|
| **ImageIconCache** | Load, cache and draw icon image surfaces with circle, hexagon, and diamond clipping plus optional tinting. |
| **RGBA** | Lightweight Cairo-friendly color struct with hex parsing, ARGB conversion, lerping, and alpha overrides. |
| **ArcanumGuiTheme** | Shared colour palette, radii, spacing and Cairo drawing helpers for consistent GUI styling. |
| **ArcanumFont / ArcanumLayout** | Font presets and vertical/horizontal layout helpers to reduce manual `ElementBounds` math. |
| **ArcanumCard / ArcanumIcon / ArcanumButton / ArcanumScrollbar** | Ready-to-use, themed Vintage Story `GuiElement` controls. |
| **Pretty** | Converts raw asset codes into readable, title-cased display strings and sanitizes names. |
| **CollectibleNameResolver** | Resolves item, block and entity display names, wildcard matches and icon codes with caching. |
| **Wildcard** | Fast case-insensitive wildcard matching for asset codes. |

More cross-mod utilities will be added as they are extracted from the consuming mods.

---

## Architecture

```
ArcanumLib/
├── ArcanumLibModSystem.cs    — Vintage Story entry point
├── src/
│   ├── Gui/
│   │   ├── Controls/          — ArcanumButton, ArcanumCard, ArcanumIcon, ArcanumScrollbar, ArcanumDialogBackground
│   │   ├── Dialogs/           — ArcanumGuiDialog base
│   │   ├── Icons/             — ImageIconCache and IconFit
│   │   ├── Layout/            — ArcanumLayout helpers
│   │   └── Theme/             — ArcanumGuiTheme, ArcanumFont, RGBA
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

---

## Authors

- **[DreadMob](https://gitlab.com/DreadMob)** — Lead developer

---

## License

This project is licensed under the [MIT License](LICENSE).

You are free to use, modify, and distribute this library, provided that the original copyright notice and license text remain intact in any copy or substantial modification.
