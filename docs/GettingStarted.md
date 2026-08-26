---
layout: default
title: Getting Started
nav_order: 2
---

# Getting Started

This guide shows the most common ways to use ArcanumLib from a third-party mod.

## Installation

1. Add `ArcanumLib.csproj` as a project reference in your mod project.
2. Add `arcanumlib` to the `dependson` list in your `modinfo.json`.
3. Make sure ArcanumLib is loaded before your mod (it runs with `ExecuteOrder = -1000`).

## Per-save data

Persist data without writing custom JSON files:

```csharp
using ArcanumLib.Persistence;

public class MyData
{
    public int Counter { get; set; }
}

public override void StartServerSide(ICoreServerAPI sapi)
{
    var store = ModDataStore.GetOrCreate<MyData>(sapi, "mymod", "progress", 1);
    store.Data.Counter++;
    store.MarkDirty();
}
```

## Networking

Send strongly-typed packets between client and server:

```csharp
using ArcanumLib.Network;

public class SyncMessage { public int Counter; }

public override void StartServerSide(ICoreServerAPI sapi)
{
    var ch = new TypedNetworkChannel(sapi, "mymod:sync")
        .OnServer<SyncMessage>((player, msg) => sapi.Logger.Notification("Got {0}", msg.Counter));
}

public override void StartClientSide(ICoreClientAPI capi)
{
    var ch = new TypedNetworkChannel(capi, "mymod:sync")
        .On<SyncMessage>(msg => capi.ShowChatMessage($"Counter is {msg.Counter}"));

    ch.Send(new SyncMessage { Counter = 5 });
}
```

## Deferred work

Run code on the game tick loop without a manual tick listener:

```csharp
using ArcanumLib.Core;
using ArcanumLib.Performance;

var dw = ArcanumServices.Get<IDeferredWorkService>()!;

// On the server
dw.Server.Schedule("mymod.cleanup", () => Cleanup(), 5000);

// On the client
dw.Client.Schedule("mymod.fx", () => SpawnFx(), 250);
```

## Status effects

Apply timed buffs and debuffs to entities:

```csharp
using ArcanumLib.Core;
using ArcanumLib.Effects;

var svc = ArcanumServices.Get<IStatusEffectService>()!;
var effect = new StatModifierEffect("mymod:swiftness", EntityStats.Speed, 1.5f);
svc.Apply(entity, effect, durationMs: 10000, data: null);

if (svc.Has(entity, "mymod:swiftness"))
{
    svc.Remove(entity, "mymod:swiftness");
}
```

## Cooldowns

Track per-entity cooldowns in `WatchedAttributes`:

```csharp
using ArcanumLib.Data;

const string key = "mymod:ability:jump";
if (entity.IsReady(key, 5.0))
{
    entity.MarkCooldownStart(key);
    DoJump();
}

float progress = entity.GetCooldownProgress(key, 5.0);
```

## Inventory changes

Detect when a player's inventory changes to refresh derived stats:

```csharp
using ArcanumLib.Inventory;

var tracker = new InventoryChangeTracker(sapi, "character", 500);

// In a tick or behavior:
if (tracker.ShouldRecalculate(player))
{
    RecalculateStats(player);
}
```

## Resolving common services

Resolve shared services from `ArcanumServices`:

```csharp
using ArcanumLib.Core;

var online = ArcanumServices.Get<IOnlinePlayerCache>();
var events = ArcanumServices.Get<IEventBusService>();
var statusEffects = ArcanumServices.Get<IStatusEffectService>();
var resistances = ArcanumServices.Get<IEffectResistanceService>();
var logger = ArcanumServices.Get<ICategorizedLogger>();
var deferredWork = ArcanumServices.Get<IDeferredWorkService>();
var gameTime = ArcanumServices.Get<IGameTimeScheduler>();
var statEngine = ArcanumServices.Get<IStatCoalescingEngine>();
```

## Next steps

- See [`ModDataStore`](ModDataStore.md) for persistence patterns and migrations.
- See [`TypedNetworkChannel`](TypedNetworkChannel.md) for targeted and broadcast networking.
- See [`DeferredWorkService`](DeferredWork.md) for scheduler patterns.
- See [`StatusEffects`](StatusEffects.md) for custom effect types.
