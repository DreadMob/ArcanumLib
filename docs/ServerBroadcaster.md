---
layout: default
title: ServerBroadcaster
nav_order: 50
---

# ServerBroadcaster

Static helpers for sending network packets to all online players or a filtered subset.

## What is it for?

- Broadcasting packets to all online players.
- Radius, exclusion, and predicate-based filtering.
- Snapshotting the player list before the loop so disconnects mid-broadcast do not crash enumeration.

## Quick example

```csharp
using ArcanumLib.Network;

// Broadcast to all
ServerBroadcaster.BroadcastPacket(sapi, channel, packet);

// Within 64 blocks of a position
ServerBroadcaster.BroadcastPacketInRange(sapi, channel, packet, x, y, z, 64);

// Exclude a specific player
ServerBroadcaster.BroadcastPacketExcept(sapi, channel, packet, new[] { excludedUid });
```

## Notes

- `BroadcastPacket` snapshots `sapi.World.AllOnlinePlayers` into a `List<IPlayer>` before sending.
- Only `IServerPlayer` instances receive packets; the loop casts and skips non-server players.
