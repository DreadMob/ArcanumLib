---
layout: default
title: Rendering helpers
nav_order: 22
---

# Rendering helpers

`ArcanumLib.Rendering` — small render fixes registered by ArcanumLib, usable from JSON, and helpers for items drawn into block meshes.

## See-through glass on entities (`ArcanumGlassShape`)

The vanilla entity renderer draws a whole entity in one opaque pass: a half-transparent pixel blends but still writes depth, so everything drawn later behind the pane (cab, passengers, other entities, water) disappears. `GlassEntityShapeRenderer` splits shape elements marked `"renderPass": 3` (Transparent) into their own mesh and draws it after the world's transparent pass — blended, without depth writes, back faces culled. Everything else renders exactly as with the vanilla `Shape` renderer.

```json
"client": { "renderer": "ArcanumGlassShape", "shape": { "base": "mymod:entity/carriage" } }
```

```json
{ "name": "window", "from": [...], "to": [...], "renderPass": 3, "faces": { ... } }
```

Entities only: chunk-meshed blocks already sort `renderPass 3` faces into the transparent pool. `GlassEntityShapeRenderer.Split(mesh)` returns the solid and glass parts of a mesh for custom renderers.

Keep the other elements free of half-transparent pixels — they still render in the opaque pass.

## Back-face culling for held items (`ArcanumCullFaces`)

```json
"behaviors": [ { "name": "ArcanumCullFaces" } ]
```

Turns on back-face culling when the item is held or lies on the ground, so an outline shell shows without its inside faces. GUI rendering is unchanged. ArcanumLib applies the same to such items in ground storage and on shelves, display cases and other display blocks (their no-cull chunk faces move to the culled opaque pass; glass and other passes are kept).

## Items in a block entity's chunk mesh (`ChunkItemMeshes`, `BlockAtlasItemSource`)

A block entity that adds an item to its chunk mesh in `OnTesselation` (a part in a basket, a kit on a turntable, a vessel under a tap) must use block-atlas textures. `TesselateItem(item, out mesh)` points into the item atlas: the chunk tesselator then throws RetryTesselation forever (`textureid N did not yet match an atlas pool` floods client-debug.log) and everything placed in that chunk afterwards stays invisible until relog.

```csharp
using ArcanumLib.Rendering;

// main thread, when the stack changes:
_partMesh = ChunkItemMeshes.Get(capi, slot.Itemstack, Pos);   // a copy: scale/move it freely
_partMesh?.Translate(0, 0.5f, 0);

public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tess)
{
    if (_partMesh != null) mesher.AddMeshData(_partMesh);
    return base.OnTesselation(mesher, tess);
}
```

`ChunkItemMeshes.Get` asks a collectible that draws itself (`IContainedMeshSource`) with the block atlas, gives a block its default block mesh and tesselates any other item with `BlockAtlasItemSource`. Meshes are cached per client by `ChunkItemMeshes.Key(stack)` (code, plus the collectible's own mesh cache key when it draws itself) and handed out as copies; `Clear(capi)` forgets them.

`BlockAtlasItemSource` is the texture source itself (the item's texture by code, then `all`, then the shape's texture, then the code as a path — the vanilla `BlockEntityDisplay` lookup); `BlockAtlasItemSource.Mesh(capi, item)` is an uncached mesh, for an `IContainedMeshSource.GenMesh` of your own.

Main thread only: inserting a texture into the atlas off-thread fails. Build the meshes when the stack changes, never inside `OnTesselation`.
