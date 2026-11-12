---
layout: default
title: BlockEntityPackets
nav_order: 55
parent: "TypedNetworkChannel"
---

# BlockEntityPackets

`ArcanumLib.Network.BlockEntityPackets` — typed packets of one block entity. Each message is a ProtoBuf class instead of a hand-numbered int id with hand-packed bytes, and the server-side checks from [Packet security]({{ site.baseurl }}{% link PacketSecurity.md %}) are built in, so a handler cannot forget them.

## What is it for?

- A block entity with a GUI that sends settings, button presses or requests to the server.
- A block entity that sends a player (or everyone near) state that does not belong in `ToTreeAttributes`.
- Replacing `OnReceivedClientPacket` overrides that switch on ids and call `BlockEntityAccessGate` by hand.

Packets that are not about one block entity go through a [TypedNetworkChannel]({{ site.baseurl }}{% link TypedNetworkChannel.md %}).

## Quick example

```csharp
using ArcanumLib.Common;
using ArcanumLib.Network;
using ProtoBuf;

public class BEPump : BlockEntityOpenableContainer
{
    [ProtoContract] public sealed class SetSpeedMsg { [ProtoMember(1)] public int Speed; }
    [ProtoContract] public sealed class ToggleMsg { }
    [ProtoContract] public sealed class GaugeMsg { [ProtoMember(1)] public float Pressure; }

    private BlockEntityPackets? _packets;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        _packets = new BlockEntityPackets(this, "mymod")
            .WithInventory(() => Inventory)                                   // vanilla 7–9, 1000, 1001
            .FromClient<SetSpeedMsg>((player, msg) => SetSpeed(msg.Speed), minIntervalMs: 100)
            .FromClient<ToggleMsg>((player, _) => Toggle(), BlockEntityAction.InsertExtract)
            .FromServer<GaugeMsg>(msg => _pressure = msg.Pressure);
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        if (_packets?.HandleClient(player, packetid, data) != true) base.OnReceivedClientPacket(player, packetid, data);
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
        if (_packets?.HandleServer(packetid, data) != true) base.OnReceivedServerPacket(packetid, data);
    }
}

// in the dialog, which keeps only the position:
BlockEntityPackets.SendToServer(capi, pos, new BEPump.SetSpeedMsg { Speed = 40 });
// on the server:
_packets.SendTo(player, new GaugeMsg { Pressure = 0.7f });
_packets.SendToClients(new GaugeMsg { Pressure = 0.7f });
```

Build the packets in `Initialize` on **both** sides with the same registrations; a client never runs `FromClient` handlers and the server never runs `FromServer` ones.

## Message ids

`BlockEntityPackets.IdOf<T>()` is the FNV-1a hash of the message's full type name folded into `[FirstId, LastId]` (`0x10000000`–`0x6FFFFFFF`). Client and server agree without a shared numbering, a dialog can send knowing only the type and the position, and the ids stay far from vanilla: slot packets 7–9, open/close 1000/1001 and the small ids vanilla block entities (anvil, sign, …) use for their own packets — those still reach the base class. Renaming or moving a message class changes its id; client and server ship together, so that is fine. Two messages of one block entity that hash alike throw at registration, never silently.

An empty message (`ToggleMsg` above) travels as an empty payload and arrives as a fresh instance.

## Server-side checks

A `FromClient` message runs only after, in order:

1. a valid server player;
2. the gate for its `BlockEntityAction` — by default `BlockEntityAccessGate.CheckAccess` (position, dimension, reach, loaded chunk, land-claim `Use`; the claim refusal also tells the player). `Configure` is the default action, `InsertExtract` for item moves, `Listen` checks only location (a viewer reporting), `None` skips the gate (closing a window must work out of reach);
3. the optional `minIntervalMs` rate limit of that message on this block entity;
4. a ProtoBuf decode that never throws.

A refusal or an unreadable payload is dropped and logged rate-limited without the payload; a handler that throws is logged and the packet dropped. `WithGate((player, action) => …)` replaces the gate — for a stricter reach check or an owner rule; it must report its own refusals.

### Inventory packets

`WithInventory(() => inv, onOpen, onClose, openClose: true)` handles the vanilla inventory ids on the server: slot packets 7–9 pass the gate (`InsertExtract`), the engine's open-session check (`CanPlayerModify`) and `InventorySlotPacketGuard` before `InvNetworkUtil.HandleClientPacket`; any other id below 1000 is dropped. 1000 opens the inventory after the same gate, 1001 closes it ungated, and the hooks run after them. Pass `openClose: false` when the base class handles 1000/1001 itself.

## Sending

| Side | Call | Goes to |
|------|------|---------|
| client | `SendToServer(msg)` / static `SendToServer(capi, pos, msg)` | this block entity on the server |
| server | `SendTo(player, msg)`, `SendTo(players, msg)` | those players' copy (encoded once) |
| server | `SendToClients(msg)` | every client with the block entity loaded (`BroadcastBlockEntityPacket`) |
