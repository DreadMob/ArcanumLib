#nullable enable
using System;
using System.Collections.Generic;
using ArcanumLib.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using GateFunc = System.Func<Vintagestory.API.Server.IServerPlayer, ArcanumLib.Common.BlockEntityAction, bool>;

namespace ArcanumLib.Network;

/// <summary>
/// Typed packets of one block entity: ProtoBuf message classes instead of hand-numbered ids and byte juggling,
/// with the server-side checks built in. Build it in <c>Initialize</c> on both sides and forward the two
/// <c>OnReceived*Packet</c> overrides to it:
/// <code>
/// _packets = new BlockEntityPackets(this, "mymod")
///     .WithInventory(() => Inventory)
///     .FromClient&lt;SetSpeed&gt;((player, msg) => SetSpeed(msg.Value))
///     .FromServer&lt;StatusView&gt;(msg => _view = msg);
///
/// public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
/// { if (_packets?.HandleClient(player, packetid, data) != true) base.OnReceivedClientPacket(player, packetid, data); }
/// </code>
/// A message's id is derived from its type's full name (<see cref="IdOf{T}"/>), so both sides agree without a
/// shared numbering and a dialog can send with only the position (<see cref="SendToServer{T}(ICoreClientAPI, BlockPos, T)"/>).
/// Ids lie in [<see cref="FirstId"/>, <see cref="LastId"/>], far from the vanilla ids: slot packets 7–9, open/close
/// 1000/1001 and the small ids vanilla block entities use for their own packets.
/// <para/>
/// Every client message passes, in order: a valid server player, the gate for its <see cref="BlockEntityAction"/>
/// (default <see cref="BlockEntityAccessGate.CheckAccess"/>: reach, dimension, loaded chunk, land-claim right), the
/// optional per-message rate limit, then a decode that never throws. Refusals are logged rate-limited without the
/// payload; a handler that throws is logged and the packet dropped.
/// </summary>
public sealed class BlockEntityPackets
{
    /// <summary>Lowest id a typed message can get.</summary>
    public const int FirstId = 0x10000000;
    /// <summary>Highest id a typed message can get.</summary>
    public const int LastId = 0x6FFFFFFF;
    private const int ErrorLogCooldownMs = 4000;

    private readonly BlockEntity _be;
    private readonly string _logTag;
    private readonly BlockEntityPacketTable<IServerPlayer> _table;
    private Func<InventoryBase?>? _inventory;
    private Action<IServerPlayer>? _onOpen, _onClose;
    private bool _openClose;
    private long _lastErrorAt = long.MinValue / 2;

    /// <param name="be">The owning block entity; its <c>Api</c> and <c>Pos</c> are read at use, so building the
    /// packets before <c>base.Initialize</c> is fine.</param>
    /// <param name="logTag">Prefix of refusal log lines, usually the mod id.</param>
    public BlockEntityPackets(BlockEntity be, string logTag = "arcanumlib")
    {
        _be = be ?? throw new ArgumentNullException(nameof(be));
        _logTag = logTag ?? "arcanumlib";
        _table = new BlockEntityPacketTable<IServerPlayer>(
            (player, action) => BlockEntityAccessGate.CheckAccess(_be.Api as ICoreServerAPI, player, _be.Pos, action, _logTag),
            () => _be.Api?.World?.ElapsedMilliseconds ?? 0,
            (player, action, reason) => BlockEntityAccessGate.LogDenial(_be.Api as ICoreServerAPI, player, _be.Pos, action, reason, _logTag),
            LogError);
    }

    /// <summary>The packet id of message type <typeparamref name="T"/>: a hash of its full type name, folded into
    /// [<see cref="FirstId"/>, <see cref="LastId"/>]. Renaming or moving the class changes it; client and server
    /// ship together, so that is fine.</summary>
    public static int IdOf<T>() => IdCache<T>.Id;

    /// <summary>Replaces the server-side gate (e.g. a mod's own stricter reach check or an owner rule). The gate is
    /// asked for every action except <see cref="BlockEntityAction.None"/> and must report its own refusals.</summary>
    public BlockEntityPackets WithGate(GateFunc gate)
    {
        _table.Gate = gate ?? throw new ArgumentNullException(nameof(gate));
        return this;
    }

