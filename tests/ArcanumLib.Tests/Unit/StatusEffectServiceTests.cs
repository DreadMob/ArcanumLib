using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Core;
using ArcanumLib.Effects;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class StatusEffectServiceTests : IDisposable
{
    public StatusEffectServiceTests()
    {
        ArcanumRuntime.Activate();
        var resistance = new EffectResistanceService();
        ArcanumServices.Register(resistance);
        ArcanumServices.Register<IEffectResistanceService>(resistance);
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void Apply_ReturnsNull_WhenEntityIsNull()
    {
        var service = new StatusEffectService();
        var effect = CreateEffect("x", EnumStackMode.Refresh);

        Assert.Null(service.Apply(null, effect, 1000f));
    }

    [Fact]
    public void Apply_SingleEffect_RaisesOnEffectApplied()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(1);
        var effect = CreateEffect("slow", EnumStackMode.Refresh, tags: new[] { "slow" });

        IStatusEffectInstance? applied = null;
        service.OnEffectApplied += (_, instance) => applied = instance;

        var instance = service.Apply(entity, effect, 1000f);

        Assert.NotNull(instance);
        Assert.Same(instance, applied);
        Assert.True(service.Has(entity, "slow"));
        effect.Received(1).OnApply(entity, instance!);
    }

    [Fact]
    public void Apply_Refresh_RefreshesDuration()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(2);
        var effect = CreateEffect("poison", EnumStackMode.Refresh);

        var first = service.Apply(entity, effect, 1000f);
        var second = service.Apply(entity, effect, 2000f);

        Assert.Same(first, second);
        effect.Received(1).OnApply(entity, first!);
    }

    [Fact]
    public void Apply_Stack_IncreasesStacks()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(3);
        var effect = CreateEffect("buff", EnumStackMode.Stack, maxStacks: 3);

        var first = service.Apply(entity, effect, 1000f);
        var second = service.Apply(entity, effect, 1000f);

        Assert.Same(first, second);
        Assert.Equal(2, second!.StackCount);
        effect.Received(2).OnApply(entity, second);
    }

    [Fact]
    public void Remove_ByCode_RaisesOnEffectRemoved()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(4);
        var effect = CreateEffect("hot", EnumStackMode.Refresh);

        var instance = service.Apply(entity, effect, 1000f)!;

        IStatusEffectInstance? removed = null;
        service.OnEffectRemoved += (_, i) => removed = i;

        Assert.True(service.Remove(entity, "hot"));
        Assert.Same(instance, removed);
        effect.Received(1).OnRemove(entity, instance);
    }

    [Fact]
    public void RemoveAll_ClearsAll()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(5);

        service.Apply(entity, CreateEffect("a", EnumStackMode.Independent), 1000f);
        service.Apply(entity, CreateEffect("b", EnumStackMode.Independent), 1000f);

        Assert.True(service.RemoveAll(entity));
        Assert.Empty(service.GetActive(entity));
    }

    [Fact]
    public void Apply_Immune_ReturnsNull()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(6);
        var effect = CreateEffect("cold", EnumStackMode.Refresh, tags: new[] { "ice" });

        ArcanumServices.Get<IEffectResistanceService>()!.AddImmunity(entity, "ice");

        Assert.Null(service.Apply(entity, effect, 1000f));
    }

    [Fact]
    public void Apply_Resisted_DurationReduced()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(7);
        var effect = CreateEffect("shock", EnumStackMode.Refresh, tags: new[] { "lightning" });

        ArcanumServices.Get<IEffectResistanceService>()!.AddResistance(entity, "lightning", 0.5f);

        var instance = service.Apply(entity, effect, 1000f);

        Assert.NotNull(instance);
        // 1000ms * 0.5 = 500ms remaining
        Assert.Equal(500f, instance!.RemainingMs, 5);
    }

    [Fact]
    public void Apply_Independent_CreatesSeparateInstances()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(9);
        var effect = CreateEffect("dot", EnumStackMode.Independent);

        var first = service.Apply(entity, effect, 1000f);
        var second = service.Apply(entity, effect, 1000f);

        Assert.NotSame(first, second);
        Assert.Equal(2, service.GetActive(entity).Count);
    }

    [Fact]
    public void RemoveByCategory_RemovesMatching()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(10);

        service.Apply(entity, CreateEffect("buff", EnumStackMode.Refresh, category: EffectCategory.Buff), 1000f);
        service.Apply(entity, CreateEffect("debuff", EnumStackMode.Refresh, category: EffectCategory.Debuff), 1000f);

        Assert.True(service.RemoveByCategory(entity, EffectCategory.Buff));
        Assert.False(service.Has(entity, "buff"));
        Assert.True(service.Has(entity, "debuff"));
    }

    [Fact]
    public void Tick_ExpiresEffect_RaisesOnEffectExpired()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(8);
        var effect = CreateEffect("short", EnumStackMode.Refresh, hasTick: false);

        var instance = service.Apply(entity, effect, 100f)!;

        IStatusEffectInstance? expired = null;
        service.OnEffectExpired += (_, i) => expired = i;

        service.Tick(0.2f); // 200ms elapsed, effect expired

        Assert.Same(instance, expired);
        Assert.False(service.Has(entity, "short"));
    }

    private static IStatusEffect CreateEffect(
        string code,
        EnumStackMode mode,
        int maxStacks = 1,
        IReadOnlyCollection<string>? tags = null,
        bool hasTick = true,
        EffectCategory category = EffectCategory.None)
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns(code);
        effect.StackMode.Returns(mode);
        effect.MaxStacks.Returns(maxStacks);
        effect.PersistThroughDeath.Returns(false);
        effect.Category.Returns(category);
        effect.Tags.Returns(tags ?? new[] { code });
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
