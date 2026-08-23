---
layout: default
title: BlockEntitySearchUtils
nav_order: 31
parent: Randomization & Geometry
---

# BlockEntitySearchUtils

## What is it for?

`BlockEntitySearchUtils` provides a chunk-based helper for counting block entities matching a predicate within a cuboid region.

## When to use it

- You need to count specific block entities (e.g. machines, chests, spawners) within a region.
- You want chunk-level iteration rather than scanning every block position.

## Quick example

```csharp
using ArcanumLib.Geometry;

int count = BlockEntitySearchUtils.CountBlockEntities(
    pos: new Vec3i(centerX, centerY, centerZ),
    radiusX: 16, radiusY: 4, radiusZ: 16,
    blockAccessor: world.BlockAccessor,
    matcher: be => be is BlockEntityChest);
```

## API overview

| Method | Description |
|--------|-------------|
| `CountBlockEntities(Vec3i, radiusX, radiusY, radiusZ, blockAccessor, matcher)` | Counts block entities matching the predicate within the given radii. Iterates chunk-by-chunk. |

## Notes

- Iteration step is `GlobalConstants.ChunkSize` for efficiency.
- The matcher receives each `BlockEntity` in the scanned chunks.
