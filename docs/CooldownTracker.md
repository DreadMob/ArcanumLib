---
layout: default
title: CooldownTracker
parent: "ApiExtensions"
nav_order: 2
---

# CooldownTracker

## What is it for?

`ArcanumLib.Data.CooldownTracker` stores per-entity cooldown timestamps in `WatchedAttributes` and provides helpers for readiness, remaining time, progress, and reset. Because values are stored in the entity's attribute tree, cooldowns persist across chunk unloads and server restarts.

## When to use it

- Add a cooldown to an entity ability or item use.
- Track per-entity timers that must survive chunk unloading or server restart.
- Display a cooldown progress bar in a GUI.
- Vary cooldown duration per entity using the `CooldownMultiplier` delegate.

## Quick example

```csharp
using ArcanumLib.Data;

const string Key = "mymod:ability:cooldown";

// On server
public void TryUseAbility(EntityAgent entity)
{
    if (!entity.IsReady(Key, 10.0)) return;

    entity.MarkCooldownStart(Key);
    // ... perform ability
}

public float GetProgress(EntityAgent entity)
    => entity.GetCooldownProgress(Key, 10.0);
```

## API overview

| Method | Returns | Description |
|---|---|---|
| `IsReady(key, durationSeconds, multiplier = 1.0)` | `bool` | `true` if the cooldown has never started or has expired. |
| `IsReady(key, durationSeconds, CooldownMultiplier? multiplierFactory)` | `bool` | Same as above, but the multiplier is resolved from the entity at check time. |
| `MarkCooldownStart(key)` | `void` | Stores the current `ElapsedMilliseconds` as the cooldown start. |
| `GetRemainingCooldownMs(key, ...)` | `long` | Returns the remaining cooldown in milliseconds, or `0` if ready. |
| `GetCooldownProgress(key, ...)` | `float` | Returns a fraction from `0.0` (just started) to `1.0` (ready). |
| `ResetCooldown(key)` | `void` | Clears the cooldown so the next `IsReady` returns `true`. |

## Notes

- Cooldowns are stored as milliseconds from `IWorldAccessor.ElapsedMilliseconds`.
- Cooldowns persist across chunk unloads and server restarts.
- If the server restarts, `ElapsedMilliseconds` resets. `IsReady` detects stale future timestamps and resets them automatically.
- The optional `multiplier` parameter scales the duration.
- Use the `CooldownMultiplier` delegate overload to compute the multiplier per entity from attributes or other runtime state.