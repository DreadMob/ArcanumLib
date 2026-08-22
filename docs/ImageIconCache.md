---
layout: default
title: ImageIconCache
---

# ImageIconCache

## What is it for?

`ImageIconCache` loads and caches icon `ImageSurface` instances from the Vintage Story asset pipeline. It supports PNG, JPEG, GIF, BMP, ICO, WBMP, WebP, HEIF, DNG, KTX, PKM and ASTC through `SkiaSharp.SKCodec`. It converts decoded images into a Cairo-compatible ARGB32 surface, applies alpha pre-multiplication, and removes near-transparent noise pixels so icons render correctly with `Context.Paint`.

## When to use it

- Display PNG, JPEG, GIF, BMP, ICO, WebP, HEIF, and other `SKCodec` formats in a GUI without converting them to the texture atlas.
- Clip an icon to a circle, hexagon, or diamond.
- Avoid first-render stalls by preloading frequently shown icons.
- Safely release cached surfaces when the client world is unloaded.

## Quick example

```csharp
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.Theme;
using Cairo;

public override void StartClientSide(ICoreClientAPI capi)
{
    ImageIconCache.Init(capi);
}

// In a GUI element's draw method:
Context ctx = ...;
ImageIconCache.TryDrawIcon(
    ctx: ctx,
    assetPath: "mydomain:textures/icons/myicon.webp",
    cx: 50.0,
    cy: 50.0,
    radius: 32.0,
    color: new RGBA(1.0, 1.0, 1.0, 1.0),
    fit: IconFit.Circle,
    tint: false);
```

## API overview

Call `Init` once during client startup:

```csharp
public override void StartClientSide(ICoreClientAPI capi)
{
    ImageIconCache.Init(capi);
}
```

Draw an icon with `TryDrawIcon`:

```csharp
Context ctx = ...; // Cairo context from a GUI element

bool drawn = ImageIconCache.TryDrawIcon(
    ctx: ctx,
    assetPath: "mydomain:textures/icons/myicon.webp",
    cx: 50.0,
    cy: 50.0,
    radius: 32.0,
    color: new RGBA(1.0, 1.0, 1.0, 1.0),
    fit: IconFit.Circle,
    tint: false);
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `ctx` | `Cairo.Context` | The Cairo context to draw into. |
| `assetPath` | `string` | Vintage Story asset path, e.g. `domain:path/to/icon`. |
| `cx` | `double` | Center X of the icon area. |
| `cy` | `double` | Center Y of the icon area. |
| `radius` | `double` | Half-size of the square area the icon fits into. |
| `color` | `RGBA` | Tint or overlay color. |
| `fit` | `IconFit` | `None`, `Circle`, `Hexagon` or `Diamond` clipping shape. |
| `tint` | `bool` | If `true`, the image is masked with the given color. If `false`, the image is painted directly and alpha is applied. |

`TryDrawIcon` returns `true` if the icon was loaded and drawn. If the asset cannot be loaded, the failure is logged and `false` is returned. Missing assets are not retried for 60 seconds.

Preload an asset to avoid first-render stalls:

```csharp
ImageIconCache.Preload("mydomain:textures/icons/myicon.webp");
```

Release cached surfaces when the client world is unloaded:

```csharp
ImageIconCache.Dispose();
```

### `IconFit`

```csharp
public enum IconFit
{
    None,    // draw inside the square without clipping
    Circle,  // clip to a circle
    Hexagon, // clip to a regular hexagon
    Diamond  // clip to a diamond (square rotated 45 degrees)
}
```

## Notes

- `ImageIconCache` uses `SkiaSharp.SKCodec` directly, so you can ship `.png`, `.jpg`, `.webp` and the other listed formats directly in your mod's asset tree and draw them through the same API. No manual conversion or texture atlas changes are needed.
- Call `Init` once during client startup and `Dispose` when the client world is unloaded.
