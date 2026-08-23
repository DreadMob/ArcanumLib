---
layout: default
title: PlaytimeTracker
nav_order: 42
parent: Common & Utility
---

# PlaytimeTracker & PlaytimeCooldownManager

## What is it for?

`PlaytimeTracker` tracks total online time per player via `PlayerJoin` / `PlayerLeave` events. It persists data to a JSON file and also tracks first join date, last online date, and login streaks.

`PlaytimeCooldownManager` builds on the tracker to provide real-time cooldowns, combat-state tracking, and playtime-gated unlocks.

## When to use it

- You need to track how long players have been online (for unlocks, rewards, display).
- You need real-time cooldowns that survive server restarts.
- You need to know if a player is "in combat" for ability gating.
- You want combined checks (cooldown + combat + playtime) in one call.

## Quick example

### Setup

```csharp
using ArcanumLib.Common;

// In your ModSystem.StartServerSide:
var tracker = new PlaytimeTracker(sapi, "myplaytime_data.json");
var cooldowns = new PlaytimeCooldownManager(tracker);
```

### Querying playtime

```csharp
float hours = tracker.GetPlaytimeHours(playerUid);
long? firstJoin = tracker.GetFirstJoinMs(playerUid);
int streak = tracker.GetLoginStreak(playerUid);
```

### Cooldowns and combat state

```csharp
// Set a cooldown
cooldowns.SetCooldown(playerUid, "ability:dash");

// Check cooldown
if (cooldowns.IsOnCooldown(playerUid, "ability:dash", 30))
    return; // still on cooldown

// Mark combat
cooldowns.MarkCombat(playerUid);

// Check if in combat
if (cooldowns.IsInCombat(playerUid, 10))
    return; // can't use while in combat

// Combined check
if (cooldowns.CanProceed(playerUid, "ability:dash", cooldownSeconds: 30,
    combatCooldownSeconds: 10, requiredPlaytimeHours: 5))
{
    // All conditions met — proceed
}
```

### Import historical data

```csharp
var historical = new Dictionary<string, long>
{
    ["player1"] = 3600000L * 100, // 100 hours
    ["player2"] = 3600000L * 50,
};
int imported = tracker.ImportFromDictionary(historical);
```

## API overview

### PlaytimeTracker

| Method | Description |
|--------|-------------|
| `GetPlaytimeHours(uid)` | Total playtime in hours. |
| `GetPlaytimeMs(uid)` | Total playtime in milliseconds. |
| `GetAllPlaytimeHours()` | All players with their hours (including offline). |
| `GetFirstJoinMs(uid)` | First join timestamp (UTC ms), or null. |
| `GetLastOnlineMs(uid)` | Last online timestamp (UTC ms). Returns now if online. |
| `GetLoginStreak(uid)` | Current consecutive-day login streak. |
| `SetFirstJoinMs(uid, ms)` | Override first join timestamp. |
| `SetTotalMs(uid, totalMs)` | Override total playtime (for imports). |
| `ImportFromDictionary(map)` | Bulk import playerUid → totalMs. Returns count. |
| `OnSessionSaved` | Event fired on save: `(playerUid, totalMs)`. |

### PlaytimeCooldownManager

| Method | Description |
|--------|-------------|
| `SetCooldown(uid, category)` | Start a cooldown. |
| `IsOnCooldown(uid, category, seconds)` | True if still on cooldown. |
| `GetCooldownRemaining(uid, category, seconds)` | Seconds remaining (0 = ready). |
| `ClearCooldown(uid, category)` | Clear immediately. |
| `MarkCombat(uid)` | Mark player as in combat. |
| `IsInCombat(uid, seconds)` | True if in combat within the window. |
| `GetCombatRemaining(uid, seconds)` | Seconds until out of combat. |
| `HasRequiredPlaytime(uid, hours)` | True if playtime requirement met. |
| `GetPlaytimeRemaining(uid, hours)` | Hours remaining until unlocked. |
| `CanProceed(uid, category, cd, combatCd, hours)` | Combined check: cooldown + combat + playtime. |

## Notes

- All timestamps use `DateTimeOffset.UtcNow` so cooldowns survive server restarts.
- Data is persisted to `ModData/<dataFileName>.json` on save and player leave.
- The tracker auto-subscribes to `PlayerJoin`, `PlayerLeave`, and `GameWorldSave` events via `EventScope`.
