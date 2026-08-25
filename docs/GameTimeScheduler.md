---
layout: default
title: GameTimeScheduler
parent: "DeferredWork"
nav_order: 2
---

# GameTimeScheduler

Schedule recurring actions by in-game time (daily, hourly, after N hours).

## What is it for?

`GameTimeScheduler` is a server-side `ModSystem` that fires actions based on in-game time rather than real time. It supports daily schedules (e.g. "every day at 6:00"), hourly schedules (e.g. "every hour on the 15-minute mark"), and one-shot schedules after a number of in-game hours.

## When to use it

- Reset daily counters at a specific in-game hour.
- Trigger events at a specific in-game time (e.g. midnight rituals).
- Schedule a one-shot action after N in-game hours of play.
- Any logic that should align with the in-game calendar, not wall-clock time.

## Quick example

```csharp
using ArcanumLib.Core;
using ArcanumLib.Performance;

// In your ModSystem.StartServerSide
var scheduler = ArcanumServices.Get<IGameTimeScheduler>()!;
scheduler.ScheduleDaily(hour: 6, hours =>
{
    sapi.Logger.Notification("It is 6:00 in-game. Resetting daily counters.");
    // ... reset logic
});

scheduler.ScheduleHourly(minute: 0, hours =>
{
    // Runs every in-game hour on the hour
});

int id = scheduler.ScheduleAfterHours(2.5, hours =>
{
    // Runs once after 2.5 in-game hours
});

// Cancel later
scheduler.Cancel(id);
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `ScheduleDaily(hour, action)` | `int` | Fires every in-game day at the given hour (0-23). |
| `ScheduleHourly(minute, action)` | `int` | Fires every in-game hour at the given minute (0-59). |
| `ScheduleAfterHours(hours, action)` | `int` | Fires once after `hours` in-game hours. |
| `Cancel(scheduleId)` | `void` | Cancels a schedule by ID. |
| `CancelAll()` | `void` | Cancels all schedules. |
| `GetScheduleCount()` | `int` | Returns the number of active schedules. |

## Notes

- Server-side only: in-game time is authoritative on the server.
- The scheduler checks for due schedules every `CheckIntervalMs` (default 2000ms).
- `ScheduleAfterHours` schedules are removed after firing; daily/hourly schedules persist until cancelled.
- Set `GameTimeScheduler.IsEnabled = false` to pause all scheduling without removing schedules.