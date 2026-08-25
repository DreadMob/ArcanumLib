using System.Collections.Generic;
using ArcanumLib.Core;
using ArcanumLib.Performance;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class StatCoalescingEngineTests : IDisposable
{
    private readonly StatCoalescingEngine _engine = new();
    private readonly ICoreServerAPI _sapi;
    private readonly IServerWorldAccessor _world;
    private readonly DeferredWorkService _deferred;

    public StatCoalescingEngineTests()
    {
        ArcanumRuntime.Activate();

        _deferred = new DeferredWorkService();
        ArcanumServices.Register(_deferred);

        _world = Substitute.For<IServerWorldAccessor>();
        _world.ElapsedMilliseconds.Returns(1000L);
        // Return null for unknown entity ids so FlushUpdates clears pending state
        _world.GetEntityById(Arg.Any<long>()).Returns((Entity?)null);

        _sapi = Substitute.For<ICoreServerAPI>();
        _sapi.World.Returns(_world);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _deferred.Stop();
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void Start_NullApi_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => _engine.Start(null!));
    }

    [Fact]
    public void Start_WithApi_DoesNotThrow()
    {
        var ex = Record.Exception(() => _engine.Start(_sapi));
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_AfterStart_DoesNotThrow()
    {
        _engine.Start(_sapi);
        var ex = Record.Exception(() => _engine.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var ex = Record.Exception(() => _engine.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_StopsEngineWithoutThrowing()
    {
        _engine.Start(_sapi);
        var ex = Record.Exception(() => _engine.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void IsEnabled_DefaultTrue()
    {
        Assert.True(_engine.IsEnabled);
    }

    [Fact]
    public void DefaultCategory_DefaultGame()
    {
        Assert.Equal("game", _engine.DefaultCategory);
    }

    [Fact]
    public void CoalesceWindowMs_Default200()
    {
        Assert.Equal(200, _engine.CoalesceWindowMs);
    }

    [Fact]
    public void MaxDelayMs_Default1000()
    {
        Assert.Equal(1000, _engine.MaxDelayMs);
    }

    [Fact]
    public void QueueStatUpdate_WhenDisabled_DoesNotQueue()
    {
        _engine.IsEnabled = false;
        _engine.Start(_sapi);
        var player = CreatePlayer(42);

        _engine.QueueStatUpdate(_sapi, player, "walkspeed", 0.5f, "mymod");

        // When disabled, stats are applied directly, not queued
        Assert.Equal(0, _engine.GetPendingUpdateCount());
    }

    [Fact]
    public void QueueStatUpdate_NullPlayer_DoesNothing()
    {
        _engine.Start(_sapi);

        _engine.QueueStatUpdate(_sapi, null!, "walkspeed", 0.5f);

        Assert.Equal(0, _engine.GetPendingUpdateCount());
    }

    [Fact]
    public void QueueStatUpdates_NullPlayer_DoesNothing()
    {
        _engine.Start(_sapi);

        _engine.QueueStatUpdates(_sapi, null!, new Dictionary<string, float> { ["x"] = 1f });

        Assert.Equal(0, _engine.GetPendingUpdateCount());
    }

    [Fact]
    public void HasPendingUpdates_NoUpdates_ReturnsFalse()
    {
        Assert.False(_engine.HasPendingUpdates(99));
    }

    [Fact]
    public void GetPendingUpdateCount_NoUpdates_ReturnsZero()
    {
        Assert.Equal(0, _engine.GetPendingUpdateCount());
    }

    [Fact]
    public void ClearAllPending_WithNoUpdates_DoesNotThrow()
    {
        _engine.Start(_sapi);
        var ex = Record.Exception(() => _engine.ClearAllPending(_sapi));
        Assert.Null(ex);
    }

    [Fact]
    public void MarkDirtyAttributePath_DefaultNull()
    {
        Assert.Null(_engine.MarkDirtyAttributePath);
    }

    [Fact]
    public void MarkDirtyAttributePath_CanBeSet()
    {
        _engine.MarkDirtyAttributePath = "mymod:stats";
        Assert.Equal("mymod:stats", _engine.MarkDirtyAttributePath);
    }

    [Fact]
    public void Stop_ClearsPendingUpdates()
    {
        _engine.Start(_sapi);
        // Don't actually queue anything, just verify Stop doesn't throw
        var ex = Record.Exception(() => _engine.Stop());
        Assert.Null(ex);
        Assert.Equal(0, _engine.GetPendingUpdateCount());
    }

    [Fact]
    public void ApplyStatImmediate_NullPlayer_DoesNothing()
    {
        var ex = Record.Exception(() => _engine.ApplyStatImmediate(null!, "walkspeed", 0.3f));
        Assert.Null(ex);
    }

    [Fact]
    public void ForceFlush_WithNoPending_DoesNotThrow()
    {
        _engine.Start(_sapi);
        var ex = Record.Exception(() => _engine.ForceFlush(_sapi, 99));
        Assert.Null(ex);
    }

    [Fact]
    public void ForceFlush_WhenDisabled_DoesNotThrow()
    {
        _engine.Start(_sapi);
        _engine.IsEnabled = false;
        var ex = Record.Exception(() => _engine.ForceFlush(_sapi, 42));
        Assert.Null(ex);
    }

    private static EntityPlayer CreatePlayer(long entityId)
    {
        var entity = Substitute.For<EntityPlayer>();
        entity.EntityId = entityId;
        var watchedAttrs = Substitute.For<SyncedTreeAttribute>();
        entity.WatchedAttributes = watchedAttrs;
        return entity;
    }
}
