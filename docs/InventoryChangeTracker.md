---
layout: default
title: InventoryChangeTracker
nav_order: 55
---

# InventoryChangeTracker

Tracks inventory changes for a player and reports whether a dependent recomputation should run. Fingerprinting is throttled and cached to avoid expensive work on every tick.

## What is it for?

- Recomputing derived stats (e.g., equipment bonuses) only when the inventory actually changes.
- Filtering which slots to include (wearable by default).
- Per-player throttle and invalidation on player disconnect.

## Quick example

```csharp
using ArcanumLib.Inventory;

var tracker = new InventoryChangeTracker(api, "character", 500);
// In a tick:
if (tracker.ShouldRecalculate(player))
{
    RecalculateGearStats(player);
}
```

## Notes

- `ShouldRecalculate` returns `false` while the per-player throttle window is active.
- Fingerprint uses a stable stack hash and slot count.
- Call `Dispose` on world unload to unsubscribe from `PlayerDisconnect`.
