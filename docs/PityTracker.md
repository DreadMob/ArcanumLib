# PityTracker

`ArcanumLib.Progression.PityTracker` is a reusable per-player pity (guarantee) counter for any loot-quality or gacha-style system. It tracks how many "opens" a player has made since last receiving each quality tier and can force a guaranteed drop.

## When to use it

Use `PityTracker` when your mod has:

- Loot boxes, reward caches, or chests with multiple quality tiers.
- A desire to guarantee a high-tier drop after a configurable number of unlucky opens.
- A need to migrate old save data without losing player progress.

## Basic concepts

- **Definition** (`PityDefinition`) — a group of tier rules. Usually one per loot source (e.g. `mymod:rewards:common`).
- **Rule** (`PityTierRule`) — a quality tier and the maximum number of opens until it is guaranteed.
- **Counter** (`PityCounters`) — per-player, per-definition counters of `opensSinceQuality` for each tier and `totalOpens`.
- **Guaranteed quality** — the highest tier whose counter has reached its `opensUntilGuarantee` threshold.

## Setup

```csharp
var tracker = new PityTracker(sapi, "oldmod:pity:data"); // sapi may be null in tests

// Register a definition with two guaranteed tiers
tracker.RegisterDefinition(new PityDefinition
{
    definitionId = "mymod:rewards:common",
    rules = new List<PityTierRule>
    {
        new() { qualityTierIndex = 3, opensUntilGuarantee = 30, displayNameKey = "mymod:quality-radiant" },
        new() { qualityTierIndex = 4, opensUntilGuarantee = 60, displayNameKey = "mymod:quality-abyssal" }
    }
});
```

## Recording opens

When a player opens a loot source, call `RecordOpen` with the rolled quality. The tracker resets the counter for all tiers `<= rolledQuality` and increments the others.

```csharp
tracker.RecordOpen(playerUid, "mymod:rewards:common", rolledQuality);
```

## Getting the guaranteed quality

Before rolling, ask the tracker which tier is currently guaranteed.

```csharp
int guaranteed = tracker.GetGuaranteedQuality(playerUid, "mymod:rewards:common");
if (guaranteed > 0)
{
    // force this tier as the result, then record it
}
```

## Legacy migration

`PityTracker` reads from the `arcanumlib:pity` `ModDataStore`. If that store is empty on load, it tries to import from each registered `LegacyFallbackKey`.

Register legacy keys either in the constructor or later:

```csharp
var tracker = new PityTracker(sapi, "oldmod:pity:data");
tracker.AddLegacyFallbackKey("anothermod:oldpity");
```

The migration is idempotent: it only runs when the new store is empty, so reloading does not overwrite already-migrated data.

## Persistence

`PityTracker` uses `ModDataStore` for per-savegame persistence. Call `Save` when appropriate (e.g. `ModSystem.OnSave`):

```csharp
tracker.Save();
```

## ModSystem integration

Create a `ModSystem` to keep a global `PityTracker` and set `PityTracker.Current`:

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

## Integration with a quest or loot framework

If your mod already has its own definitions, implement `IPityProvider` and forward calls to `PityTracker`:

```csharp
public class MyPityProvider : IPityProvider
{
    private readonly PityTracker tracker;
    // ...
    public int GetGuaranteedQuality(string playerUid, string definitionId) => tracker.GetGuaranteedQuality(playerUid, definitionId);
    public void RecordOpen(string playerUid, string definitionId, int rolledQuality) => tracker.RecordOpen(playerUid, definitionId, rolledQuality);
    public PityCounters? GetCounters(string playerUid, string definitionId) => tracker.GetCounters(playerUid, definitionId);
    public bool TryGetDefinition(string definitionId, out PityDefinition? definition) => tracker.TryGetDefinition(definitionId, out definition);
}
```
