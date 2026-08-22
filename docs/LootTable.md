---
layout: default
title: LootTable
---

# LootTable

JSON-friendly loot tables with tiers, weighted entries, and luck multipliers.

## What is it for?

`LootTable<T>` generalizes the weighted random pattern into a reusable loot table abstraction that can be loaded directly from JSON assets. Each entry has a value, a weight, and a tier. A luck multiplier shifts selection probability toward higher-tier entries.

## When to use it

- Roll rewards from a JSON-defined table with rarity tiers.
- Apply a luck stat that biases rolls toward better tiers.
- Roll multiple distinct rewards without replacement.
- Serialize loot tables to and from JSON for asset-driven content.

## Quick example

```csharp
using ArcanumLib.Randomization;

var table = new LootTable<string>();
table.Add("common", weight: 100, tier: 0);
table.Add("rare",   weight: 10,  tier: 1);
table.Add("legend", weight: 1,   tier: 2);

table.LuckMultiplier = 0.5f; // bias toward higher tiers

string reward = table.Roll(sapi.World.Rand);
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `Add(value, weight, tier)` | `LootTable<T>` | Adds an entry. Fluid. |
| `Roll(Random)` | `T` | Rolls one entry. Returns `default` if empty. |
| `RollMany(Random, count)` | `IReadOnlyList<T>` | Rolls `count` entries with replacement. |
| `RollDistinct(Random, count)` | `IReadOnlyList<T>` | Rolls `count` distinct entries without replacement. |
| `FromJson(string)` | `LootTable<T>` | Deserializes from JSON. |
| `ToJson()` | `string` | Serializes to JSON. |

### Luck multiplier

`LuckMultiplier` shifts the effective weight of each entry:

```
effectiveWeight = weight * (1 + luck * tier)
```

A luck of `0` means no bias. A luck of `1.0` doubles the weight of tier-1 entries, triples tier-2, and so on.

## Notes

- Entries with weight `<= 0` are treated as weight `0` and never selected unless all weights are zero.
- If all weights are zero, `Roll` returns `default(T)`.
- `RollDistinct` returns at most `min(count, entries)` entries.
