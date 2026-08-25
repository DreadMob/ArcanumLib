using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Common;
using ArcanumLib.Core;
using ArcanumLib.Network;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class TypedNetworkChannelTests : IDisposable
{
    public TypedNetworkChannelTests()
    {
        ArcanumRuntime.Activate();
        // OnlinePlayerCache is used by SendToAllExcept; reset it before each test.
        new OnlinePlayerCache().Dispose();
    }

    public void Dispose()
    {
        new OnlinePlayerCache().Dispose();
        ArcanumRuntime.Current?.Dispose();
    }

    // ─── Constructor ───────────────────────────────────────────────

    [Fact]
    public void Constructor_NullApi_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TypedNetworkChannel(null!, "test"));
    }

    [Fact]
    public void Constructor_NullName_ThrowsArgumentNullException()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentNullException>(() => new TypedNetworkChannel(api, null!));
    }

    // ─── Register (server) ─────────────────────────────────────────

    [Fact]
    public void Register_OnServer_RegistersChannelWithServerNetwork()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);

        var channel = new TypedNetworkChannel(sapi, "test");

        channel.Register();

        sapi.Network.Received(1).RegisterChannel("test");
    }

    [Fact]
    public void Register_OnClient_RegistersChannelWithClientNetwork()
    {
        var capi = CreateClientApi();
        var clientNetwork = Substitute.For<IClientNetworkChannel>();
        capi.Network.RegisterChannel("test").Returns(clientNetwork);

        var channel = new TypedNetworkChannel(capi, "test");

        channel.Register();

        capi.Network.Received(1).RegisterChannel("test");
    }

    [Fact]
    public void Register_CalledTwice_DoesNotRegisterTwice()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();
        channel.Register();

        sapi.Network.Received(1).RegisterChannel("test");
    }

    [Fact]
    public void Register_ReturnsSelf_ForChaining()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);

        var channel = new TypedNetworkChannel(sapi, "test");

        Assert.Same(channel, channel.Register());
    }

    // ─── AddMessageType ────────────────────────────────────────────

    [Fact]
    public void AddMessageType_OnServer_RegistersTypeOnServerChannel()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        channel.AddMessageType<TestPacket>();

        serverNetwork.Received(1).RegisterMessageType<TestPacket>();
    }

    [Fact]
    public void AddMessageType_OnClient_RegistersTypeOnClientChannel()
    {
        var capi = CreateClientApi();
        var clientNetwork = Substitute.For<IClientNetworkChannel>();
        capi.Network.RegisterChannel("test").Returns(clientNetwork);
        capi.Network.GetChannel("test").Returns((IClientNetworkChannel?)null);

        var channel = new TypedNetworkChannel(capi, "test");
        channel.Register();

        channel.AddMessageType<TestPacket>();

        clientNetwork.Received(1).RegisterMessageType<TestPacket>();
    }

    [Fact]
    public void AddMessageType_SameTypeTwice_RegistersOnlyOnce()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        channel.AddMessageType<TestPacket>();
        channel.AddMessageType<TestPacket>();

        serverNetwork.Received(1).RegisterMessageType<TestPacket>();
    }

    [Fact]
    public void AddMessageType_DifferentTypes_RegistersEachOnce()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        channel.AddMessageType<TestPacket>();
        channel.AddMessageType<OtherPacket>();

        serverNetwork.Received(1).RegisterMessageType<TestPacket>();
        serverNetwork.Received(1).RegisterMessageType<OtherPacket>();
    }

    [Fact]
    public void AddMessageType_ReturnsSelf_ForChaining()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        Assert.Same(channel, channel.AddMessageType<TestPacket>());
    }

    [Fact]
    public void AddMessageType_WithoutRegister_AutoCreatesChannel()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        // No explicit Register() call — AddMessageType should call EnsureChannel.
        channel.AddMessageType<TestPacket>();

        serverNetwork.Received(1).RegisterMessageType<TestPacket>();
    }

    // ─── OnServer ──────────────────────────────────────────────────

    [Fact]
    public void OnServer_RegistersMessageTypeAndHandler()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        Action<IServerPlayer, TestPacket> handler = (_, _) => { };

        channel.OnServer(handler);

        serverNetwork.Received(1).RegisterMessageType<TestPacket>();
        serverNetwork.Received(1).SetMessageHandler(Arg.Any<NetworkClientMessageHandler<TestPacket>>());
    }

    // ─── On (client) ───────────────────────────────────────────────

    [Fact]
    public void On_RegistersMessageTypeAndHandlerOnClient()
    {
        var capi = CreateClientApi();
        var clientNetwork = Substitute.For<IClientNetworkChannel>();
        capi.Network.RegisterChannel("test").Returns(clientNetwork);
        capi.Network.GetChannel("test").Returns((IClientNetworkChannel?)null);

        var channel = new TypedNetworkChannel(capi, "test");
        channel.Register();

        Action<TestPacket> handler = _ => { };

        channel.On(handler);

        clientNetwork.Received(1).RegisterMessageType<TestPacket>();
        clientNetwork.Received(1).SetMessageHandler(Arg.Any<NetworkServerMessageHandler<TestPacket>>());
    }

    // ─── Send ──────────────────────────────────────────────────────

    [Fact]
    public void Send_OnServer_WithOnlinePlayers_BroadcastsPacket()
    {
        var sapi = CreateServerApi(CreatePlayer("uid1"));
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var packet = new TestPacket { Value = 42 };
        channel.Send(packet);

        serverNetwork.Received(1).SendPacket(packet);
    }

    [Fact]
    public void Send_OnServer_WithNoOnlinePlayers_DoesNotBroadcast()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var packet = new TestPacket { Value = 42 };
        channel.Send(packet);

        serverNetwork.DidNotReceive().SendPacket(Arg.Any<TestPacket>());
    }

    [Fact]
    public void Send_OnClient_SendsPacketToServer()
    {
        var capi = CreateClientApi();
        var clientNetwork = Substitute.For<IClientNetworkChannel>();
        capi.Network.RegisterChannel("test").Returns(clientNetwork);
        capi.Network.GetChannel("test").Returns((IClientNetworkChannel?)null);

        var channel = new TypedNetworkChannel(capi, "test");
        channel.Register();

        var packet = new TestPacket { Value = 42 };
        channel.Send(packet);

        clientNetwork.Received(1).SendPacket(packet);
    }

    // ─── SendToPlayer ──────────────────────────────────────────────

    [Fact]
    public void SendToPlayer_WithValidPlayer_SendsPacket()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var player = CreatePlayer("uid1");
        var packet = new TestPacket { Value = 42 };
        channel.SendToPlayer(packet, player);

        serverNetwork.Received(1).SendPacket(packet, player);
    }

    [Fact]
    public void SendToPlayer_WithNullPlayer_DoesNotSend()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var packet = new TestPacket { Value = 42 };
        channel.SendToPlayer<TestPacket>(packet, null!);

        serverNetwork.DidNotReceive().SendPacket(Arg.Any<TestPacket>(), Arg.Any<IServerPlayer>());
    }

    // ─── SendToPlayers ─────────────────────────────────────────────

    [Fact]
    public void SendToPlayers_WithMultiplePlayers_SendsToEach()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var p1 = CreatePlayer("uid1");
        var p2 = CreatePlayer("uid2");
        var p3 = CreatePlayer("uid3");
        var packet = new TestPacket { Value = 42 };

        channel.SendToPlayers(packet, new[] { p1, p2, p3 });

        serverNetwork.Received(1).SendPacket(packet, p1);
        serverNetwork.Received(1).SendPacket(packet, p2);
        serverNetwork.Received(1).SendPacket(packet, p3);
    }

    [Fact]
    public void SendToPlayers_WithNullCollection_DoesNotSend()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var packet = new TestPacket { Value = 42 };
        channel.SendToPlayers<TestPacket>(packet, null!);

        serverNetwork.DidNotReceive().SendPacket(Arg.Any<TestPacket>(), Arg.Any<IServerPlayer>());
    }

    [Fact]
    public void SendToPlayers_WithNullEntriesInCollection_SkipsNulls()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var p1 = CreatePlayer("uid1");
        var packet = new TestPacket { Value = 42 };

        var playersWithNull = new List<IServerPlayer> { p1, null! };
        channel.SendToPlayers(packet, playersWithNull);

        serverNetwork.Received(1).SendPacket(packet, p1);
        serverNetwork.DidNotReceive().SendPacket(Arg.Any<TestPacket>(), Arg.Is<IServerPlayer>(x => x == null!));
    }

    [Fact]
    public void SendToPlayers_WithEmptyCollection_DoesNotSend()
    {
        var sapi = CreateServerApi();
        var serverNetwork = Substitute.For<IServerNetworkChannel>();
        sapi.Network.RegisterChannel("test").Returns(serverNetwork);
        sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

        var channel = new TypedNetworkChannel(sapi, "test");
        channel.Register();

        var packet = new TestPacket { Value = 42 };
        channel.SendToPlayers(packet, Array.Empty<IServerPlayer>());

        serverNetwork.DidNotReceive().SendPacket(Arg.Any<TestPacket>(), Arg.Any<IServerPlayer>());
    }

    // ─── SendToAllExcept ───────────────────────────────────────────

    [Fact]
    public void SendToAllExcept_OnClient_DoesNothing()
    {
        var capi = CreateClientApi();
        var clientNetwork = Substitute.For<IClientNetworkChannel>();
        capi.Network.RegisterChannel("test").Returns(clientNetwork);
        capi.Network.GetChannel("test").Returns((IClientNetworkChannel?)null);

        var channel = new TypedNetworkChannel(capi, "test");
        channel.Register();

        var packet = new TestPacket { Value = 42 };
        channel.SendToAllExcept(packet, null);

        clientNetwork.DidNotReceive().SendPacket(Arg.Any<TestPacket>());
    }

    [Fact]
    public void SendToAllExcept_OnServer_WithOnlinePlayers_SendsToAllExceptExcluded()
    {
        var p1 = CreatePlayer("uid1");
        var p2 = CreatePlayer("uid2");
        var p3 = CreatePlayer("uid3");
        var sapi = CreateServerApi(p1, p2, p3);

        // Initialize OnlinePlayerCache so SendToAllExcept can find players.
        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);
        try
        {
            var serverNetwork = Substitute.For<IServerNetworkChannel>();
            sapi.Network.RegisterChannel("test").Returns(serverNetwork);
            sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

            var channel = new TypedNetworkChannel(sapi, "test");
            channel.Register();

            var packet = new TestPacket { Value = 42 };
            channel.SendToAllExcept(packet, p2);

            serverNetwork.Received(1).SendPacket(packet, p1);
            serverNetwork.DidNotReceive().SendPacket(packet, p2);
            serverNetwork.Received(1).SendPacket(packet, p3);
        }
        finally
        {
            cache.Dispose();
        }
    }

    [Fact]
    public void SendToAllExcept_OnServer_WithNullExcept_SendsToAll()
    {
        var p1 = CreatePlayer("uid1");
        var p2 = CreatePlayer("uid2");
        var sapi = CreateServerApi(p1, p2);

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);
        try
        {
            var serverNetwork = Substitute.For<IServerNetworkChannel>();
            sapi.Network.RegisterChannel("test").Returns(serverNetwork);
            sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

            var channel = new TypedNetworkChannel(sapi, "test");
            channel.Register();

            var packet = new TestPacket { Value = 42 };
            channel.SendToAllExcept(packet, null);

            serverNetwork.Received(1).SendPacket(packet, p1);
            serverNetwork.Received(1).SendPacket(packet, p2);
        }
        finally
        {
            cache.Dispose();
        }
    }

    [Fact]
    public void SendToAllExcept_OnServer_WithNoOnlinePlayers_DoesNotSend()
    {
        var sapi = CreateServerApi();

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);
        try
        {
            var serverNetwork = Substitute.For<IServerNetworkChannel>();
            sapi.Network.RegisterChannel("test").Returns(serverNetwork);
            sapi.Network.GetChannel("test").Returns((IServerNetworkChannel?)null);

            var channel = new TypedNetworkChannel(sapi, "test");
            channel.Register();

            var packet = new TestPacket { Value = 42 };
            channel.SendToAllExcept(packet, null);

            serverNetwork.DidNotReceive().SendPacket(Arg.Any<TestPacket>(), Arg.Any<IServerPlayer>());
        }
        finally
        {
            cache.Dispose();
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private static ICoreServerAPI CreateServerApi(params IServerPlayer[] players)
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var world = Substitute.For<IServerWorldAccessor>();
        world.AllOnlinePlayers.Returns(players);
        sapi.World.Returns(world);

        var network = Substitute.For<IServerNetworkAPI>();
        sapi.Network.Returns(network);

        var logger = Substitute.For<ILogger>();
        sapi.Logger.Returns(logger);

        sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1);

        return sapi;
    }

    private static ICoreClientAPI CreateClientApi()
    {
        var capi = Substitute.For<ICoreClientAPI>();
        var network = Substitute.For<IClientNetworkAPI>();
        capi.Network.Returns(network);

        var logger = Substitute.For<ILogger>();
        capi.Logger.Returns(logger);

        return capi;
    }

    private static IServerPlayer CreatePlayer(string uid)
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns(uid);
        return player;
    }

    private sealed class TestPacket
    {
        public int Value { get; set; }
    }

    private sealed class OtherPacket
    {
        public string? Text { get; set; }
    }
}
