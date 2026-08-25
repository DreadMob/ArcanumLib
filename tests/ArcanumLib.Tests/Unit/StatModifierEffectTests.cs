using ArcanumLib.Effects;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class StatModifierEffectTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var effect = new StatModifierEffect("haste", "walkspeed", 0.2f);

        Assert.Equal("haste", effect.Code);
        Assert.Equal("walkspeed", effect.StatCategory);
        Assert.Equal(0.2f, effect.Value);
        Assert.Equal(EnumStackMode.Refresh, effect.StackMode);
        Assert.Equal(1, effect.MaxStacks);
        Assert.False(effect.PersistThroughDeath);
        Assert.False(effect.HasTick);
    }

    [Fact]
    public void OnApply_NullEntity_DoesNothing()
    {
        var effect = new StatModifierEffect("haste", "walkspeed", 0.2f);
        var instance = CreateInstance("haste", stackCount: 1);

        effect.OnApply(null!, instance);
    }

    [Fact]
    public void OnApply_NullStats_DoesNothing()
    {
        var effect = new StatModifierEffect("haste", "walkspeed", 0.2f);
        var entity = new DummyEntity();
        var instance = CreateInstance("haste", stackCount: 1);

        effect.OnApply(entity, instance);
    }

    [Fact]
    public void OnApply_RefreshMode_AppliesValueToBlendedStat()
    {
        var entity = new DummyEntity();
        entity.Stats = new EntityStats(entity);
        var effect = new StatModifierEffect("haste", "walkspeed", 0.25f);
        var instance = CreateInstance("haste", stackCount: 1);

        effect.OnApply(entity, instance);

        Assert.Equal(1.25f, entity.Stats.GetBlended("walkspeed"), 4);
    }

    [Fact]
    public void OnApply_StackMode_MultipliesByStackCount()
    {
        var entity = new DummyEntity();
        entity.Stats = new EntityStats(entity);
        var effect = new StatModifierEffect("haste", "walkspeed", 0.1f)
        { StackMode = EnumStackMode.Stack };
        var instance = CreateInstance("haste", stackCount: 3);

        effect.OnApply(entity, instance);

        Assert.Equal(1.3f, entity.Stats.GetBlended("walkspeed"), 4);
    }

    [Fact]
    public void OnApply_IndependentMode_UsesUniqueIdKey()
    {
        var entity = new DummyEntity();
        entity.Stats = new EntityStats(entity);
        var effect = new StatModifierEffect("haste", "walkspeed", 0.1f)
        { StackMode = EnumStackMode.Independent };
        var instance = CreateInstance("haste", id: 42, stackCount: 1);

        effect.OnApply(entity, instance);

        Assert.Equal(1.1f, entity.Stats.GetBlended("walkspeed"), 4);
    }

    [Fact]
    public void OnApply_IndependentMode_MultipleInstancesStack()
    {
        var entity = new DummyEntity();
        entity.Stats = new EntityStats(entity);
        var effect = new StatModifierEffect("haste", "walkspeed", 0.1f)
        { StackMode = EnumStackMode.Independent };

        effect.OnApply(entity, CreateInstance("haste", id: 1, stackCount: 1));
        effect.OnApply(entity, CreateInstance("haste", id: 2, stackCount: 1));

        Assert.Equal(1.2f, entity.Stats.GetBlended("walkspeed"), 4);
    }

    [Fact]
    public void OnRemove_RefreshMode_RemovesModifier()
    {
        var entity = new DummyEntity();
        entity.Stats = new EntityStats(entity);
        var effect = new StatModifierEffect("haste", "walkspeed", 0.25f);
        var instance = CreateInstance("haste", stackCount: 1);

        effect.OnApply(entity, instance);
        Assert.Equal(1.25f, entity.Stats.GetBlended("walkspeed"), 4);

        effect.OnRemove(entity, instance);
        Assert.Equal(1.0f, entity.Stats.GetBlended("walkspeed"), 4);
    }

    [Fact]
    public void OnRemove_IndependentMode_RemovesOnlyThatInstance()
    {
        var entity = new DummyEntity();
        entity.Stats = new EntityStats(entity);
        var effect = new StatModifierEffect("haste", "walkspeed", 0.1f)
        { StackMode = EnumStackMode.Independent };
        var i1 = CreateInstance("haste", id: 1, stackCount: 1);
        var i2 = CreateInstance("haste", id: 2, stackCount: 1);

        effect.OnApply(entity, i1);
        effect.OnApply(entity, i2);
        Assert.Equal(1.2f, entity.Stats.GetBlended("walkspeed"), 4);

        effect.OnRemove(entity, i1);
        Assert.Equal(1.1f, entity.Stats.GetBlended("walkspeed"), 4);
    }

    [Fact]
    public void OnTick_DoesNothing()
    {
        var entity = new DummyEntity();
        entity.Stats = new EntityStats(entity);
        var effect = new StatModifierEffect("haste", "walkspeed", 0.25f);
        var instance = CreateInstance("haste", stackCount: 1);

        effect.OnApply(entity, instance);
        effect.OnTick(entity, instance, 0.5f);

        Assert.Equal(1.25f, entity.Stats.GetBlended("walkspeed"), 4);
    }

    private static IStatusEffectInstance CreateInstance(string code, long id = 1, int stackCount = 1)
    {
        var instance = Substitute.For<IStatusEffectInstance>();
        instance.Id.Returns(id);
        instance.Code.Returns(code);
        instance.StackCount.Returns(stackCount);
        return instance;
    }

    private sealed class DummyEntity : Entity
    {
        public DummyEntity()
        {
            EntityId = 1;
            Class = "dummy";
        }
    }
}
