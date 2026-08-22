---
layout: default
title: CooldownTracker
---

# CooldownTracker

`ArcanumLib.Data.CooldownTracker` stores per-entity cooldown timestamps in `WatchedAttributes` and provides helpers for readiness, elapsed, and progress checks.

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

## Notes

- Cooldowns are stored as milliseconds from `IWorldAccessor.ElapsedMilliseconds`.
- Cooldowns persist across chunk unloads and server restarts.
- The optional `multiplier` parameter scales the duration.
- Use the `CooldownMultiplier` delegate overload to compute the multiplier per entity from attributes or other runtime state.
