using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Effects;
using NSubstitute;
using Vintagestory.API.Common.Entities;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class StatusEffectContainerTests
{
    [Fact]
    public void Constructor_NullEntity_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new StatusEffectContainer(null!, () => 1));
    }

    [Fact]
    public void Constructor_NullNextId_Throws()
    {
        var entity = new DummyEntity(1);
        Assert.Throws<System.ArgumentNullException>(() => new StatusEffectContainer(entity, null!));
    }

    [Fact]
    public void Apply_NullEffect_Throws()
    {
        var container = CreateContainer(1);

        Assert.Throws<System.ArgumentNullException>(() => container.Apply(null!, 1000f, null));
    }

    [Fact]
    public void Apply_Independent_CreatesSeparateInstances()
    {
        var container = CreateContainer(2);
        var effect = CreateEffect("dot", EnumStackMode.Independent);

        var (first, _, _) = container.Apply(effect, 1000f, null);
        var (second, _, _) = container.Apply(effect, 1000f, null);

        Assert.NotSame(first, second);
        Assert.Equal(2, container.Instances.Count);
    }

    [Fact]
    public void Apply_Refresh_WhenMissing_CreatesInstance()
    {
        var container = CreateContainer(3);
        var effect = CreateEffect("slow", EnumStackMode.Refresh);

        var (instance, result, old) = container.Apply(effect, 1000f, null);

        Assert.NotNull(instance);
        Assert.Equal(StatusEffectApplyResult.New, result);
        Assert.Null(old);
        Assert.Equal("slow", instance!.Code);
    }

    [Fact]
    public void Apply_Refresh_WhenExisting_RefreshesDuration()
    {
        var container = CreateContainer(4);
        var effect = CreateEffect("slow", EnumStackMode.Refresh);

        var (first, _, _) = container.Apply(effect, 1000f, null);
        var (second, result, _) = container.Apply(effect, 2000f, null);

        Assert.Same(first, second);
        Assert.Equal(StatusEffectApplyResult.Refreshed, result);
        Assert.Equal(2000f, second!.RemainingMs, 5);
    }

    [Fact]
    public void Apply_Stack_IncreasesStacksUpToMax()
    {
        var container = CreateContainer(5);
        var effect = CreateEffect("strength", EnumStackMode.Stack, maxStacks: 2);

        var (first, _, _) = container.Apply(effect, 1000f, null);
        var (second, r2, _) = container.Apply(effect, 1000f, null);
        var (third, r3, _) = container.Apply(effect, 1500f, null);

        Assert.Same(first, second);
        Assert.Same(second, third);
        Assert.Equal(StatusEffectApplyResult.Stacked, r2);
        Assert.Equal(StatusEffectApplyResult.Refreshed, r3);
        Assert.Equal(2, third!.StackCount);
        Assert.Equal(1500f, third.RemainingMs, 5);
    }

    [Fact]
    public void Apply_Override_ReplacessInstance()
    {
        var (container, entity) = CreateContainerWithEntity(6);
        var effect = CreateEffect("shield", EnumStackMode.Override);

        var (first, _, _) = container.Apply(effect, 1000f, null);
        var (second, result, old) = container.Apply(effect, 500f, "payload");

        Assert.NotSame(first, second);
        Assert.Equal(StatusEffectApplyResult.Overridden, result);
        Assert.Same(first, old);
        Assert.Same("payload", second!.Data);
        Assert.Single(container.Instances);
    }

    [Fact]
    public void RemoveByCode_RemovesMatching()
    {
        var container = CreateContainer(7);
        var effect = CreateEffect("burn", EnumStackMode.Refresh);

        container.Apply(effect, 1000f, null);
        var removed = container.RemoveByCode("burn");

        Assert.Single(removed);
        Assert.Empty(container.Instances);
    }

    [Fact]
    public void RemoveByCategory_None_ReturnsEmpty()
    {
        var container = CreateContainer(8);
        container.Apply(CreateEffect("x", EnumStackMode.Refresh), 1000f, null);

        var removed = container.RemoveByCategory(EffectCategory.None);

        Assert.Empty(removed);
        Assert.Single(container.Instances);
    }

    [Fact]
    public void RemoveByCategory_Matching_Removes()
    {
        var container = CreateContainer(9);
        var buff = CreateEffect("haste", EnumStackMode.Refresh, category: EffectCategory.Buff);
        var debuff = CreateEffect("curse", EnumStackMode.Refresh, category: EffectCategory.Debuff);

        container.Apply(buff, 1000f, null);
        container.Apply(debuff, 1000f, null);

        var removed = container.RemoveByCategory(EffectCategory.Buff);

        Assert.Single(removed);
        Assert.Equal("haste", removed[0].Code);
        Assert.Single(container.Instances);
        Assert.Equal("curse", container.Instances[0].Code);
    }

    [Fact]
    public void RemoveById_RemovesAndReturnsInstance()
    {
        var container = CreateContainer(10);
        var effect = CreateEffect("mark", EnumStackMode.Refresh);

        var (instance, _, _) = container.Apply(effect, 1000f, null);
        var removed = container.RemoveById(instance!.Id);

        Assert.Same(instance, removed);
        Assert.Empty(container.Instances);
    }

    [Fact]
    public void RemoveById_Unknown_ReturnsNull()
    {
        var container = CreateContainer(11);

        Assert.Null(container.RemoveById(999));
    }

    [Fact]
    public void RemoveAll_ClearsInstances()
    {
        var container = CreateContainer(12);

        container.Apply(CreateEffect("a", EnumStackMode.Independent), 1000f, null);
        container.Apply(CreateEffect("b", EnumStackMode.Independent), 1000f, null);

        var removed = container.RemoveAll();

        Assert.Equal(2, removed.Count);
        Assert.Empty(container.Instances);
    }

    [Fact]
    public void Tick_Alive_Entity_DecreasesRemainingAndExpires()
    {
        var (container, entity) = CreateContainerWithEntity(13);
        entity.Alive = true;
        var effect = CreateEffect("short", EnumStackMode.Refresh, hasTick: false);

        var (instance, _, _) = container.Apply(effect, 200f, null);
        var result = container.Tick(0.1f); // 100ms

        Assert.Single(result.Alive);
        Assert.Empty(result.Expired);
        Assert.Equal(100f, instance!.RemainingMs, 5);

        result = container.Tick(0.2f); // 200ms more, expires

        Assert.Single(result.Expired);
        Assert.Same(instance, result.Expired[0]);
        Assert.Empty(result.Alive);
        Assert.Empty(container.Instances);
    }

    [Fact]
    public void Tick_DeadEntity_PersistsWhenFlagSet()
    {
        var (container, entity) = CreateContainerWithEntity(14);
        entity.Alive = false;
        var effect = CreateEffect("soulbound", EnumStackMode.Refresh, persistThroughDeath: true);

        var (instance, _, _) = container.Apply(effect, 1000f, null);
        var result = container.Tick(1f);

        Assert.Same(instance, result.Alive[0]);
        Assert.Empty(result.RemovedByDeath);
        Assert.Single(container.Instances);
    }

    [Fact]
    public void Tick_DeadEntity_RemovesWhenNotPersistent()
    {
        var (container, entity) = CreateContainerWithEntity(15);
        entity.Alive = false;
        var effect = CreateEffect("fragile", EnumStackMode.Refresh);

        var (instance, _, _) = container.Apply(effect, 1000f, null);
        var result = container.Tick(1f);

        Assert.Same(instance, result.RemovedByDeath[0]);
        Assert.Empty(result.Alive);
        Assert.Empty(container.Instances);
    }

    private static long _nextId;

    private static StatusEffectContainer CreateContainer(long entityId)
    {
        var entity = new DummyEntity(entityId);
        _nextId = 1;
        return new StatusEffectContainer(entity, () => _nextId++);
    }

    private static (StatusEffectContainer container, DummyEntity entity) CreateContainerWithEntity(long entityId)
    {
        var entity = new DummyEntity(entityId);
        _nextId = 1;
        var container = new StatusEffectContainer(entity, () => _nextId++);
        return (container, entity);
    }

    private static IStatusEffect CreateEffect(
        string code,
        EnumStackMode mode,
        int maxStacks = 1,
        bool persistThroughDeath = false,
        EffectCategory category = EffectCategory.None,
        bool hasTick = true)
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns(code);
        effect.StackMode.Returns(mode);
        effect.MaxStacks.Returns(maxStacks);
        effect.PersistThroughDeath.Returns(persistThroughDeath);
        effect.Category.Returns(category);
        effect.Tags.Returns(new[] { code });
        effect.HasTick.Returns(hasTick);
        return effect;
    }

    private sealed class DummyEntity : Entity
    {
        public DummyEntity(long id)
        {
            EntityId = id;
            Class = "dummy";
        }
    }
}
