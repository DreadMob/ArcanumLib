#nullable enable
using System;
using System.Collections.Generic;
using ArcanumLib.Common;
using ArcanumLib.Network;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[ProtoContract]
public class BePacketSetSpeed { [ProtoMember(1)] public int Speed; [ProtoMember(2)] public string? Note; }

[ProtoContract]
public class BePacketToggle { }

[ProtoContract]
public class BePacketStatus { [ProtoMember(1)] public float Heat; }

public class BlockEntityPacketsTests
{
    private sealed class FakePlayer { public string Uid = "p1"; }

    private sealed class Harness
    {
        public readonly List<string> Denied = new();
        public readonly List<string> Errors = new();
        public readonly List<BlockEntityAction> GateAsked = new();
        public bool Allow = true;
        public long Now;
        public readonly BlockEntityPacketTable<FakePlayer> Table;

        public Harness()
        {
            Table = new BlockEntityPacketTable<FakePlayer>(
                (_, action) => { GateAsked.Add(action); return Allow; },
                () => Now,
                (_, action, reason) => Denied.Add(action + ":" + reason),
                (what, _) => Errors.Add(what));
        }
    }

    private sealed class DummyBe : BlockEntity { }

    // ─── ids ───────────────────────────────────────────────────────

    [Fact]
    public void IdOf_IsStableAndInsideTheTypedRange()
    {
        int id = BlockEntityPackets.IdOf<BePacketSetSpeed>();
        Assert.Equal(id, BlockEntityPackets.IdOf<BePacketSetSpeed>());
        Assert.InRange(id, BlockEntityPackets.FirstId, BlockEntityPackets.LastId);
        Assert.NotEqual(id, BlockEntityPackets.IdOf<BePacketToggle>());
    }

    [Fact]
    public void IdOf_NeverHitsVanillaIds()
    {
        // slot packets 7-9, open/close 1000/1001 and the low ids vanilla block entities use
        Assert.True(BlockEntityPackets.FirstId > 100000);
        Assert.True(BlockEntityPackets.IdOf<BePacketToggle>() >= BlockEntityPackets.FirstId);
    }

    [Fact]
    public void Hash_IsFnv1a()
    {
        // known FNV-1a 32 vectors
        Assert.Equal(0x811c9dc5u, BlockEntityPacketTable<object>.Hash(""));
        Assert.Equal(0xe40c292cu, BlockEntityPacketTable<object>.Hash("a"));
    }

    [Fact]
    public void RegisteringTheSameMessageTwice_Throws()
    {
        var h = new Harness();
        h.Table.AddFromClient<BePacketToggle>((_, _) => { }, BlockEntityAction.Configure, 0);
        Assert.Throws<InvalidOperationException>(() =>
            h.Table.AddFromClient<BePacketToggle>((_, _) => { }, BlockEntityAction.Configure, 0));
    }

    // ─── dispatch ──────────────────────────────────────────────────

    [Fact]
    public void Dispatch_DecodesAndRunsTheHandler()
    {
        var h = new Harness();
        BePacketSetSpeed? got = null;
        h.Table.AddFromClient<BePacketSetSpeed>((_, m) => got = m, BlockEntityAction.Configure, 0);

        var data = SerializerUtil.Serialize(new BePacketSetSpeed { Speed = 42, Note = "x" });
        var r = h.Table.DispatchClient(new FakePlayer(), BlockEntityPackets.IdOf<BePacketSetSpeed>(), data);

        Assert.Equal(PacketDispatch.Handled, r);
        Assert.Equal(42, got!.Speed);
        Assert.Equal("x", got.Note);
        Assert.Equal(new[] { BlockEntityAction.Configure }, h.GateAsked);
    }

    [Fact]
    public void Dispatch_EmptyPayload_IsAFreshMessage()
    {
        var h = new Harness();
        int hits = 0;
        h.Table.AddFromClient<BePacketToggle>((_, m) => { Assert.NotNull(m); hits++; }, BlockEntityAction.InsertExtract, 0);

        Assert.Equal(PacketDispatch.Handled, h.Table.DispatchClient(new FakePlayer(), BlockEntityPackets.IdOf<BePacketToggle>(), null));
        Assert.Equal(PacketDispatch.Handled, h.Table.DispatchClient(new FakePlayer(), BlockEntityPackets.IdOf<BePacketToggle>(), Array.Empty<byte>()));
        Assert.Equal(2, hits);
    }

    [Fact]
    public void Dispatch_UnknownId_IsNotMine()
    {
        var h = new Harness();
        h.Table.AddFromClient<BePacketToggle>((_, _) => { }, BlockEntityAction.Configure, 0);
        Assert.Equal(PacketDispatch.NotMine, h.Table.DispatchClient(new FakePlayer(), 1234, null));
        Assert.Equal(PacketDispatch.NotMine, h.Table.DispatchServer(1234, null));
        Assert.Empty(h.GateAsked);
    }

    [Fact]
    public void Dispatch_BadPayload_IsDroppedAndLoggedNeverThrown()
    {
        var h = new Harness();
        bool ran = false;
        h.Table.AddFromClient<BePacketSetSpeed>((_, _) => ran = true, BlockEntityAction.Configure, 0);

        var garbage = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x12, 0x80 };
        var r = h.Table.DispatchClient(new FakePlayer(), BlockEntityPackets.IdOf<BePacketSetSpeed>(), garbage);

