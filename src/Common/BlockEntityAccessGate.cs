#nullable enable
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Common;

/// <summary>What a client→server block-entity packet is trying to do — decides which land-claim right applies.</summary>
public enum BlockEntityAction
{
    /// <summary>GUI settings edits.</summary>
    Configure,
    /// <summary>Inventory slot packets and item moves.</summary>
    InsertExtract
}

/// <summary>
/// Server-side gate for client→server block-entity packets. The engine checks range and Use-claim for block
/// interactions, but custom block-entity packets (<c>OnReceivedClientPacket</c>) get nothing — this is the
/// equivalent: valid player/pos, same dimension, within the player's pick range, loaded chunk, then land-claim
/// <see cref="EnumBlockAccessFlags.Use"/> (which also sends the vanilla "no privilege" message). Denials are logged,
/// rate-limited.
/// </summary>
public static class BlockEntityAccessGate
{
    /// <summary>Slack added to the player's real pick range — the client GUI can lag a step behind.</summary>
    public const double InteractSlack = 0.5;
    private const double SelectionBoxMargin = 4;
    private const int DenyLogCooldownMs = 4000, DenyLogCapacity = 512;
    private static readonly Dictionary<string, long> _denyLogAt = new();

    /// <summary>Full gate: location, then claim rights.</summary>
    public static bool CheckAccess(ICoreServerAPI? sapi, IServerPlayer? player, BlockPos? pos, BlockEntityAction action, string logTag = "arcanumlib")
    {
        if (sapi?.World?.BlockAccessor == null || player?.Entity == null || pos == null) return false;
        if (!sapi.World.BlockAccessor.IsValidPos(pos)) return Deny(sapi, player, pos, action, "bad-pos", logTag);
        if (player.Entity.Pos.Dimension != pos.dimension) return Deny(sapi, player, pos, action, "dimension", logTag);
        double reach = player.WorldData.PickingRange + InteractSlack;
        var e = player.Entity; var eye = e.LocalEyePos;
        double dx = e.Pos.X + eye.X - (pos.X + 0.5), dy = e.Pos.Y + eye.Y - (pos.Y + 0.5), dz = e.Pos.Z + eye.Z - (pos.Z + 0.5);
        double bound = reach + SelectionBoxMargin;
        if (dx * dx + dy * dy + dz * dz > bound * bound) return Deny(sapi, player, pos, action, "distance", logTag);
        if (sapi.World.BlockAccessor.GetChunkAtBlockPos(pos) == null) return Deny(sapi, player, pos, action, "unloaded", logTag);   // never load chunks from packet data
        var resp = sapi.World.Claims.TestAccess(player, pos, EnumBlockAccessFlags.Use);
        if (resp == EnumWorldAccessResponse.Granted) return true;
        LogDenial(sapi, player, pos, action, "access-" + resp.ToString().ToLowerInvariant(), logTag);
        sapi.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.Use);
        return false;
    }

    /// <summary>Vanilla inventory slot packet ids handled by <c>InventoryNetworkUtil.HandleClientPacket</c> (7 activate, 8 move, 9 flip).</summary>
    public static bool IsInventoryPacket(int packetid) => packetid is >= 7 and <= 9;

    /// <summary>Gate for slot packets: reach + Use-claim, then the engine's own session check (inventory actually open).</summary>
    public static bool CanUseInventory(ICoreServerAPI? sapi, IServerPlayer? player, BlockPos pos, InventoryBase? inv, string logTag = "arcanumlib")
    {
        if (!CheckAccess(sapi, player, pos, BlockEntityAction.InsertExtract, logTag)) return false;
        if (inv == null || player?.Entity == null) return false;
        if (!inv.CanPlayerModify(player, player.Entity.Pos)) { LogDenial(sapi, player, pos, BlockEntityAction.InsertExtract, "inventory-not-open", logTag); return false; }
        return true;
    }

    /// <summary>Rate-limited denial log; never includes packet payload.</summary>
    public static void LogDenial(ICoreServerAPI? sapi, IPlayer? player, BlockPos? pos, BlockEntityAction action, string reason, string logTag = "arcanumlib")
    {
        if (sapi?.World == null) return;
        long now = sapi.World.ElapsedMilliseconds;
        string key = $"{player?.PlayerUID ?? "?"}|{action}|{pos?.X},{pos?.Y},{pos?.Z}|{reason}";
        lock (_denyLogAt)
        {
            if (_denyLogAt.TryGetValue(key, out var at) && now - at < DenyLogCooldownMs) return;
            _denyLogAt[key] = now;
            if (_denyLogAt.Count > DenyLogCapacity) _denyLogAt.Clear();
        }
        sapi.World.Logger.Warning("[{0}] Denied {1} for player '{2}' ({3}) at {4}: {5}", logTag, action, player?.PlayerName ?? "?", player?.PlayerUID ?? "?", pos?.ToString() ?? "null", reason);
    }

    private static bool Deny(ICoreServerAPI sapi, IServerPlayer player, BlockPos pos, BlockEntityAction action, string reason, string logTag)
    { LogDenial(sapi, player, pos, action, reason, logTag); return false; }
}
