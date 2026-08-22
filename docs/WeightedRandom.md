---
layout: default
title: WeightedRandom
nav_order: 100
has_children: true
---

# WeightedRandom

## What is it for?

`ArcanumLib.Randomization` provides reusable weighted random selection. It removes the common boilerplate of summing weights, rolling, and walking a cumulative list.

## When to use it

- You need to pick one item from a list where each entry has a different chance.
- You want to roll multiple distinct winners without replacement.
- You need to display percentage chances in UI tooltips or info text.
- You are rolling from the same set repeatedly and want a cached table for performance.

## Quick example

```csharp
var chosen = WeightedRandom.Pick(
    eligibleMobs,
    mob => mob.weight,
    sapi.World.Rand);
```

`chosen` is `default` if the list is empty, or the first item if all weights are zero.

## API overview

### One-off picks

| Method | Returns | Description |
|--------|---------|-------------|
| `WeightedRandom.Pick(items, weightSelector, random)` | `T` | Returns the winning item, `default` when the list is empty, or the first item when all weights are zero. |
| `WeightedRandom.PickOrDefault(items, weightSelector, random)` | `T?` | Returns `default(T)` when the list is empty or all weights are zero. |
| `WeightedRandom.PickDistinct(items, weightSelector, random, count)` | `IReadOnlyList<T>` | Returns `count` distinct winners without replacement. |
| `WeightedRandom.GetPercentages(items, weightSelector)` | percentages | Returns the percentage share for each item, useful for UI tooltips and info text. |

### Reusable tables

For repeated rolls, use `WeightedTable<T>`:

```csharp
var table = new WeightedTable<ModifierDefinition>();
foreach (var def in defs)
    table.Add(def, Math.Max(def.Weight, 1));

var def = table.PickOrDefault(sapi.World.Rand);
```

`WeightedTable<T>` tracks the total weight and updates it when entries are added or cleared. It supports `Add`, `AddRange`, `Clear`, `Pick`, and `PickOrDefault`.

## Notes

- Negative weights are treated as zero.
- If all weights are zero, `Pick` returns the first item (or `default` for `PickOrDefault`).
- Uses `Random.NextDouble()` for floating-point rolls, matching the Vintage Story `Rand` pattern.