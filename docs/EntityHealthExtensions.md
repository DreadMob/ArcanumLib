---
layout: default
title: EntityHealthExtensions
---

# EntityHealthExtensions

## What is it for?

`ArcanumLib.Common.EntityHealthExtensions` provides health queries and scaling helpers that work through `Entity.WatchedAttributes` or the underlying `EntityBehaviorHealth` tree, so you do not need to manually inspect the health attribute tree.

## When to use it

- Get an entity's current health as a fraction of max health.
- Read current and max health values safely.
- Scale an entity's max and current health by a multiplier for difficulty or temporary effects.

## Quick example

```csharp
using ArcanumLib.Common;

if (entity.TryGetHealthFraction(out float frac))
{
    // frac is 0.0..1.0
}

entity.ScaleHealth(1.5f); // +50% max/current health
```

## API overview

| Method | Returns | Description |
|---|---|---|
| `TryGetHealthFraction(out float fraction)` | `bool` | Returns `true` and the health fraction `0.0..1.0` when the health tree is present. |
| `TryGetHealth(out ITreeAttribute? healthTree, out float currentHealth, out float maxHealth)` | `bool` | Returns `true` and the health tree plus raw current/max values. |
| `ScaleHealth(float mult)` | `bool` | Multiplies max health and sets current health to the new max; returns `true` on success. |

## Notes

- Health values are read from the `health` tree in `WatchedAttributes`.
- `maxhealth` is used when present; otherwise `basemaxhealth` is used as a fallback.
- `ScaleHealth` leaves both `maxhealth` and `currenthealth` at the new value.
