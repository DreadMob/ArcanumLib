---
layout: default
title: Machine claims
nav_order: 20
---

# Machine claims

`ArcanumLib.Common.MachineClaims` — may a machine that edits the world on its own (a drill, a dredge, a vehicle's front module, an icebreaker) touch this cell.

## Why not `Claims.TryAccess`

`TryAccess` is for clicks. Every refusal sends an ingame error, resyncs the player's held item and marks the block dirty, so the client re-tesselates the chunk. A machine asks for dozens of cells several times a second: next to a claim that froze and crashed the players nearby. `MachineClaims` asks quietly (`Claims.TestAccess`) and tells the driver or owner why at most every 4 s (`TellEveryMs`) per machine, with the vanilla `noprivilege-*` message.

## Usage

```csharp
using ArcanumLib.Common;

// a vehicle or other entity, for its driver (null = unclaimed land only)
if (!MachineClaims.MayWork(entity, driver, pos, EnumBlockAccessFlags.BuildOrBreak)) return;

// a block entity working for its owner
if (!MachineClaims.MayWork(this, ownerPlayer, ownerUid, pos, EnumBlockAccessFlags.BuildOrBreak)) return;
```

The block-entity overload works while the owner is offline or joining: without a player body it lets the machine into unclaimed land and into claims that `ownerUid` owns, and nowhere else (the engine's access test throws while the player's entity or world data is missing).

Server side. Use it in every tick or repeat path; keep `TryAccess` for one-off clicks.
