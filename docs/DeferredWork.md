---
layout: default
title: DeferredWork
nav_order: 60
has_children: true
---

# DeferredWork

## What is it for?

`ArcanumLib.Performance.DeferredWork` is a game-tick scheduler for one-shot, coalesced, callback, and end-of-tick work. It helps avoid multiple immediate callbacks when several systems react to the same event by collecting work and executing it in a controlled order on the main game thread.

## When to use it

- Delay a reaction by a few milliseconds without creating and tracking a tick listener.
- Schedule a one-shot callback (replacement for `RegisterCallback`-based timers).
- Debounce repeated events, such as marking a mesh dirty or rebuilding a cache.
- Run a batch of work at the end of the current tick.
- Cancel or replace pending work before it runs.
- Work must run on the game thread and any failures must be logged, not crash the tick loop.

## Quick example

```csharp
DeferredWork.Schedule("spawn-particles", () => SpawnParticles(pos), 250);
```

## Usage

`DeferredWork` is a `ModSystem`; it starts and stops its game tick listener automatically. You can call the static methods from anywhere once the game has loaded.

| Method | Description |
| --- | --- |
| `Schedule(key, action, delayMs)` | One-shot action after `delayMs`. Calling again with the same key reschedules and replaces the action. |
| `ScheduleCallback(key, action, delayMs)` | Schedules a one-shot callback. Callbacks are tracked separately from `Schedule` tasks and can be cancelled by prefix. |
| `Coalesce(key, action, windowMs, maxDelayMs)` | Repeated calls with the same key extend the window. The action runs after the window expires or `maxDelayMs` is reached. |
| `AtEndOfTick(action)` | Queues work to run at the end of the current tick. |
| `Cancel(key)` | Cancels a pending scheduled or coalesced task. |
| `CancelCallback(key)` | Cancels a pending callback by its key. |
| `CancelCallbacksByPrefix(prefix)` | Cancels all callbacks whose keys start with the given prefix. Useful for cleanup when an entity or module unloads. |
| `IsPending(key)` | Returns `true` if a task with the given key is scheduled. |
| `IsCallbackPending(key)` | Returns `true` if a callback with the given key is pending. |

### Schedule a one-shot task

```csharp
DeferredWork.Schedule("spawn-particles", () => SpawnParticles(pos), 250);
```

### Coalesce repeated events

```csharp
DeferredWork.Coalesce("rebuild-mesh", () => meshManager.MarkDirty(pos), 100, 500);
```

### Schedule a one-shot callback

```csharp
DeferredWork.ScheduleCallback("player-123-fx", () => SpawnFx(pos), 250);
```

### Cancel callbacks by prefix

```csharp
// Cancel all callbacks for a player when they disconnect.
DeferredWork.CancelCallbacksByPrefix("player-123-");
```

### Run work at the end of the tick

```csharp
DeferredWork.AtEndOfTick(() => FlushBuffers());
```

### Cancel or check pending work

```csharp
DeferredWork.Cancel("spawn-particles");

if (DeferredWork.IsPending("rebuild-mesh"))
{
    // still waiting
}
```

## Notes

- `IsEnabled` defaults to `true`. If it is `false` or the world is not loaded, scheduled work executes immediately.
- Exceptions in deferred and end-of-tick tasks are logged and do not stop the tick loop.
- `AtEndOfTick` actions are capped at 100 per tick to avoid infinite cascading.