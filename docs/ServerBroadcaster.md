---
layout: default
title: ServerBroadcaster
---

# ServerBroadcaster

Broadcast packets to all online players with predicate, radius, and exclusion filters.

## What is it for?

`ServerBroadcaster` is a static helper that sends packets to all online players through a `IServerNetworkChannel`, with optional filtering by predicate, world-position radius, or exclusion list.

## When to use it

- Send a notification packet to all online players.
- Send an effect packet only to players within a certain radius.
- Broadcast to all players except the sender.
- Send to a custom group defined by a predicate.

## Quick example

```csharp
using ArcanumLib.Network;

// Broadcast to everyone
ServerBroadcaster.BroadcastPacket(sapi, channel, myPacket);

// Broadcast within a radius
ServerBroadcaster.BroadcastPacketInRange(
    sapi, channel, myPacket,
    centerX: 100, centerY: 60, centerZ: 200,
    radius: 50);

// Broadcast except the sender
ServerBroadcaster.BroadcastPacketExcept(
    sapi, channel, myPacket,
    excludePlayerUids: new[] { senderUid });
```

## API overview

| Method | Description |
|--------|-------------|
| `BroadcastPacket(sapi, channel, packet)` | Sends to all online players. |
| `BroadcastPacket(sapi, channel, packet, predicate)` | Sends to players matching a predicate. |
| `BroadcastPacketInRange(sapi, channel, packet, x, y, z, radius)` | Sends to players within a spherical radius. |
| `BroadcastPacketExcept(sapi, channel, packet, excludeUids)` | Sends to all except the listed player UIDs. |
| `BroadcastPacketToGroup(sapi, channel, packet, groupPredicate)` | Alias for the predicate overload. |

## Notes

- All methods are null-safe: passing null `sapi` or `channel` is a no-op.
- Radius filtering uses the player entity's world position.
- Exclusion is by player UID, not entity ID.
