---
layout: default
title: ArcanumLibModSystem
nav_order: 6
---

# ArcanumLibModSystem

The library's own `ModSystem`s. Vintage Story discovers them; a dependent mod writes no start-up code for them.

## What they do

| System | Side | Order | Does |
|--------|------|-------|------|
| `ArcanumLibModSystem` | both | -1000 | Joins the `ArcanumRuntime`, registers the APIs and the common services (`EventBusService`, `DeferredWorkService`, `EffectResistanceService`) per side, registers the block behavior `arcanumlib.FacePlayer`, the collectible behavior `ArcanumCullFaces` and (client) the entity renderer `ArcanumGlassShape`, applies the ground-pile and ground-storage cull patches, initialises `ImageIconCache` and the generic tab icons. |
| `ArcanumPerformanceModSystem` | both | 0.1 | Server: `GameTimeScheduler`, `StatCoalescingEngine`. |
| `MultiblockReach` | server | 0.1 | Widens vanilla multiblock fillers (see [Blocks](Blocks.md)). |
| `PlayerTransferModSystem` | server | 0.15 | `/arcanum transfer` (see [Account transfer](PlayerTransfer.md)). |
| `ArcanumDataModSystem` | server | default | `ModDataStore` registry and its load/save on world events, `PlaytimeTracker`, `ActionRegistryService`, `ActionExecutorService`. |
| `StatusEffectModSystem` | both | default | `StatusEffectService`, one per side in that side's scope, ticked once a second. |
| `PlayerAnimationSystem` | both | default | Custom player animations (see [Player animations](PlayerAnimations.md)). |
| `OnlinePlayerCache`, `PlayerZoneTracker`, `PlayerProximityTracker`, `PlayerInLandTracker` | server | default | Player trackers (see [Player trackers](PlayerTrackers.md)). |
| `DiagnosticsModSystem` | server | 1000 | Start-up checks and `/arcanum diagnose`, `monitor`, `eventbus` (see [Diagnostics](Diagnostics.md)). |

## Runtime and singleplayer

`ArcanumRuntime` owns the service registry of one world. In singleplayer the client and the server run in one process: the side that starts second **joins** the runtime of the first instead of replacing it, and the services stay apart by `ArcanumServiceScope` (`Client`, `Server`, `Global`). A new world on a side that is already part of the runtime gets a fresh one. Resolve services by scope:

```csharp
var sapi = ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server);
var bus  = ArcanumServices.Get<IEventBusService>(ArcanumServiceScope.Client);
```

## Notes

- `ArcanumLibModSystem` starts the runtime in `StartPre`, so mods whose `StartPre` runs later can already use `ArcanumServices`.
- On unload each side leaves the runtime; the last one out disposes it, which runs the `ArcanumLifecycle` cleanups (`ImageIconCache`, `CollectibleNameResolver`, `ModDataStore`, `CustomIconRegistry`) and shuts the services down.
- Every chat command of the library is a subcommand of `/arcanum` (`ChatCommands.GetOrCreate("arcanum")`); a dependent mod adds its own subcommands the same way instead of a new root command.
