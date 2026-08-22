---
layout: default
title: WeightedRandom
---

# WeightedRandom

`ArcanumLib.Randomization` provides reusable weighted random selection. It removes the common boilerplate of summing weights, rolling, and walking a cumulative list.

## Quick example

```csharp
var chosen = WeightedRandom.Pick(
    eligibleMobs,
    mob => mob.weight,
    sapi.World.Rand);
```

`chosen` is `null` if the list is empty or all weights are zero.

## WeightedTable

For repeated rolls, use `WeightedTable<T>`:

```csharp
var table = new WeightedTable<ModifierDefinition>();
foreach (var def in defs)
    table.Add(def, Math.Max(def.Weight, 1));

var def = table.PickOrDefault(sapi.World.Rand);
```

`WeightedTable` tracks the total weight and updates it when entries are added or cleared.

## Methods

- `WeightedRandom.Pick(items, weightSelector, random)` — one pick.
- `WeightedRandom.PickOrDefault(...)` — returns `default(T)` when nothing is pickable.
- `WeightedRandom.PickDistinct(items, weightSelector, random, count)` — `count` distinct winners without replacement.
- `WeightedRandom.GetPercentages(items, weightSelector)` — percentage share for each item, useful for UI tooltips and info text.
- `WeightedTable<T>` — reusable table supporting `Add`, `AddRange`, `Clear`, `Pick`, `PickOrDefault`.

## Notes

- Negative weights are treated as zero.
- If all weights are zero, `Pick` returns the first item (or `default` for `PickOrDefault`).
- Uses `Random.NextDouble()` for floating-point rolls, matching the Vintage Story `Rand` pattern.
