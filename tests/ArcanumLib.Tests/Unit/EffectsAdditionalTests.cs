using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using ArcanumLib.Effects;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class EffectCategoryTests
{
    [Fact]
    public void None_IsZero()
    {
        Assert.Equal(0, (int)EffectCategory.None);
    }

    [Fact]
    public void Buff_IsOne()
    {
        Assert.Equal(1, (int)EffectCategory.Buff);
    }

    [Fact]
    public void Debuff_IsTwo()
    {
        Assert.Equal(2, (int)EffectCategory.Debuff);
    }

    [Fact]
    public void Values_AreDistinct()
    {
        var values = Enum.GetValues<EffectCategory>();
        Assert.Equal(3, values.Length);
        Assert.Contains(EffectCategory.None, values);
        Assert.Contains(EffectCategory.Buff, values);
        Assert.Contains(EffectCategory.Debuff, values);
    }
}

public class EnumStackModeTests
{
    [Fact]
    public void Values_AreDistinct()
    {
        var values = Enum.GetValues<EnumStackMode>();
        Assert.Equal(4, values.Length);
        Assert.Contains(EnumStackMode.Refresh, values);
        Assert.Contains(EnumStackMode.Stack, values);
        Assert.Contains(EnumStackMode.Override, values);
        Assert.Contains(EnumStackMode.Independent, values);
    }

    [Fact]
    public void Refresh_IsDefault()
    {
        var defaultMode = default(EnumStackMode);
        Assert.Equal(EnumStackMode.Refresh, defaultMode);
    }
}

public class StatusEffectInstanceTests
{
    [Fact]
    public void Constructor_NullEffect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new StatusEffectInstance(1, null!, 1000f));
    }

    [Fact]
    public void Constructor_PreservesValues()
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns("poison");
        effect.PersistThroughDeath.Returns(true);

        var instance = new StatusEffectInstance(42, effect, 5000f, "payload");

        Assert.Equal(42, instance.Id);
        Assert.Same(effect, instance.Effect);
        Assert.Equal("poison", instance.Code);
        Assert.Equal(5000f, instance.DurationMs);
        Assert.Equal(5000f, instance.RemainingMs);
        Assert.Equal(1, instance.StackCount);
        Assert.Equal("payload", instance.Data);
        Assert.True(instance.PersistThroughDeath);
    }

    [Fact]
    public void Constructor_NullData_AllowsNull()
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns("buff");

        var instance = new StatusEffectInstance(1, effect, 1000f, null);

        Assert.Null(instance.Data);
    }

    [Fact]
    public void RemainingMs_CanBeModified()
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns("slow");

        var instance = new StatusEffectInstance(1, effect, 1000f);
        instance.RemainingMs = 500f;

        Assert.Equal(500f, instance.RemainingMs);
    }

    [Fact]
    public void StackCount_CanBeModified()
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns("rage");

        var instance = new StatusEffectInstance(1, effect, 1000f);
        instance.StackCount = 3;

        Assert.Equal(3, instance.StackCount);
    }

    [Fact]
    public void PersistThroughDeath_DelegatesToEffect()
    {
        var effectTrue = Substitute.For<IStatusEffect>();
        effectTrue.PersistThroughDeath.Returns(true);
        var effectFalse = Substitute.For<IStatusEffect>();
        effectFalse.PersistThroughDeath.Returns(false);

        var instanceTrue = new StatusEffectInstance(1, effectTrue, 1000f);
        var instanceFalse = new StatusEffectInstance(2, effectFalse, 1000f);

        Assert.True(instanceTrue.PersistThroughDeath);
        Assert.False(instanceFalse.PersistThroughDeath);
    }

    [Fact]
    public void Code_DelegatesToEffect()
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns("custom-code");

        var instance = new StatusEffectInstance(1, effect, 1000f);

        Assert.Equal("custom-code", instance.Code);
    }
}

public class StatusEffectServiceAdditionalTests : IDisposable
{
    public StatusEffectServiceAdditionalTests()
    {
        ArcanumRuntime.Activate();
        ArcanumServices.Register(new EffectResistanceService());
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void Apply_NullEffect_Throws()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(1);

        Assert.Throws<ArgumentNullException>(() => service.Apply(entity, null!, 1000f));
    }

