---
layout: default
title: Holograms
nav_order: 16
parent: Arcanum GUI Toolkit
---

# Holograms

## What is it for?

`ArcanumLib.Hologram` provides a reusable way to draw floating text labels above blocks in the world. It takes care of Cairo texture generation, 3D to 2D projection, distance scaling, and caching so consumer mods only need to supply the text and position.

## When to use it

- You need a floating label above a `BlockEntity` (block name, status, progress, owner, etc.).
- You want world-space text that scales by distance and handles occlusion.
- You want one `IRenderer` per block or one `IRenderer` that scans an area.

## Core types

| Type | Purpose |
|------|---------|
| `IHologramTextSource` | Provides text, color, version, position, range, and visibility for one hologram. |
| `HologramTexture` | Wraps a `LoadedTexture` with the version it was generated from. |
| `HologramTextureOptions` | Font, layout, padding, colors, and an optional per-line renderer. |
| `HologramTextureGenerator` | Generates a `HologramTexture` from multi-line text. |
| `IHologramLineRenderer` | Override how a single line is drawn. |
| `SingleHologramRenderer` | One `IRenderer` for a single `IHologramTextSource`. |
| `AreaHologramRenderer` | One `IRenderer` that scans nearby chunks for sources and renders them. |
| `HologramRenderUtils` | Projection and occlusion helpers. |

## Implementing a text source

The block entity (or any object with a fixed position) can implement `IHologramTextSource`:

```csharp
using ArcanumLib.Hologram;
using Vintagestory.API.Common;

public class MyHologramBlockEntity : BlockEntity, IHologramTextSource
{
    public BlockPos Position => Pos;

    public string? GetHologramText() => $"Level {Tier}\nOwner: {Owner}";

    public double[]? GetHologramColor() => new[] { 0.95, 0.80, 0.25, 1.0 };

    // Bump this when text, color, or visibility changes.
    public long GetHologramVersion() => _version;

    public float GetHologramRange() => 32f;

    public float GetHologramHeightOffset() => 2.5f;

    public bool IsHologramVisible() => _visible;

    public bool IsHologramVisibleThroughWalls() => false;
}
```

Version-based invalidation is the recommended way to refresh text: the renderer regenerates the texture only when the source version changes, not every frame.

## Single block hologram

For one block entity, create a `SingleHologramRenderer` in the block entity client initialisation and dispose it on teardown:

```csharp
var source = this; // the block entity implements IHologramTextSource
var options = new HologramTextureOptions
{
    FontSize = 24,
    LineWidth = 400,
    PaddingX = 12,
    PaddingTop = 10,
    PaddingBottom = 10
};

_hologramRenderer = new SingleHologramRenderer(capi, source, options, renderKey: "mydomain:block-holo");
```

The renderer:

- registers itself with `EnumRenderStage.Ortho`
- returns `RenderRange` from the source
- checks distance each frame
- regenerates the texture when the version or text changes
- projects the world position and calls `Render2DTexture`

Call `_hologramRenderer.UpdateSettings()` when external settings change and you want to force a re-render.

## Area hologram

For many blocks spread across an area (altars, control points, etc.), use `AreaHologramRenderer`:

```csharp
_hologramRenderer = new AreaHologramRenderer(
    capi,
    static be => be as IHologramTextSource,
    new HologramTextureOptions
    {
        FontSize = 18,
        LineWidth = 520,
        DrawBackground = false,
        TextColor = new RGBA(0.95, 0.80, 0.25, 1.0)
    },
    range: 48,
    yRange: 8,
    maxSources: 20,
    renderKey: "mydomain:area-holo");
```

`AreaHologramRenderer`:

- scans nearby chunks for `BlockEntity` instances
- uses the factory to obtain `IHologramTextSource` from each one
- caches textures and reuses them while the source version is unchanged
- renders each visible, non-occluded source with distance scaling
- limits the number of active sources with `maxSources`

## Per-line custom drawing

`HologramTextureOptions.RenderLine` is an optional `IHologramLineRenderer` that lets a mod override how a single line is drawn. Return `true` to skip the default centered text.

```csharp
using ArcanumLib.Hologram;
using Cairo;

public class MyLineRenderer : IHologramLineRenderer
{
    public bool RenderLine(Context ctx, int lineIndex, string line, double x, double y, double lineHeight)
    {
        if (line.StartsWith(">"))
        {
            ctx.SetSourceRGBA(0.95, 0.85, 0.45, 0.95);
            ctx.MoveTo(x, y);
            ctx.ShowText(line);
            return true;
        }
        return false;
    }
}

options.RenderLine = new MyLineRenderer();
```

Use this for progress bars, separators, headers, or different colors per line without re-implementing the texture generator.

## Dispose

Both `SingleHologramRenderer` and `AreaHologramRenderer` implement `IDisposable`. Dispose them when the block entity or client system is disposed to release the `LoadedTexture` and unregister the `IRenderer`.
