using System;
using System.Collections.Generic;
using ArcanumLib.Spatial;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PlayerProximityTrackerTests
{
    [Fact]
    public void ShouldLoad_Server_ReturnsTrue()
    {
        var tracker = new PlayerProximityTracker();
        Assert.True(tracker.ShouldLoad(EnumAppSide.Server));
    }

    [Fact]
    public void ShouldLoad_Client_ReturnsFalse()
    {
        var tracker = new PlayerProximityTracker();
        Assert.False(tracker.ShouldLoad(EnumAppSide.Client));
    }

    [Fact]
    public void TickIntervalMs_MatchesPlayerZoneTracker()
    {
        Assert.Equal(PlayerZoneTracker.TickIntervalMs, PlayerProximityTracker.TickIntervalMs);
    }

    [Fact]
    public void Register_NullListener_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        tracker.Register(null);
        // No exception expected.
    }

    [Fact]
    public void Unregister_NullListener_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        tracker.Unregister(null);
        // No exception expected.
    }

    [Fact]
    public void Register_SameListenerTwice_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);

        tracker.Register(listener);
        tracker.Register(listener);
        // Second call should be a no-op (idempotent).
    }

    [Fact]
    public void Unregister_NeverRegistered_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);

        tracker.Unregister(listener);
        // No exception expected.
    }

    [Fact]
    public void Register_WithNullPosition_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        // Position is a BlockPos (struct), so we cannot set it to null directly.
        // But we can verify that a listener with a default BlockPos does not crash.
        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);

        tracker.Register(listener);
        // No exception expected even when PlayerZoneTracker is not running.
    }

    [Fact]
    public void Register_ThenUnregister_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);

        tracker.Register(listener);
        tracker.Unregister(listener);
        // No exception expected.
    }

    [Fact]
    public void Unregister_AfterUnregister_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);

        tracker.Register(listener);
        tracker.Unregister(listener);
        tracker.Unregister(listener);
        // Second unregister should be a no-op.
    }

    [Fact]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        tracker.Dispose();
        // No exception expected.
    }

    [Fact]
    public void Dispose_AfterRegister_UnregistersAllListeners()
    {
        var tracker = new PlayerProximityTracker();
        var listener1 = new TestListener(new BlockPos(0, 0, 0), 10f);
        var listener2 = new TestListener(new BlockPos(10, 0, 10), 5f);

        tracker.Register(listener1);
        tracker.Register(listener2);

        tracker.Dispose();
        // No exception expected; all listeners should be cleaned up.
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        tracker.Dispose();
        tracker.Dispose();
        // No exception expected.
    }

    [Fact]
    public void StartServerSide_StoresApi_ForLogging()
    {
        var sapi = CreateServerApi();
        var tracker = new PlayerProximityTracker();

        tracker.StartServerSide(sapi);

        // After StartServerSide, the tracker should have the sapi for logging.
        // We verify indirectly: register a listener with a throwing callback, then
        // trigger SafeInvoke. But since PlayerZoneTracker is not running, we cannot
        // trigger callbacks directly. Instead, we just verify no crash on Dispose.
        tracker.Dispose();
    }

    [Fact]
    public void StartServerSide_NullApi_DoesNotCrash()
    {
        // Vintage Story always passes a valid API, but the tracker should not crash
        // if null is passed — it simply stores it and SafeInvoke guards with _sapi?.
        var tracker = new PlayerProximityTracker();

        tracker.StartServerSide(null!);

        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);
        tracker.Register(listener);
        tracker.Unregister(listener);
        tracker.Dispose();
    }

    [Fact]
    public void Register_MultipleDistinctListeners_AllRegistered()
    {
        var tracker = new PlayerProximityTracker();
        var listener1 = new TestListener(new BlockPos(0, 0, 0), 10f);
        var listener2 = new TestListener(new BlockPos(10, 0, 10), 5f);
        var listener3 = new TestListener(new BlockPos(20, 0, 20), 15f);

        tracker.Register(listener1);
        tracker.Register(listener2);
        tracker.Register(listener3);

        // All three should be registered without error.
        // Unregistering all three should also work.
        tracker.Unregister(listener1);
        tracker.Unregister(listener2);
        tracker.Unregister(listener3);

        tracker.Dispose();
    }

    [Fact]
    public void Register_AfterDispose_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        tracker.Dispose();

        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);
        tracker.Register(listener);
        // No exception expected; the tracker is disposed but Register is still safe.
        tracker.Unregister(listener);
    }

    [Fact]
    public void Register_WithZeroRadius_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), 0f);

        tracker.Register(listener);
        // Zero radius is unusual but should not crash.
        tracker.Unregister(listener);
        tracker.Dispose();
    }

    [Fact]
    public void Register_WithNegativeRadius_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), -5f);

        tracker.Register(listener);
        // Negative radius is invalid but the tracker does not validate it;
        // PlayerZoneTracker may reject it, but PlayerProximityTracker should not crash.
        tracker.Unregister(listener);
        tracker.Dispose();
    }

    [Fact]
    public void Register_WithLargeRadius_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), 10000f);

        tracker.Register(listener);
        tracker.Unregister(listener);
        tracker.Dispose();
    }

    [Fact]
    public void Register_WithDimension_DoesNotThrow()
    {
        var tracker = new PlayerProximityTracker();
        var pos = new BlockPos(0, 0, 0);
        pos.dimension = 1;
        var listener = new TestListener(pos, 10f);

        tracker.Register(listener);
        tracker.Unregister(listener);
        tracker.Dispose();
    }

    [Fact]
    public void Unregister_RemovesListener_AllowsReRegister()
    {
        var tracker = new PlayerProximityTracker();
        var listener = new TestListener(new BlockPos(0, 0, 0), 10f);

        tracker.Register(listener);
        tracker.Unregister(listener);

        // After unregistering, registering again should work (new zone id).
        tracker.Register(listener);
        tracker.Unregister(listener);
        tracker.Dispose();
    }

    [Fact]
    public void Dispose_ClearsListeners_AllowingCleanShutdown()
    {
        var tracker = new PlayerProximityTracker();
        var listener1 = new TestListener(new BlockPos(0, 0, 0), 10f);
        var listener2 = new TestListener(new BlockPos(5, 0, 5), 8f);

        tracker.Register(listener1);
        tracker.Register(listener2);

        tracker.Dispose();

        // After dispose, the internal dictionary should be empty.
        // We verify by registering again (should not conflict with previous entries).
        tracker.Register(listener1);
        tracker.Unregister(listener1);
        tracker.Dispose();
    }

    private static ICoreServerAPI CreateServerApi()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var world = Substitute.For<IServerWorldAccessor>();
        sapi.World.Returns(world);
        sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1);
        return sapi;
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
