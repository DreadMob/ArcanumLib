---
layout: default
title: PositionUtils
nav_order: 30
parent: Randomization & Geometry
---

# PositionUtils

## What is it for?

`PositionUtils` provides helpers for computing random positions, ground-level searches, distance checks, and direction calculations around entities and world coordinates.

## When to use it

- You need a random position on the horizontal plane around a center point.
- You need a uniformly distributed random point inside a circle, cone, or rectangular strip.
- You need to find a ground-level or passable-floor position near an entity or anchor.
- You want horizontal (XZ-plane) distance checks that ignore the Y axis.
- You need normalized direction vectors or angles between positions.

## Quick examples

### Random horizontal offset

```csharp
using ArcanumLib.Geometry;

// Random position between 2 and 5 blocks from center
var offset = PositionUtils.GetRandomHorizontalOffset(center, minDist: 2, maxDist: 5, rand);
```

### Random point in shape

```csharp
// Uniform point inside a circle (sqrt sampling, no center clustering)
var p = PositionUtils.GetRandomPointInCircle(center, radius: 4, rand);

// Point inside a cone (sector) — 30° half-angle, pointing east
var p = PositionUtils.GetRandomPointInCone(apex, new Vec3d(1, 0, 0), radius: 5, halfAngleDegrees: 30, rand);

// Point inside a rectangular strip (line) — 8 long, 2 wide
var p = PositionUtils.GetRandomPointInLine(origin, direction, length: 8, width: 2, rand);
```

### Ground-level search

```csharp
// Random ground position around an entity
if (PositionUtils.TryGetRandomGroundPositionAround(entity, 3, 8, blockAccessor, rand, out var groundPos))
{
    // Use groundPos
}

// Find a passable floor near an anchor Y (feet+head passable, ground solid)
if (PositionUtils.TryFindLocalFloor(blockAccessor, pos, anchorY: 70, maxDelta: 3, out int feetY))
{
    // Use feetY
}
```

### Horizontal distance

```csharp
double dist = PositionUtils.HorizontalDistanceTo(player.Pos.XYZ, boss.Pos.XYZ);

if (PositionUtils.IsWithinHorizontalRange(player.Entity, boss.Pos.XYZ, range: 12))
{
    // Player is within 12 blocks (ignoring Y)
}
```

### Direction & angle

```csharp
Vec3f dir = PositionUtils.GetDirectionTo(from, to);   // normalized XZ direction
double angle = PositionUtils.GetAngleTo(from, to);    // atan2 radians
Vec3d mid = PositionUtils.LerpPosition(a, b, 0.5);    // midpoint
```

## API overview

### Random horizontal offsets

| Method | Description |
|--------|-------------|
| `GetRandomHorizontalOffset(Vec3d, min, max, rand)` | Random position on the horizontal plane at the same Y as center. |
| `GetRandomHorizontalOffset(Entity, min, max, rand)` | Same, using an entity's position. |

### Random point in shapes

| Method | Description |
|--------|-------------|
| `GetRandomPointInCircle(center, radius, rand)` | Uniformly distributed point inside a circle (sqrt sampling). |
| `GetRandomPointInCone(apex, direction, radius, halfAngle, rand)` | Point inside a circular sector. Accepts `Vec3d` or `Vec3f` direction. |
| `GetRandomPointInLine(origin, direction, length, width, rand)` | Point inside a rectangular strip along a direction. Accepts `Vec3d` or `Vec3f` direction. |

### Ground-level search

| Method | Description |
|--------|-------------|
| `TryGetRandomGroundPositionAround(Entity, min, max, ba, rand, out pos)` | Random ground position using terrain height. Returns false if terrain is at or below 0. |
| `TryFindLocalFloor(ba, pos, anchorY, maxDelta, out feetY)` | Searches a vertical column for a passable floor (feet+head passable, ground solid). |
| `IsPassable(block, ba, pos)` | Returns true if a block has no collision boxes. |

### Horizontal distance

| Method | Description |
|--------|-------------|
| `HorizontalDistanceTo(Vec3d, Vec3d)` | XZ-plane distance, ignoring Y. |
| `HorizontalDistanceTo(Entity, Entity)` | Same, for two entities. |
| `HorizontalSquareDistanceTo(Vec3d, Vec3d)` | Squared XZ distance (faster for range checks). |
| `IsWithinHorizontalRange(Vec3d, Vec3d, range)` | True if within range on the XZ plane. |
| `IsWithinHorizontalRange(Entity, Vec3d, range)` | Same, for an entity vs a position. |

### Direction & angle

| Method | Description |
|--------|-------------|
| `GetDirectionTo(from, to)` | Normalized horizontal direction (Vec3f, Y = 0). |
| `GetAngleTo(from, to)` | Horizontal atan2 angle in radians. |
| `LerpPosition(a, b, t)` | Linear interpolation between two positions. |

## Notes

- All methods are null-safe: they return defaults (zero vector, `double.MaxValue`, false) when inputs are null.
- Shape sampling uses `Math.Sqrt(rand.NextDouble())` for uniform area distribution.
- `TryFindLocalFloor` scans from `anchorY + maxDelta` downward to `anchorY - maxDelta`.
