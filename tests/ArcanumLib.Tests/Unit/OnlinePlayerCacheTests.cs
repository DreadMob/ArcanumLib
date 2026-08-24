using ArcanumLib.Common;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class OnlinePlayerCacheTests
{
    public OnlinePlayerCacheTests()
    {
        new OnlinePlayerCache().Dispose();
    }

    [Fact]
    public void StartServerSide_PopulatesFromOnlinePlayers()
    {
        var player = CreatePlayer("uid1");
        var sapi = CreateServerApi(player);

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);

        Assert.Equal(1, OnlinePlayerCache.Count);
        Assert.Same(player, OnlinePlayerCache.GetByUid("uid1"));
        Assert.True(OnlinePlayerCache.IsLoaded);

        cache.Dispose();
    }

    [Fact]
    public void Rebuild_ReflectsChangesInOnlinePlayers()
    {
        var player1 = CreatePlayer("uid1");
        var sapi = CreateServerApi(player1);

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);

        Assert.Single(OnlinePlayerCache.All);

        var player2 = CreatePlayer("uid2");
        sapi.World.AllOnlinePlayers.Returns(new IPlayer[] { player1, player2 });

        cache.GetType().GetMethod("Rebuild", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null);

        Assert.Equal(2, OnlinePlayerCache.Count);
        Assert.NotNull(OnlinePlayerCache.GetByUid("uid2"));

        cache.Dispose();
    }

    [Fact]
    public void Dispose_ClearsCache()
    {
        var sapi = CreateServerApi(CreatePlayer("uid1"));

        var cache = new OnlinePlayerCache();
        cache.StartServerSide(sapi);
        Assert.Equal(1, OnlinePlayerCache.Count);

        cache.Dispose();

        Assert.Equal(0, OnlinePlayerCache.Count);
        Assert.False(OnlinePlayerCache.IsLoaded);
        Assert.Null(OnlinePlayerCache.GetByUid("uid1"));
    }

    [Fact]
    public void GetByUid_EmptyUid_ReturnsNull()
    {
        Assert.Null(OnlinePlayerCache.GetByUid(""));
        Assert.Null(OnlinePlayerCache.GetByUid("   "));
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
