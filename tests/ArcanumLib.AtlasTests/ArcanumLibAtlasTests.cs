using ArcanumLib.Common;
using ArcanumLib.Core;
using ArcanumLib.Data;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.AtlasTests;

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class ArcanumLibAtlasTests : AtlasScenarioBase
{
    [AtlasScenario]
    public async Task Mod_Loads_And_Registers_Api()
    {
        await World.Ticks(5);

        var sapi = ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server);
        Assert.NotNull(sapi);
        Assert.Same(World.Api, sapi);

        var api = ArcanumServices.Get<ICoreAPI>(ArcanumServiceScope.Server);
        Assert.NotNull(api);
    }

    [AtlasScenario]
    public async Task OnlinePlayerCache_Tracks_Joined_Player()
    {
        await World.Ticks(5);

        var player = await World.JoinPlayer("testcache");
        await World.Ticks(5);

        var cache = ArcanumServices.Get<IOnlinePlayerCache>();
        Assert.NotNull(cache);
        Assert.True(cache!.IsLoaded);
        Assert.Equal(1, cache.Count);
        Assert.NotNull(cache.GetByUid(player.Player.PlayerUID));
        Assert.Same(player.Player, cache.GetByUid(player.Player.PlayerUID));
    }

    [AtlasScenario]
    public async Task OnlinePlayerCache_Excludes_Disconnected_Player()
    {
        var player = await World.JoinPlayer("testcache2");
        await World.Ticks(5);

        var cache = ArcanumServices.Get<IOnlinePlayerCache>();
        Assert.NotNull(cache);

        Assert.Contains(cache!.All, p => p.PlayerUID == player.Player.PlayerUID);

        player.Player.Disconnect("test");
        await World.Until(() => !player.IsConnected, timeoutTicks: 120);

        Assert.DoesNotContain(cache.All, p => p.PlayerUID == player.Player.PlayerUID);
    }

    [AtlasScenario]
    public async Task TagMatcher_Matches_Block_Code_Pattern()
    {
        var pos = World.Spawn.UpCopy();
        World.SetBlock("game:chest-east", pos);
        await World.Ticks(5);

        var block = World.BlockAt(pos);
        Assert.NotNull(block);

        var matcher = new TagMatcher().AddCodePattern("game:chest-*");
        Assert.True(matcher.Matches(block));

        var nonMatcher = new TagMatcher().AddCodePattern("game:soil-*");
        Assert.False(nonMatcher.Matches(block));
    }

    [AtlasScenario]
    public async Task CooldownTracker_Tracks_Ready_State()
    {
        var player = await World.JoinPlayer("testcooldown");
        await World.Ticks(5);

        var entity = player.Entity;
        Assert.NotNull(entity);

        const string key = "arcanumlib:test:cooldown";

        entity.MarkCooldownStart(key);

        Assert.False(entity.IsReady(key, 5.0));
        Assert.True(entity.GetRemainingCooldownMs(key, 5.0) > 0);

        await World.Ticks(20);

        Assert.True(entity.IsReady(key, 0.05));
        Assert.Equal(0, entity.GetRemainingCooldownMs(key, 0.05));
    }
}
