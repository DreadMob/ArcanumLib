---
layout: default
title: ShapeCloner
parent: "WeightedRandom"
nav_order: 2
---

# ShapeCloner

## What is it for?

Deep-clones Vintage Story `Shape` objects so they can be safely modified without sharing mutable state with the cached original. This is useful when the engine reuses a single `Shape` for many renderers, such as wearable attachments, transmog, or unique creature model variants.

## When to use it

- You need to modify a `Shape` loaded from an asset without corrupting the shared cache.
- Multiple renderers or entities share the same `Shape` and need independent texture or mesh changes.
- You want to create per-instance visual variations at runtime.

## Quick example

```csharp
using ArcanumLib.Geometry;
using Vintagestory.API.Common;

Shape source = Shape.TryGet(api, "path/to/shape.json");
Shape clone = ShapeCloner.DeepClone(source);

// Safe to mutate without affecting the cached source.
clone.Textures["main"] = new AssetLocation("mydomain", "texture.png");
```

## API overview

```csharp
public static class ShapeCloner
{
    public static Shape? DeepClone(Shape? source);
    public static Shape? LoadAndClone(ICoreAPI api, AssetLocation location);
    public static Shape? LoadAndClone(ICoreAPI api, string path);
}
```

`ShapeCloner.DeepClone` makes independent copies of mutable `Shape` sub-objects that `Shape.Clone()` only shallow-copies:

| Data | Deep-cloned? |
|------|--------------|
| `Shape.Textures` (`AssetLocation` instances) | Yes |
| `Shape.TextureSizes` (per-texture `int[]`) | Yes |
| `ShapeElement.FacesResolved` (each `ShapeElementFace` and its `Uv`, `WindMode`, `WindData` arrays) | Yes |
| `ShapeElement.AttachmentPoints` (each `AttachmentPoint`, with parent reset to the cloned element) | Yes |

## Notes

- `Shape.Clone()` creates new `ShapeElement` and `Animation` instances, but it keeps shallow references for `Textures`, `TextureSizes`, `FacesResolved`, and `AttachmentPoints`. Mutating those on one clone leaks back into the cached shape.