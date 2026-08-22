# ShapeCloner

Deep-clones Vintage Story `Shape` objects so they can be safely modified without
sharing mutable state with the cached original. This is useful when the engine
reuses a single `Shape` for many renderers (wearable attachments, transmog, boss
model swaps).

## Why not `Shape.Clone()`?

`Shape.Clone()` creates new `ShapeElement` and `Animation` instances, but it
keeps shallow references for `Textures`, `TextureSizes`, `FacesResolved` and
`AttachmentPoints`. Mutating those on one clone leaks back into the cached shape.

`ShapeCloner.DeepClone` makes independent copies of:

- `Shape.Textures` (`AssetLocation` instances)
- `Shape.TextureSizes` (per-texture `int[]`)
- `ShapeElement.FacesResolved` (each `ShapeElementFace` and its `Uv`, `WindMode`, `WindData` arrays)
- `ShapeElement.AttachmentPoints` (each `AttachmentPoint`, with parent reset to the cloned element)

## Usage

```csharp
using ArcanumLib.Geometry;
using Vintagestory.API.Common;

Shape source = Shape.TryGet(api, "path/to/shape.json");
Shape clone = ShapeCloner.DeepClone(source);

// Safe to mutate without affecting the cached source.
clone.Textures["main"] = new AssetLocation("mydomain", "texture.png");
```

## API

```csharp
public static class ShapeCloner
{
    public static Shape? DeepClone(Shape? source);
    public static Shape? LoadAndClone(ICoreAPI api, AssetLocation location);
    public static Shape? LoadAndClone(ICoreAPI api, string path);
}
```
