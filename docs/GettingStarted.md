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

## Commands

Register a server command with typed arguments and autocomplete:

```csharp
using ArcanumLib.Commands;

public override void StartServerSide(ICoreServerAPI sapi)
{
    CommandBuilder
        .Create(sapi, "mymod.givepoints")
        .WithDescription("Gives points to a player.")
        .WithPermission("mymod.admin")
        .Arg<string>("player", autocomplete: (api, player) => api.World.AllOnlinePlayers.Select(p => p.PlayerName).ToArray())
        .Arg<int>("amount")
        .OnExecute((api, player, args) =>
        {
            var targetName = args.String("player");
            var amount = args.Int("amount");
            // ...
        });
}
```

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
using ArcanumLib.Performance;

// On the server
DeferredWork.Server.Schedule("mymod.cleanup", () => Cleanup(), 5000);

// On the client
DeferredWork.Client.Schedule("mymod.fx", () => SpawnFx(), 250);
```

## Status effects

Apply timed buffs and debuffs to entities:

```csharp
using ArcanumLib.Effects;

var effect = new StatModifierEffect("mymod:swiftness", EntityStats.Speed, 1.5f);
StatusEffectManager.Apply(entity, effect, durationMs: 10000, data: null);

if (StatusEffectManager.Has(entity, "mymod:swiftness"))
{
    StatusEffectManager.Remove(entity, "mymod:swiftness");
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

## Services

Resolve the current API or a shared service by scope:

```csharp
using ArcanumLib.Core;

var sapi = ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server);
var playtime = ArcanumServices.Get<PlaytimeTracker>(ArcanumServiceScope.Server);
```

## Next steps

- See [`ModDataStore`](ModDataStore.md) for persistence patterns and migrations.
- See [`CommandBuilder`](CommandBuilder.md) for command parser details.
- See [`TypedNetworkChannel`](TypedNetworkChannel.md) for targeted and broadcast networking.
- See [`DeferredWork`](DeferredWork.md) for scheduler patterns.
- See [`StatusEffects`](StatusEffects.md) for custom effect types.
