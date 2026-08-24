using ArcanumLib.Spatial;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.AtlasTests;

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class PlayerProximityTrackerTests : AtlasScenarioBase
{
    [AtlasScenario]
    public async Task ProximityTracker_Fires_Enter_And_Exit()
    {
        var tracker = World.Api.ModLoader.GetModSystem<PlayerProximityTracker>();
        Assert.NotNull(tracker);

        var listener = new TestListener(World.Spawn, radius: 10f);
        tracker.Register(listener);

        await World.Ticks(5);

        var player = await World.JoinPlayer("testproximity");
        await player.TeleportTo(World.Spawn);

        await World.Until(() => listener.EnteredCount > 0, timeoutTicks: 120);

        Assert.Equal(1, listener.EnteredCount);

        await player.TeleportTo(World.Spawn.UpCopy().AddCopy(50, 0, 0));

        await World.Until(() => listener.ExitedCount > 0, timeoutTicks: 120);

        Assert.Equal(1, listener.ExitedCount);

        tracker.Unregister(listener);
    }

    private sealed class TestListener : IPlayerProximityListener
    {
        public BlockPos Position { get; }
        public float Radius { get; }

        public int EnteredCount { get; private set; }
        public int StayedCount { get; private set; }
        public int ExitedCount { get; private set; }

        public TestListener(BlockPos pos, float radius)
        {
            Position = pos;
            Radius = radius;
        }

        public void OnPlayerEntered(IServerPlayer player) => EnteredCount++;
        public void OnPlayerStayed(IServerPlayer player) => StayedCount++;
        public void OnPlayerExited(IServerPlayer player) => ExitedCount++;
    }
}
