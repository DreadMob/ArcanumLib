---
layout: default
title: Packet security
parent: "TypedNetworkChannel"
nav_order: 60
---

# Packet security

`ArcanumLib.Common` — server-side checks for packets a client sends to a block entity. The engine checks range and claim rights for block interactions, but custom block-entity packets (`OnReceivedClientPacket`) get nothing; these two helpers are that check.

[BlockEntityPackets]({{ site.baseurl }}{% link BlockEntityPackets.md %}) runs both for every typed message and for the vanilla inventory packets, so a new block entity should use it instead of calling them by hand.

## BlockEntityAccessGate

```csharp
public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
{
    var sapi = (ICoreServerAPI)Api;
    var player = fromPlayer as IServerPlayer;

    if (BlockEntityAccessGate.IsInventoryPacket(packetid))
    {
        if (!BlockEntityAccessGate.CanUseInventory(sapi, player, Pos, Inventory, "mymod")) return;
        if (!InventorySlotPacketGuard.IsValid(fromPlayer, Inventory, packetid, data)) return;
    }
    else if (!BlockEntityAccessGate.CheckAccess(sapi, player, Pos, BlockEntityAction.Configure, "mymod")) return;

    base.OnReceivedClientPacket(fromPlayer, packetid, data);
}
```

`BlockEntityAction`: `Configure` and `InsertExtract` need the claim's `Use` right, `Listen` only the location checks, `None` passes any valid player.

`CheckAccess` passes when the position is valid, in the player's dimension, within the player's pick range (+`InteractSlack` 0.5 and a selection-box margin), in a loaded chunk (packet data never loads chunks), and the player has `Use` rights in the land claim. On a claim refusal it also sends the vanilla "no privilege" message. `CanUseInventory` adds the engine's own check that the inventory is actually open for that player. Denials are logged rate-limited (`LogDenial`), never with the payload.

`CheckAccess` reports refusals to the player, so use it for clicks and packets, not for anything a machine runs every tick.

## InventorySlotPacketGuard

Vanilla slot packets (7 activate, 8 move, 9 flip) are handled by `InvNetworkUtil.HandleClientPacket`, which indexes slot arrays without bounds checks and moves between any two inventories the player can resolve. `IsValid(player, inv, packetid, data)` drops a packet unless it deserializes, carries the matching sub-packet, involves this inventory, and every slot index is in range.
