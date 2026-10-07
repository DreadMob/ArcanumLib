#nullable enable
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Common;

/// <summary>
/// Pre-flight validation for vanilla inventory slot packets (ids 7–9) before
/// they reach <c>InvNetworkUtil.HandleClientPacket</c>.
///
/// The engine handlers index slot arrays DIRECTLY:
/// activate-slot does <c>inv[packet.TargetSlot]</c> and move/flip resolve both
/// inventories then index them without a bounds check — an out-of-range id
/// throws on the server packet thread. Worse, move/flip happily operate on any
/// two inventories the player can resolve, so a crafted packet could shuffle a
/// third party's open inventory through this device's BE channel. Anything
/// that fails here is dropped before the engine sees it.
/// </summary>
public static class InventorySlotPacketGuard
{
    /// <summary>
    /// True when the packet deserializes, carries the sub-packet matching
    /// <paramref name="packetid"/>, involves <paramref name="inv"/>, and every
    /// slot index that will be indexed is inside that inventory's range.
    /// </summary>
    public static bool IsValid(IPlayer player, InventoryBase inv, int packetid, byte[] data)
    {
        Packet_Client packet;
        try
        {
            packet = Packet_ClientSerializer.DeserializeBuffer(data, data.Length, new Packet_Client());
        }
        catch
        {
            return false;
        }
        if (packet == null) return false;

        switch (packetid)
        {
            case 7: // activate slot — always resolves against this inventory
            {
                var p = packet.ActivateInventorySlot;
                return p != null && InRange(inv, p.TargetSlot);
            }
            case 8:
            {
                var p = packet.MoveItemstack;
                return p != null && BoundsOk(player, inv,
                    p.SourceInventoryId, p.SourceSlot, p.TargetInventoryId, p.TargetSlot);
            }
            case 9:
            {
                var p = packet.Flipitemstacks;
                return p != null && BoundsOk(player, inv,
                    p.SourceInventoryId, p.SourceSlot, p.TargetInventoryId, p.TargetSlot);
            }
            default:
                return false;
        }
    }

    private static bool InRange(InventoryBase inv, int slot)
        => slot >= 0 && slot < inv.Count;

    /// <summary>
    /// Our inventory must be a party to the transfer; every index addressing a
    /// resolvable inventory must be in range. An unresolvable foreign inventory
    /// is left to the engine, which treats it as a failed move and reverts —
    /// it never indexes a null inventory.
    /// </summary>
    private static bool BoundsOk(IPlayer player, InventoryBase inv,
        string? srcId, int srcSlot, string? dstId, int dstSlot)
    {
        bool srcOurs = srcId != null && srcId == inv.InventoryID;
        bool dstOurs = dstId != null && dstId == inv.InventoryID;
        if (!srcOurs && !dstOurs) return false;

        if (srcOurs && !InRange(inv, srcSlot)) return false;
        if (dstOurs && !InRange(inv, dstSlot)) return false;

        // Bound-check the foreign side too when it resolves — the engine
        // indexes it just as blindly.
        var srcInv = srcOurs ? inv : player?.InventoryManager?.GetInventory(srcId ?? "") as InventoryBase;
        var dstInv = dstOurs ? inv : player?.InventoryManager?.GetInventory(dstId ?? "") as InventoryBase;
        if (srcInv != null && !InRange(srcInv, srcSlot)) return false;
        if (dstInv != null && !InRange(dstInv, dstSlot)) return false;
        return true;
    }
}
