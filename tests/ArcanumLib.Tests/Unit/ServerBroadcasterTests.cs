using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Network;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ServerBroadcasterTests
{
    [Fact]
    public void BroadcastPacket_NullSapi_DoesNothing()
    {
        var channel = Substitute.For<IServerNetworkChannel>();
        ServerBroadcaster.BroadcastPacket(null!, channel, "msg");
        channel.DidNotReceive().SendPacket(Arg.Any<string>(), Arg.Any<IServerPlayer>());
    }

    [Fact]
    public void BroadcastPacket_NullChannel_DoesNothing()
    {
        var sapi = CreateSapi();
        ServerBroadcaster.BroadcastPacket(sapi, null!, "msg");
    }

    [Fact]
    public void BroadcastPacket_NoPlayers_DoesNothing()
    {
        var sapi = CreateSapi();
        var channel = Substitute.For<IServerNetworkChannel>();
        sapi.World.AllOnlinePlayers.Returns(new IPlayer[0]);

        ServerBroadcaster.BroadcastPacket(sapi, channel, "msg");

        channel.DidNotReceive().SendPacket(Arg.Any<string>(), Arg.Any<IServerPlayer>());
    }

    [Fact]
    public void BroadcastPacket_SendsToAllServerPlayers()
    {
        var sapi = CreateSapi();
        var channel = Substitute.For<IServerNetworkChannel>();
        var players = new[] { CreatePlayer("a", 1, 0, 0), CreatePlayer("b", 2, 0, 0) };
        sapi.World.AllOnlinePlayers.Returns(players.Cast<IPlayer>().ToArray());

        ServerBroadcaster.BroadcastPacket(sapi, channel, "msg");

        channel.Received().SendPacket("msg", players[0]);
        channel.Received().SendPacket("msg", players[1]);
    }

    [Fact]
    public void BroadcastPacket_Predicate_SendsOnlyMatching()
    {
        var sapi = CreateSapi();
        var channel = Substitute.For<IServerNetworkChannel>();
        var players = new[] { CreatePlayer("a", 1, 0, 0), CreatePlayer("b", 2, 0, 0) };
        sapi.World.AllOnlinePlayers.Returns(players.Cast<IPlayer>().ToArray());

        ServerBroadcaster.BroadcastPacket(sapi, channel, "msg", p => p.PlayerUID == "b");

        channel.DidNotReceive().SendPacket("msg", players[0]);
        channel.Received().SendPacket("msg", players[1]);
    }

    [Fact]
    public void BroadcastPacketInRange_SendsOnlyWithinRadius()
    {
        var sapi = CreateSapi();
        var channel = Substitute.For<IServerNetworkChannel>();
        var near = CreatePlayer("near", 3, 0, 4); // distance 5
        var far = CreatePlayer("far", 100, 0, 0);
        sapi.World.AllOnlinePlayers.Returns(new[] { near, far }.Cast<IPlayer>().ToArray());

        ServerBroadcaster.BroadcastPacketInRange(sapi, channel, "msg", 0, 0, 0, 5);

        channel.Received().SendPacket("msg", near);
        channel.DidNotReceive().SendPacket("msg", far);
    }

    [Fact]
    public void BroadcastPacketInRange_ZeroRadius_DoesNothing()
    {
        var sapi = CreateSapi();
        var channel = Substitute.For<IServerNetworkChannel>();
        var near = CreatePlayer("near", 1, 0, 0);
        sapi.World.AllOnlinePlayers.Returns(new[] { near }.Cast<IPlayer>().ToArray());

        ServerBroadcaster.BroadcastPacketInRange(sapi, channel, "msg", 0, 0, 0, 0);

        channel.DidNotReceive().SendPacket("msg", near);
    }

    [Fact]
    public void BroadcastPacketExcept_SkipsExcluded()
    {
        var sapi = CreateSapi();
        var channel = Substitute.For<IServerNetworkChannel>();
        var a = CreatePlayer("a", 1, 0, 0);
        var b = CreatePlayer("b", 2, 0, 0);
        sapi.World.AllOnlinePlayers.Returns(new[] { a, b }.Cast<IPlayer>().ToArray());

        ServerBroadcaster.BroadcastPacketExcept(sapi, channel, "msg", new[] { "a" });

        channel.DidNotReceive().SendPacket("msg", a);
        channel.Received().SendPacket("msg", b);
    }

    [Fact]
    public void BroadcastPacketToGroup_DelegatesToPredicate()
    {
        var sapi = CreateSapi();
        var channel = Substitute.For<IServerNetworkChannel>();
        var a = CreatePlayer("a", 1, 0, 0);
        sapi.World.AllOnlinePlayers.Returns(new[] { a }.Cast<IPlayer>().ToArray());

        ServerBroadcaster.BroadcastPacketToGroup(sapi, channel, "msg", _ => true);

        channel.Received().SendPacket("msg", a);
    }

    private static ICoreServerAPI CreateSapi()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var world = Substitute.For<IServerWorldAccessor>();
        sapi.World.Returns(world);
        return sapi;
    }

    private static IServerPlayer CreatePlayer(string uid, double x, double y, double z)
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns(uid);

        var entity = new EntityPlayer();
        entity.Pos.X = x;
        entity.Pos.Y = y;
        entity.Pos.Z = z;
        entity.EntityId = 1;

        player.Entity.Returns(entity);
        return player;
    }
}
