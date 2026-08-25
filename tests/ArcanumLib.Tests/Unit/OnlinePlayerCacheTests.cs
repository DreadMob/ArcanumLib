using System.Reflection;
using ArcanumLib.Common;
using ArcanumLib.Core;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class OnlinePlayerCacheTests : IDisposable
{
    public OnlinePlayerCacheTests()
    {
        ArcanumRuntime.Activate();
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void StartServerSide_PopulatesFromOnlinePlayers()
    {
        var player = CreatePlayer("uid1");
        var sapi = CreateServerApi(player);

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);

        Assert.Equal(1, cache.Count);
        Assert.Same(player, cache.GetByUid("uid1"));
        Assert.True(cache.IsLoaded);

        cache.Dispose();
    }

    [Fact]
    public void Rebuild_ReflectsChangesInOnlinePlayers()
    {
        var player1 = CreatePlayer("uid1");
        var sapi = CreateServerApi(player1);

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);

        Assert.Single(cache.All);

        var player2 = CreatePlayer("uid2");
        sapi.World.AllOnlinePlayers.Returns(new IPlayer[] { player1, player2 });

        cache.GetType().GetMethod("Rebuild", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(cache, null);

        Assert.Equal(2, cache.Count);
        Assert.NotNull(cache.GetByUid("uid2"));

        cache.Dispose();
    }

    [Fact]
    public void Dispose_ClearsCache()
    {
        var sapi = CreateServerApi(CreatePlayer("uid1"));

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);
        Assert.Equal(1, cache.Count);

        cache.Dispose();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.IsLoaded);
        Assert.Null(cache.GetByUid("uid1"));
    }

    [Fact]
    public void GetByUid_EmptyUid_ReturnsNull()
    {
        var cache = new OnlinePlayerCache();
        Assert.Null(cache.GetByUid(""));
        Assert.Null(cache.GetByUid("   "));
    }

    [Fact]
    public void FreshCache_HasEmptyState()
    {
        var cache = new OnlinePlayerCache();
        Assert.Equal(0, cache.Count);
        Assert.Empty(cache.All);
        Assert.Empty(cache.ByUid);
        Assert.False(cache.IsLoaded);
        Assert.Null(cache.GetByUid("anyone"));
    }

    private static IServerPlayer CreatePlayer(string uid)
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns(uid);
        return player;
    }

    private static ICoreServerAPI CreateServerApi(params IServerPlayer[] players)
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var world = Substitute.For<IServerWorldAccessor>();
        world.AllOnlinePlayers.Returns(players);
        sapi.World.Returns(world);
        sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1);
        return sapi;
    }
}