    /// <summary>
    /// Handles the vanilla inventory packets for <paramref name="inventory"/> on the server: slot packets 7–9 pass
    /// the gate (<see cref="BlockEntityAction.InsertExtract"/>), the engine's open-session check and
    /// <see cref="InventorySlotPacketGuard"/> before <c>InvNetworkUtil.HandleClientPacket</c>; any other id below
    /// 1000 is dropped. With <paramref name="openClose"/> (default) 1000 opens the inventory after the same gate and
    /// 1001 closes it ungated; <paramref name="onOpen"/>/<paramref name="onClose"/> run after them. Pass
    /// <c>openClose: false</c> when the base class owns 1000/1001.
    /// </summary>
    public BlockEntityPackets WithInventory(Func<InventoryBase?> inventory, Action<IServerPlayer>? onOpen = null,
        Action<IServerPlayer>? onClose = null, bool openClose = true)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _onOpen = onOpen; _onClose = onClose; _openClose = openClose;
        return this;
    }

    /// <summary>Registers a client→server message and its server handler. Register on both sides (the client
    /// never runs the handler); <paramref name="minIntervalMs"/> drops repeats of this message that come sooner
    /// (any player), for sliders and other GUI spam.</summary>
    public BlockEntityPackets FromClient<T>(Action<IServerPlayer, T> handler,
        BlockEntityAction action = BlockEntityAction.Configure, int minIntervalMs = 0) where T : class, new()
    {
        _table.AddFromClient(handler, action, minIntervalMs);
        return this;
    }

    /// <summary>Registers a server→client message and its client handler.</summary>
    public BlockEntityPackets FromServer<T>(Action<T> handler) where T : class, new()
    {
        _table.AddFromServer(handler);
        return this;
    }

    /// <summary>Forward <c>OnReceivedClientPacket</c> here. False = not ours, call the base method.</summary>
    public bool HandleClient(IPlayer player, int packetid, byte[]? data)
    {
        var sp = player as IServerPlayer;
        if (_inventory != null)
        {
            if (packetid < 1000) { HandleSlotPacket(sp, packetid, data); return true; }
            if (_openClose && packetid == (int)EnumBlockEntityPacketId.Open) { Open(sp); return true; }
            if (_openClose && packetid == (int)EnumBlockEntityPacketId.Close) { Close(player, sp); return true; }
        }
        return _table.DispatchClient(sp, packetid, data) != PacketDispatch.NotMine;
    }

    /// <summary>Forward <c>OnReceivedServerPacket</c> here. False = not ours, call the base method.</summary>
    public bool HandleServer(int packetid, byte[]? data)
        => _table.DispatchServer(packetid, data) != PacketDispatch.NotMine;

    /// <summary>Client: sends <paramref name="msg"/> to this block entity on the server.</summary>
    public void SendToServer<T>(T msg) where T : class
    {
        if (_be.Api is ICoreClientAPI capi) SendToServer(capi, _be.Pos, msg);
    }

    /// <summary>Client: sends <paramref name="msg"/> to the block entity at <paramref name="pos"/> — for dialogs
    /// that keep only the position.</summary>
    public static void SendToServer<T>(ICoreClientAPI capi, BlockPos pos, T msg) where T : class
        => capi.Network.SendBlockEntityPacket(pos, IdOf<T>(), BlockEntityPacketTable<IServerPlayer>.Encode(msg));

    /// <summary>Server: sends <paramref name="msg"/> to one player's copy of this block entity.</summary>
    public void SendTo<T>(IServerPlayer player, T msg) where T : class
    {
        if (player == null || _be.Api is not ICoreServerAPI sapi) return;
        sapi.Network.SendBlockEntityPacket(player, _be.Pos, IdOf<T>(), BlockEntityPacketTable<IServerPlayer>.Encode(msg));
    }

    /// <summary>Server: sends <paramref name="msg"/> to several players, encoding it once.</summary>
    public void SendTo<T>(IEnumerable<IServerPlayer> players, T msg) where T : class
    {
        if (players == null || _be.Api is not ICoreServerAPI sapi) return;
        byte[]? data = null;
        foreach (var p in players)
            if (p != null) sapi.Network.SendBlockEntityPacket(p, _be.Pos, IdOf<T>(), data ??= BlockEntityPacketTable<IServerPlayer>.Encode(msg));
    }

    /// <summary>Server: sends <paramref name="msg"/> to every client that has this block entity loaded.</summary>
    public void SendToClients<T>(T msg) where T : class
    {
        if (_be.Api is ICoreServerAPI sapi)
            sapi.Network.BroadcastBlockEntityPacket(_be.Pos, IdOf<T>(), BlockEntityPacketTable<IServerPlayer>.Encode(msg));
    }

    private void HandleSlotPacket(IServerPlayer? sp, int packetid, byte[]? data)
    {
        if (sp?.Entity == null || data == null || !BlockEntityAccessGate.IsInventoryPacket(packetid)) return;
        var inv = _inventory!();
        if (inv == null || !_table.Gate(sp, BlockEntityAction.InsertExtract)) return;
        if (!inv.CanPlayerModify(sp, sp.Entity.Pos)) { _table.Deny(sp, BlockEntityAction.InsertExtract, "inventory-not-open"); return; }
        if (!InventorySlotPacketGuard.IsValid(sp, inv, packetid, data)) { _table.Deny(sp, BlockEntityAction.InsertExtract, "bad-slot-packet"); return; }
        inv.InvNetworkUtil?.HandleClientPacket(sp, packetid, data);
    }

    private void Open(IServerPlayer? sp)
    {
        var inv = _inventory!();
        if (sp == null || inv == null || !_table.Gate(sp, BlockEntityAction.InsertExtract)) return;
        sp.InventoryManager?.OpenInventory(inv);
        Run(_onOpen, sp, "open");
    }

    private void Close(IPlayer player, IServerPlayer? sp)
    {
        var inv = _inventory!();
        if (inv != null) player?.InventoryManager?.CloseInventory(inv);
        if (sp != null) Run(_onClose, sp, "close");
    }

    private void Run(Action<IServerPlayer>? hook, IServerPlayer sp, string what)
    {
        if (hook == null) return;
        try { hook(sp); }
        catch (Exception e) { LogError("Inventory " + what + " hook failed", e); }
    }

    private void LogError(string what, Exception e)
    {
        var world = _be.Api?.World;
        if (world == null) return;
        long now = world.ElapsedMilliseconds;
        if (now - _lastErrorAt < ErrorLogCooldownMs) return;
        _lastErrorAt = now;
        world.Logger.Error("[{0}] {1} at {2} ({3}): {4}", _logTag, what, _be.Pos, _be.GetType().Name, e);
    }

    private static class IdCache<T>
    {
        public static readonly int Id = (int)(FirstId + BlockEntityPacketTable<IServerPlayer>.Hash(typeof(T).FullName ?? typeof(T).Name) % ((uint)LastId - FirstId + 1));
    }
}
