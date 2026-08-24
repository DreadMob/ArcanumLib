using ArcanumLib.Data;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class CooldownTrackerTests
{
    [Fact]
    public void IsReady_WhenNeverStarted_ReturnsTrue()
    {
        var entity = CreateEntity(elapsedMs: 0);

        Assert.True(entity.IsReady("mymod:test", 5.0));
    }

    [Fact]
    public void MarkCooldownStart_ThenIsReady_ReturnsFalseBeforeDuration()
    {
        var entity = CreateEntity(elapsedMs: 1000);

        entity.MarkCooldownStart("mymod:test");
        Assert.False(entity.IsReady("mymod:test", 5.0));
    }

    [Fact]
    public void IsReady_AfterDuration_ReturnsTrue()
    {
        var entity = CreateEntity(elapsedMs: 0);

        entity.MarkCooldownStart("mymod:test");

        entity.Api.World.ElapsedMilliseconds.Returns(6000);

        Assert.True(entity.IsReady("mymod:test", 5.0));
    }

    [Fact]
    public void GetRemainingCooldownMs_ReturnsExpected()
    {
        var entity = CreateEntity(elapsedMs: 1000);

        entity.MarkCooldownStart("mymod:test");
        entity.Api.World.ElapsedMilliseconds.Returns(2000);

        Assert.Equal(4000, entity.GetRemainingCooldownMs("mymod:test", 5.0));
    }

    [Fact]
    public void ResetCooldown_MakesReady()
    {
        var entity = CreateEntity(elapsedMs: 1000);

        entity.MarkCooldownStart("mymod:test");
        Assert.False(entity.IsReady("mymod:test", 5.0));

        entity.ResetCooldown("mymod:test");
        Assert.True(entity.IsReady("mymod:test", 5.0));
    }

    [Fact]
    public void StaleLastStartMs_ResetsAndReturnsReady()
    {
        var entity = CreateEntity(elapsedMs: 0);
        entity.WatchedAttributes.SetLong("mymod:test", 5000);

        Assert.True(entity.IsReady("mymod:test", 5.0));
        Assert.Equal(0, entity.WatchedAttributes.GetLong("mymod:test"));
    }

    [Fact]
    public void Multiplier_AppliesToDuration()
    {
        var entity = CreateEntity(elapsedMs: 1000);

        entity.MarkCooldownStart("mymod:test");
        entity.Api.World.ElapsedMilliseconds.Returns(4000);

        Assert.False(entity.IsReady("mymod:test", 5.0, multiplier: 2.0));
        Assert.Equal(7000, entity.GetRemainingCooldownMs("mymod:test", 5.0, multiplier: 2.0));
    }

    private static Entity CreateEntity(long elapsedMs)
    {
        var api = Substitute.For<ICoreAPI>();
        var world = Substitute.For<IWorldAccessor>();
        world.ElapsedMilliseconds.Returns(elapsedMs);
        api.World.Returns(world);

        var entity = new DummyEntity();
        entity.Api = api;
        entity.WatchedAttributes = new SyncedTreeAttribute();
        return entity;
    }

    private sealed class DummyEntity : Entity
    {
        public DummyEntity()
        {
            Class = "dummy";
        }
    }
}
