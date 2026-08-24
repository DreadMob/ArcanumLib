using System;
using System.Collections.Generic;
using ArcanumLib.Effects;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("EffectState")]
public class EffectResistanceStoreTests
{
    [Fact]
    public void AddImmunity_AndIsImmune_Match()
    {
        var entity = CreateEntity(1);
        var effect = CreateEffect("fireball", new[] { "fire" });

        Assert.False(EffectResistanceStore.IsImmune(entity, "fire"));
        Assert.False(EffectResistanceStore.IsImmuneToEffect(entity, effect));

        EffectResistanceStore.AddImmunity(entity, "fire");

        Assert.True(EffectResistanceStore.IsImmune(entity, "fire"));
        Assert.True(EffectResistanceStore.IsImmuneToEffect(entity, effect));
    }

    [Fact]
    public void RemoveImmunity_Works()
    {
        var entity = CreateEntity(1);
        EffectResistanceStore.AddImmunity(entity, "poison");
        Assert.True(EffectResistanceStore.IsImmune(entity, "poison"));

        EffectResistanceStore.RemoveImmunity(entity, "poison");
        Assert.False(EffectResistanceStore.IsImmune(entity, "poison"));
    }

    [Fact]
    public void AddResistance_ReducesDuration()
    {
        var entity = CreateEntity(2);
        var effect = CreateEffect("slow", new[] { "slow" });

        Assert.Equal(1f, EffectResistanceStore.GetDurationMultiplier(entity, effect));

        EffectResistanceStore.AddResistance(entity, "slow", 0.5f);

        Assert.Equal(0.5f, EffectResistanceStore.GetDurationMultiplier(entity, effect), 5);
    }

    [Fact]
    public void Immunity_OverridesResistance()
    {
        var entity = CreateEntity(3);
        var effect = CreateEffect("burn", new[] { "fire", "dot" });

        EffectResistanceStore.AddResistance(entity, "fire", 0.25f);
        EffectResistanceStore.AddImmunity(entity, "dot");

        Assert.Equal(0f, EffectResistanceStore.GetDurationMultiplier(entity, effect));
    }

    [Fact]
    public void Resistance_ClampsToOne()
    {
        var entity = CreateEntity(4);

        EffectResistanceStore.AddResistance(entity, "x", 2f);

        var effect = CreateEffect("x", new[] { "x" });
        Assert.Equal(0f, EffectResistanceStore.GetDurationMultiplier(entity, effect), 5);
    }

    [Fact]
    public void Clear_RemovesAllModifiers()
    {
        var entity = CreateEntity(5);
        EffectResistanceStore.AddImmunity(entity, "fire");
        EffectResistanceStore.AddResistance(entity, "cold", 0.25f);

        EffectResistanceStore.Clear(entity);

        Assert.False(EffectResistanceStore.IsImmune(entity, "fire"));
        Assert.Equal(1f, EffectResistanceStore.GetDurationMultiplier(entity, CreateEffect("cold", new[] { "cold" })), 5);
    }

    public EffectResistanceStoreTests()
    {
        EffectResistanceStore.ClearAll();
    }

    private static Entity CreateEntity(long id) => new DummyEntity(id);

    private static IStatusEffect CreateEffect(string code, IReadOnlyList<string> tags)
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns(code);
        effect.Tags.Returns(tags);
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
