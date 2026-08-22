# ImageIconCache

`ArcanumLib.Gui.Icons.ImageIconCache` loads and caches icon `ImageSurface` instances from the Vintage Story asset pipeline. It supports PNG, WebP, and any other format that `GuiElement.getImageSurfaceFromAsset` can decode through Skia.

Because the cache uses Vintage Story's existing image loader, you can ship `.webp` icons directly in your mod's asset tree and draw them through the same API used for PNG. No manual PNG conversion and no changes to the texture atlas are needed.

The cache performs two important tasks:

1. Converts decoded images into a Cairo-compatible ARGB32 surface.
2. Applies alpha pre-multiplication and removes near-transparent noise pixels, which makes PNG/WebP icons render correctly with `Context.Paint`.

## Initialization

Call `Init` once during client startup (for example in a `ModSystem` that runs on the client side).

```csharp
using ArcanumLib.Gui.Icons;

public override void StartClientSide(ICoreClientAPI capi)
{
    ImageIconCache.Init(capi);
}
```

## Drawing an icon

```csharp
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.Theme;
using Cairo;

Context ctx = ...; // obtain a Cairo context from a GUI element

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

### Parameters of `TryDrawIcon`

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

### Return value

`TryDrawIcon` returns `true` if the icon was loaded and drawn. If the asset cannot be loaded, the failure is logged and `false` is returned. Missing assets are not retried for 60 seconds.

## Preloading

To avoid stalling the first render, preload an asset during GUI setup:

```csharp
ImageIconCache.Preload("mydomain:textures/icons/myicon.webp");
```

## Disposing the cache

When the client world is unloaded or the mod is disabled, release cached surfaces:

```csharp
ImageIconCache.Dispose();
```

## IconFit

```csharp
public enum IconFit
{
    None,    // draw inside the square without clipping
    Circle,  // clip to a circle
    Hexagon, // clip to a regular hexagon
    Diamond  // clip to a diamond (square rotated 45 degrees)
}
```
