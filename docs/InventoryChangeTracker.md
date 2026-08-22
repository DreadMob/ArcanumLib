---
layout: default
title: InventoryChangeTracker
---

# InventoryChangeTracker

Tracks inventory changes for a player and reports whether a dependent recomputation should run.

## What is it for?

Some systems (e.g. wearable stat bonuses, equipment-based effects) need to recalculate when a player's inventory changes. Polling the inventory every tick is expensive. `InventoryChangeTracker` fingerprints the relevant slots at a configurable interval and reports only when something actually changed.

## When to use it

- Recalculate stat bonuses when wearable items change.
- Rebuild cached data that depends on inventory contents.
- Any system that needs to react to inventory changes without per-tick polling.

## Quick example

```csharp
using ArcanumLib.Inventory;

private readonly InventoryChangeTracker _tracker = new(api, "character", checkIntervalMs: 500);

public override void OnGameTick(float dt)
{
    if (!_tracker.ShouldRecalculate(player.Entity))
        return;

    // Expensive recalculation only runs when the inventory actually changed.
    RecalculateWearableStats(player);
}
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `ShouldRecalculate(player)` | `bool` | True if the inventory has changed since the last check. Throttled by `checkIntervalMs`. |
| `Invalidate(entityId)` | `void` | Forces a recalculation on the next `ShouldRecalculate` call. |
| `Clear()` | `void` | Clears all cached fingerprints and throttle timestamps. |

## Constructor parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `api` | required | API instance for time access and logging. |
| `inventoryCode` | `"character"` | Inventory class to watch. |
| `checkIntervalMs` | `500` | Minimum time between checks for one player. |
| `stackHash` | `InventoryFingerprint.GetStableStackHash` | Custom hash function for item stacks. |
| `slotFilter` | wearable slots | Predicate for which slots to include. |

## Notes

- Uses `InventoryFingerprint.GetStableStackHash` by default for stable, jitter-free hashing.
- The default slot filter includes only slots with `IWearable` collectibles.
- Fingerprinting is throttled per player to avoid expensive work on every tick.
- Call `Invalidate` when you know the inventory changed externally and want to force a recheck.
