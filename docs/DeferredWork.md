---
layout: default
title: DeferredWork
nav_order: 60
has_children: true
---

# DeferredWorkService

## What is it for?

`ArcanumLib.Performance.DeferredWorkService` is a game-tick scheduler for one-shot, coalesced, callback, and end-of-tick work. It helps avoid multiple immediate callbacks when several systems react to the same event by collecting work and executing it in a controlled order on the main game thread.

## When to use it

- Delay a reaction by a few milliseconds without creating and tracking a tick listener.
- Schedule a one-shot callback (replacement for `RegisterCallback`-based timers).
- Debounce repeated events, such as marking a mesh dirty or rebuilding a cache.
- Run a batch of work at the end of the current tick.
- Cancel or replace pending work before it runs.
- Work must run on the game thread and any failures must be logged, not crash the tick loop.

## Quick example

```csharp
using ArcanumLib.Core;
using ArcanumLib.Performance;

var dw = ArcanumServices.Get<DeferredWorkService>()!;
dw.Schedule("spawn-particles", () => SpawnParticles(pos), 250);
```

## Usage

`DeferredWorkService` is registered in `ArcanumServices` by `ArcanumLibModSystem`, which starts client and server schedulers automatically.

The instance methods (`Schedule`, `Coalesce`, etc.) pick the right side automatically based on the calling thread. For code that runs on a known side, you can use the explicit scopes:

```csharp
dw.Server.Schedule("save-all", () => Save(), 1000);
dw.Client.Schedule("spawn-fx", () => SpawnFx(pos), 100);
```

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
dw.Schedule("spawn-particles", () => SpawnParticles(pos), 250);
```

### Coalesce repeated events

```csharp
dw.Coalesce("rebuild-mesh", () => meshManager.MarkDirty(pos), 100, 500);
```

### Schedule a one-shot callback

```csharp
dw.ScheduleCallback("player-123-fx", () => SpawnFx(pos), 250);
```

### Cancel callbacks by prefix

```csharp
// Cancel all callbacks for a player when they disconnect.
dw.CancelCallbacksByPrefix("player-123-");
```

### Run work at the end of the tick

```csharp
dw.AtEndOfTick(() => FlushBuffers());
```

### Cancel or check pending work

```csharp
dw.Cancel("spawn-particles");

if (dw.IsPending("rebuild-mesh"))
{
    // still waiting
}
```

## Notes

- `IsEnabled` defaults to `true`. If it is `false` or the world is not loaded, scheduled work executes immediately.
- Exceptions in deferred and end-of-tick tasks are logged and do not stop the tick loop.
- `AtEndOfTick` actions are capped at 100 per tick to avoid infinite cascading.
- Client and server schedulers are independent. In singleplayer, each side gets its own task queue.
- The service is registered in `ArcanumServices` by `ArcanumLibModSystem` during startup and disposed on world unload.