        Assert.Equal(PacketDispatch.Dropped, r);
        Assert.False(ran);
        Assert.Contains(h.Denied, d => d.Contains("bad-payload"));
    }

    [Fact]
    public void Dispatch_AccessDenied_SkipsDecodeAndHandler()
    {
        var h = new Harness { Allow = false };
        bool ran = false;
        h.Table.AddFromClient<BePacketSetSpeed>((_, _) => ran = true, BlockEntityAction.InsertExtract, 0);

        var r = h.Table.DispatchClient(new FakePlayer(), BlockEntityPackets.IdOf<BePacketSetSpeed>(), new byte[] { 0xFF });

        Assert.Equal(PacketDispatch.Dropped, r);
        Assert.False(ran);
        Assert.Empty(h.Denied);   // the gate reports its own refusals; garbage was never read
        Assert.Equal(new[] { BlockEntityAction.InsertExtract }, h.GateAsked);
    }

    [Fact]
    public void Dispatch_ActionNone_BypassesTheGate()
    {
        var h = new Harness { Allow = false };
        bool ran = false;
        h.Table.AddFromClient<BePacketToggle>((_, _) => ran = true, BlockEntityAction.None, 0);

        Assert.Equal(PacketDispatch.Handled, h.Table.DispatchClient(new FakePlayer(), BlockEntityPackets.IdOf<BePacketToggle>(), null));
        Assert.True(ran);
        Assert.Empty(h.GateAsked);
    }

    [Fact]
    public void Dispatch_NoPlayer_IsDropped()
    {
        var h = new Harness();
        h.Table.AddFromClient<BePacketToggle>((_, _) => { }, BlockEntityAction.None, 0);
        Assert.Equal(PacketDispatch.Dropped, h.Table.DispatchClient(null, BlockEntityPackets.IdOf<BePacketToggle>(), null));
    }

    [Fact]
    public void Dispatch_HandlerThrows_IsDroppedAndLogged()
    {
        var h = new Harness();
        h.Table.AddFromClient<BePacketToggle>((_, _) => throw new InvalidOperationException("boom"), BlockEntityAction.Configure, 0);
        Assert.Equal(PacketDispatch.Dropped, h.Table.DispatchClient(new FakePlayer(), BlockEntityPackets.IdOf<BePacketToggle>(), null));
        Assert.Single(h.Errors);
    }

    [Fact]
    public void Dispatch_RateLimit_DropsRepeatsInsideTheInterval()
    {
        var h = new Harness();
        int hits = 0;
        h.Table.AddFromClient<BePacketToggle>((_, _) => hits++, BlockEntityAction.Configure, 100);
        int id = BlockEntityPackets.IdOf<BePacketToggle>();

        h.Now = 1000; h.Table.DispatchClient(new FakePlayer(), id, null);
        h.Now = 1050; Assert.Equal(PacketDispatch.Dropped, h.Table.DispatchClient(new FakePlayer(), id, null));
        h.Now = 1100; h.Table.DispatchClient(new FakePlayer(), id, null);
        Assert.Equal(2, hits);
    }

    [Fact]
    public void DispatchServer_DecodesForTheClient()
    {
        var h = new Harness();
        float heat = 0;
        h.Table.AddFromServer<BePacketStatus>(m => heat = m.Heat);
        var r = h.Table.DispatchServer(BlockEntityPackets.IdOf<BePacketStatus>(), SerializerUtil.Serialize(new BePacketStatus { Heat = 3.5f }));
        Assert.Equal(PacketDispatch.Handled, r);
        Assert.Equal(3.5f, heat);
        Assert.Equal(PacketDispatch.Dropped, h.Table.DispatchServer(BlockEntityPackets.IdOf<BePacketStatus>(), new byte[] { 0xFF, 0xFF }));
    }

    // ─── vanilla ids ───────────────────────────────────────────────

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(1000)]
    [InlineData(1001)]
    [InlineData(1002)]
    public void WithoutInventory_VanillaIdsGoToTheBaseClass(int packetid)
    {
        var packets = new BlockEntityPackets(new DummyBe(), "test")
            .FromClient<BePacketToggle>((_, _) => { });
        Assert.False(packets.HandleClient(null!, packetid, Array.Empty<byte>()));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(1000)]
    [InlineData(1001)]
    public void WithInventory_VanillaInventoryIdsAreTakenOver(int packetid)
    {
        // no player → refused, but claimed so the base class never sees an ungated slot packet
        var packets = new BlockEntityPackets(new DummyBe(), "test").WithInventory(() => null);
        Assert.True(packets.HandleClient(null!, packetid, Array.Empty<byte>()));
    }

    [Fact]
    public void WithInventory_OpenCloseOff_LeavesThemToTheBaseClass()
    {
        var packets = new BlockEntityPackets(new DummyBe(), "test").WithInventory(() => null, openClose: false);
        Assert.False(packets.HandleClient(null!, 1000, null));
        Assert.False(packets.HandleClient(null!, 1001, null));
        Assert.True(packets.HandleClient(null!, 8, Array.Empty<byte>()));
    }

    [Fact]
    public void HandleServer_UnknownId_GoesToTheBaseClass()
    {
        var packets = new BlockEntityPackets(new DummyBe(), "test").FromServer<BePacketStatus>(_ => { });
        Assert.False(packets.HandleServer(1002, null));
        Assert.True(packets.HandleServer(BlockEntityPackets.IdOf<BePacketStatus>(), null));
    }
}
