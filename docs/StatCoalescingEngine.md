---
layout: default
title: StatCoalescingEngine
parent: "DeferredWork"
nav_order: 1
---

# StatCoalescingEngine

Coalesces multiple `EntityStats.Set` calls into a single network sync.

## What is it for?

When stats change rapidly (equipment swaps, buff stacks, debuff removals), each `Stats.Set` call triggers a network packet. `StatCoalescingEngine` batches stat updates per entity within a configurable time window and applies them in one flush, reducing packet spam.

## When to use it

- Equipment changes that modify multiple stats at once.
- Buff/debuff systems that stack or refresh rapidly.
- Any code path that calls `EntityStats.Set` more than once per tick for the same entity.

## Quick example

```csharp
using ArcanumLib.Core;
using ArcanumLib.Performance;

var engine = ArcanumServices.Get<IStatCoalescingEngine>()!;

// Queue a stat update (applied after the coalesce window).
engine.QueueStatUpdate(
    sapi,
    player.Entity,
    stat: "walkspeed",
    value: 0.15f,
    category: "mymod");

// Queue multiple stats at once.
engine.QueueStatUpdates(
    sapi,
    player.Entity,
    new Dictionary<string, float>
    {
        ["walkspeed"] = 0.15f,
        ["healingrate"] = 0.05f
    },
    category: "mymod");
```

## API overview

| Method | Description |
|--------|-------------|
| `QueueStatUpdate(api, player, stat, value, category)` | Queues a single stat update for coalescing. |
| `QueueStatUpdates(api, player, stats, category)` | Queues multiple stat updates at once. |
| `ForceFlush(api, entityId)` | Flushes pending stats for one entity immediately. |
| `ApplyStatImmediate(player, stat, value, category)` | Applies a stat bypassing coalescing. |
| `HasPendingUpdates(entityId)` | Returns `true` if the entity has queued stat changes. |
| `GetPendingUpdateCount()` | Total pending stat updates across all entities. |
| `ClearAllPending(api)` | Clears all pending updates and cancels scheduled flushes. |

## Configuration

| Property | Default | Description |
|----------|---------|-------------|
| `IsEnabled` | `true` | When `false`, stats are applied immediately without batching. |
| `CoalesceWindowMs` | `200` | Time window for batching. |
| `MaxDelayMs` | `1000` | Maximum delay before a forced flush. |
| `DefaultCategory` | `"game"` | Default stat category when none is supplied. |
| `MarkDirtyAttributePath` | `null` | Optional watched attribute path to mark dirty after flush. |

## Notes

- Uses `DeferredWorkService.Coalesce` internally for scheduling.
- Automatically cleans up when a player disconnects.
- `StatCoalescingEngine` is a `ModSystem`; it starts and stops automatically.