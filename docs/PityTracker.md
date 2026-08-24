---
layout: default
title: PityTracker
nav_order: 25
---

# PityTracker

Per-player pity/guarantee counters for loot, rewards, or any quality-tier system.

## What is it for?

Use `PityTracker` when your mod has random drops with quality tiers and you want to guarantee a high-tier result after a streak of bad luck:

- Loot caches with Common / Rare / Epic / Legendary tiers.
- Reward chests that should not be frustrating forever.
- Gacha-like systems where the player eventually gets a top-tier item.

It persists via `ModDataStore` and can import legacy save data.

## Core concepts

| Term | Meaning |
|------|---------|
| `PityDefinition` | A loot source and its guarantee rules. |
| `PityTierRule` | A quality tier and the maximum opens until it is guaranteed. |
| `PityCounters` | Per-player, per-definition counters. |
| `guaranteedQuality` | The highest tier whose counter has reached its threshold. |

## Quick example

```csharp
using ArcanumLib.Progression;

var tracker = new PityTracker(sapi, "oldmod:pity:data");

tracker.RegisterDefinition(new PityDefinition
{
    definitionId = "mymod:rewards:common",
    rules = new List<PityTierRule>
    {
        new() { qualityTierIndex = 3, opensUntilGuarantee = 30 },
        new() { qualityTierIndex = 4, opensUntilGuarantee = 60 }
    }
});

int guaranteed = tracker.GetGuaranteedQuality(playerUid, "mymod:rewards:common");
```

## Usage

### Register a definition

```csharp
tracker.RegisterDefinition(new PityDefinition
{
    definitionId = "mymod:rewards:common",
    rules = new List<PityTierRule>
    {
        new() { qualityTierIndex = 3, opensUntilGuarantee = 30, displayNameKey = "mymod:rare" },
        new() { qualityTierIndex = 4, opensUntilGuarantee = 60, displayNameKey = "mymod:legendary" }
    }
});
```

### Record an open

```csharp
// player opened a "common" reward cache and rolled quality 2 (Uncommon)
tracker.RecordOpen(playerUid, "mymod:rewards:common", 2);
```

The tracker resets the counter for tiers `<= 2` and increments tiers `> 2`.

### Enforce the guarantee

```csharp
int guaranteed = tracker.GetGuaranteedQuality(playerUid, "mymod:rewards:common");
int finalQuality = Math.Max(guaranteed, rolledQuality);

tracker.RecordOpen(playerUid, "mymod:rewards:common", finalQuality);
```

### Check opens until guarantee

```csharp
// How many opens until the next guaranteed high-tier drop?
int remaining = tracker.GetOpensUntilGuarantee(playerUid, "mymod:rewards:common");

// Or for a specific tier:
int untilLegendary = tracker.GetOpensUntilGuarantee(playerUid, "mymod:rewards:common", qualityTierIndex: 4);
```

Returns 0 if a guarantee is already due, or -1 if the definition/player is not found.

### Legacy migration

```csharp
var tracker = new PityTracker(sapi, "oldmod:pity:data");
tracker.AddLegacyFallbackKey("anothermod:oldpity");
```

If the new `arcanumlib:pity` store is empty, the tracker tries each registered legacy key once and imports the old counters.

### Save

```csharp
tracker.Save();
```

## ModSystem integration

```csharp
public class MyPityModSystem : ModSystem
{
    private PityTracker? tracker;

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        tracker = new PityTracker(sapi);
        // register definitions
        PityTracker.Current = tracker;
    }

    public override void OnSaveGameData()
    {
        tracker?.Save();
    }
}
```

## Thread-safety and lifecycle

- All public methods on `PityTracker` are thread-safe (`_syncLock`).
- `PityTracker.Current` is a facade backed by `ArcanumServices`. Setting it registers the instance; setting it to `null` unregisters it.
- `PityTrackerModSystem` creates and saves the global tracker on the server.
- `RecordOpen` automatically marks the `ModDataStore` as dirty so the next `Save` persists.