    [Fact]
    public void Remove_ByInstanceId_RemovesSpecificInstance()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(2);
        var effect = CreateEffect("dot", EnumStackMode.Independent);

        var first = service.Apply(entity, effect, 1000f)!;
        var second = service.Apply(entity, effect, 1000f)!;

        Assert.True(service.Remove(entity, first.Id));
        Assert.Equal(1, service.GetActive(entity).Count);
        Assert.True(service.Has(entity, "dot"));
    }

    [Fact]
    public void Remove_ByInstanceId_NonExistent_ReturnsFalse()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(3);

        Assert.False(service.Remove(entity, 999));
    }

    [Fact]
    public void Remove_ByCode_NonExistent_ReturnsFalse()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(4);

        Assert.False(service.Remove(entity, "nothing"));
    }

    [Fact]
    public void RemoveAll_NonExistent_ReturnsFalse()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(5);

        Assert.False(service.RemoveAll(entity));
    }

    [Fact]
    public void RemoveByCategory_None_ReturnsFalse()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(6);
        service.Apply(entity, CreateEffect("buff", EnumStackMode.Refresh, category: EffectCategory.Buff), 1000f);

        Assert.False(service.RemoveByCategory(entity, EffectCategory.None));
    }

    [Fact]
    public void RemoveByCategory_NonExistent_ReturnsFalse()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(7);

        Assert.False(service.RemoveByCategory(entity, EffectCategory.Buff));
    }

    [Fact]
    public void Has_NonExistentEntity_ReturnsFalse()
    {
        var service = new StatusEffectService();

        Assert.False(service.Has(null, "anything"));
    }

    [Fact]
    public void GetActive_NonExistentEntity_ReturnsEmpty()
    {
        var service = new StatusEffectService();

        Assert.Empty(service.GetActive(null));
    }

    [Fact]
    public void GetActive_AfterApply_ReturnsInstances()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(8);
        service.Apply(entity, CreateEffect("a", EnumStackMode.Independent), 1000f);
        service.Apply(entity, CreateEffect("b", EnumStackMode.Independent), 1000f);

        var active = service.GetActive(entity);

        Assert.Equal(2, active.Count);
    }

    [Fact]
    public void Apply_Override_ReplacesOldEffect()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(9);
        var effect = CreateEffect("override", EnumStackMode.Override);

        var first = service.Apply(entity, effect, 1000f);
        var second = service.Apply(entity, effect, 2000f);

        Assert.NotSame(first, second);
        Assert.Equal(1, service.GetActive(entity).Count);
        Assert.Equal(2000f, second!.RemainingMs);
    }

    [Fact]
    public void Apply_Override_RaisesOnEffectApplied()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(10);
        var effect = CreateEffect("override", EnumStackMode.Override);

        service.Apply(entity, effect, 1000f);

        IStatusEffectInstance? applied = null;
        service.OnEffectApplied += (_, i) => applied = i;

        service.Apply(entity, effect, 2000f);

        Assert.NotNull(applied);
    }

    [Fact]
    public void Apply_Stack_AtMaxStacks_RefreshesDuration()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(11);
        var effect = CreateEffect("stack", EnumStackMode.Stack, maxStacks: 2);

        service.Apply(entity, effect, 1000f);
        var second = service.Apply(entity, effect, 1000f);
        Assert.Equal(2, second!.StackCount);

        var third = service.Apply(entity, effect, 3000f);
        Assert.Equal(2, third!.StackCount);
        Assert.Equal(3000f, third.RemainingMs);
    }

    [Fact]
    public void Tick_RemovesExpiredEffects()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(12);
        var effect = CreateEffect("short", EnumStackMode.Refresh, hasTick: false);

        service.Apply(entity, effect, 100f);
        service.Tick(0.2f);

        Assert.False(service.Has(entity, "short"));
    }

    [Fact]
    public void Tick_CallsOnTick_ForActiveEffects()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(13);
        var effect = CreateEffect("tick", EnumStackMode.Refresh, hasTick: true);

        service.Apply(entity, effect, 5000f);
        service.Tick(0.1f);

        effect.Received(1).OnTick(entity, Arg.Any<IStatusEffectInstance>(), 0.1f);
    }

    [Fact]
    public void Tick_SkipsOnTick_WhenHasTickIsFalse()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(14);
        var effect = CreateEffect("notick", EnumStackMode.Refresh, hasTick: false);

        service.Apply(entity, effect, 5000f);
        service.Tick(0.1f);

        effect.DidNotReceive().OnTick(Arg.Any<Entity>(), Arg.Any<IStatusEffectInstance>(), Arg.Any<float>());
    }

    [Fact]
    public void Clear_RemovesAllContainers()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(15);
        service.Apply(entity, CreateEffect("a", EnumStackMode.Refresh), 1000f);

        service.Clear();

        Assert.Empty(service.GetActive(entity));
    }

    [Fact]
    public void Apply_Refresh_RaisesOnEffectRefreshed()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(16);
        var effect = CreateEffect("refresh", EnumStackMode.Refresh);

        service.Apply(entity, effect, 1000f);

        IStatusEffectInstance? refreshed = null;
        service.OnEffectRefreshed += (_, i) => refreshed = i;

        service.Apply(entity, effect, 2000f);

        Assert.NotNull(refreshed);
    }

    [Fact]
    public void Apply_Stack_RaisesOnEffectStacked()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(17);
        var effect = CreateEffect("stack", EnumStackMode.Stack, maxStacks: 3);

        service.Apply(entity, effect, 1000f);

        IStatusEffectInstance? stacked = null;
        service.OnEffectStacked += (_, i) => stacked = i;

        service.Apply(entity, effect, 1000f);

        Assert.NotNull(stacked);
        Assert.Equal(2, stacked!.StackCount);
    }

    [Fact]
    public void Tick_RaisesOnEffectExpired_WhenEffectExpires()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(18);
        var effect = CreateEffect("short", EnumStackMode.Refresh, hasTick: false);

        service.Apply(entity, effect, 100f);

        IStatusEffectInstance? expired = null;
        service.OnEffectExpired += (_, i) => expired = i;

        service.Tick(0.2f);

        Assert.NotNull(expired);
    }

    [Fact]
    public void Remove_RaisesOnEffectRemoved()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(19);
        var effect = CreateEffect("removable", EnumStackMode.Refresh);

        var instance = service.Apply(entity, effect, 1000f)!;

        IStatusEffectInstance? removed = null;
        service.OnEffectRemoved += (_, i) => removed = i;

        service.Remove(entity, "removable");

        Assert.Same(instance, removed);
    }

    [Fact]
    public void Apply_ResistanceFull_ReturnsNull()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(20);
        var effect = CreateEffect("fire", EnumStackMode.Refresh, tags: new[] { "fire" });

        // Resistance of 1.0 means 100% resisted → multiplier = 0 → returns null
        ArcanumServices.Get<EffectResistanceService>()!.AddResistance(entity, "fire", 1f);

        Assert.Null(service.Apply(entity, effect, 1000f));
    }

    [Fact]
    public void Apply_OnApplyThrows_DoesNotPropagate()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(21);
        var effect = CreateEffect("throwing", EnumStackMode.Refresh);
        effect.When(e => e.OnApply(Arg.Any<Entity>(), Arg.Any<IStatusEffectInstance>()))
             .Throw(new InvalidOperationException("boom"));

        var instance = service.Apply(entity, effect, 1000f);

        // The exception is caught inside SafeApply, so the instance is still created
        Assert.NotNull(instance);
    }

    [Fact]
    public void Tick_OnTickThrows_DoesNotPropagate()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(22);
        var effect = CreateEffect("throwing-tick", EnumStackMode.Refresh, hasTick: true);
        effect.When(e => e.OnTick(Arg.Any<Entity>(), Arg.Any<IStatusEffectInstance>(), Arg.Any<float>()))
             .Throw(new InvalidOperationException("tick boom"));

        service.Apply(entity, effect, 5000f);

        var ex = Record.Exception(() => service.Tick(0.1f));

        Assert.Null(ex);
    }

    [Fact]
    public void Remove_OnRemoveThrows_DoesNotPropagate()
    {
        var service = new StatusEffectService();
        var entity = new DummyEntity(23);
        var effect = CreateEffect("throwing-remove", EnumStackMode.Refresh);
        effect.When(e => e.OnRemove(Arg.Any<Entity>(), Arg.Any<IStatusEffectInstance>()))
             .Throw(new InvalidOperationException("remove boom"));

        service.Apply(entity, effect, 1000f);

        var ex = Record.Exception(() => service.Remove(entity, "throwing-remove"));

        Assert.Null(ex);
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
