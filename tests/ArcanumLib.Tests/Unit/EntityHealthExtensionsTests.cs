using ArcanumLib.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class EntityHealthExtensionsTests
{
    [Fact]
    public void GetHealthPercent_TopLevelAttributes_ReturnsFraction()
    {
        var entity = CreateEntity(wa =>
        {
            wa.SetFloat("health", 50f);
            wa.SetFloat("maxHealth", 100f);
        });

        Assert.Equal(0.5f, entity.GetHealthPercent());
    }

    [Fact]
    public void GetHealthPercent_HealthTree_Fallback()
    {
        var entity = CreateEntity(wa =>
        {
            var health = new TreeAttribute();
            health.SetFloat("currenthealth", 25f);
            health.SetFloat("maxhealth", 50f);
            wa["health"] = health;
        });

        Assert.Equal(0.5f, entity.GetHealthPercent());
    }

    [Fact]
    public void GetHealthPercent_MissingData_ReturnsDefault()
    {
        var entity = CreateEntity(_ => { });

        Assert.Equal(0.1f, entity.GetHealthPercent(0.1f));
    }

    [Fact]
    public void TryGetHealthFraction_HealthTree_ReturnsTrue()
    {
        var entity = CreateEntity(wa =>
        {
            var health = new TreeAttribute();
            health.SetFloat("currenthealth", 30f);
            health.SetFloat("basemaxhealth", 60f);
            wa["health"] = health;
        });

        Assert.True(entity.TryGetHealthFraction(out float fraction));
        Assert.Equal(0.5f, fraction);
    }

    [Fact]
    public void TryGetHealthFraction_MissingTree_ReturnsFalse()
    {
        var entity = CreateEntity(_ => { });

        Assert.False(entity.TryGetHealthFraction(out _));
    }

    [Fact]
    public void TryGetHealth_ReturnsValues()
    {
        var entity = CreateEntity(wa =>
        {
            var health = new TreeAttribute();
            health.SetFloat("currenthealth", 20f);
            health.SetFloat("maxhealth", 40f);
            wa["health"] = health;
        });

        Assert.True(entity.TryGetHealth(out var tree, out float current, out float max));
        Assert.NotNull(tree);
        Assert.Equal(20f, current);
        Assert.Equal(40f, max);
    }

    [Fact]
    public void ScaleHealth_DoublesValues()
    {
        var entity = CreateEntity(wa =>
        {
            var health = new TreeAttribute();
            health.SetFloat("currenthealth", 20f);
            health.SetFloat("maxhealth", 40f);
            wa["health"] = health;
        });

        Assert.True(entity.ScaleHealth(2f));

        Assert.True(entity.TryGetHealth(out _, out float current, out float max));
        Assert.Equal(80f, current);
        Assert.Equal(80f, max);
    }

    [Fact]
    public void ScaleHealth_MultOneOrLess_ReturnsFalse()
    {
        var entity = CreateEntity(wa =>
        {
            var health = new TreeAttribute();
            health.SetFloat("maxhealth", 40f);
            wa["health"] = health;
        });

        Assert.False(entity.ScaleHealth(1f));
        Assert.False(entity.ScaleHealth(0f));
        Assert.False(entity.ScaleHealth(-1f));
    }

    private static Entity CreateEntity(System.Action<SyncedTreeAttribute> setup)
    {
        var wa = new SyncedTreeAttribute();
        setup(wa);

        var entity = new DummyEntity();
        entity.WatchedAttributes = wa;
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
