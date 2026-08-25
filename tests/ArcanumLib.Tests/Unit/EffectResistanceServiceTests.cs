using System;
using System.Collections.Generic;
using ArcanumLib.Effects;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class EffectResistanceServiceTests
{
    private readonly EffectResistanceService _service = new();

    [Fact]
    public void AddImmunity_AndIsImmune_Match()
    {
        var entity = CreateEntity(1);
        var effect = CreateEffect("fireball", new[] { "fire" });

        Assert.False(_service.IsImmune(entity, "fire"));
        Assert.False(_service.IsImmuneToEffect(entity, effect));

        _service.AddImmunity(entity, "fire");

        Assert.True(_service.IsImmune(entity, "fire"));
        Assert.True(_service.IsImmuneToEffect(entity, effect));
    }

    [Fact]
    public void RemoveImmunity_Works()
    {
        var entity = CreateEntity(1);
        _service.AddImmunity(entity, "poison");
        Assert.True(_service.IsImmune(entity, "poison"));

        _service.RemoveImmunity(entity, "poison");
        Assert.False(_service.IsImmune(entity, "poison"));
    }

    [Fact]
    public void AddResistance_ReducesDuration()
    {
        var entity = CreateEntity(2);
        var effect = CreateEffect("slow", new[] { "slow" });

        Assert.Equal(1f, _service.GetDurationMultiplier(entity, effect));

        _service.AddResistance(entity, "slow", 0.5f);

        Assert.Equal(0.5f, _service.GetDurationMultiplier(entity, effect), 5);
    }

    [Fact]
    public void Immunity_OverridesResistance()
    {
        var entity = CreateEntity(3);
        var effect = CreateEffect("burn", new[] { "fire", "dot" });

        _service.AddResistance(entity, "fire", 0.25f);
        _service.AddImmunity(entity, "dot");

        Assert.Equal(0f, _service.GetDurationMultiplier(entity, effect));
    }

    [Fact]
    public void Resistance_ClampsToOne()
    {
        var entity = CreateEntity(4);

        _service.AddResistance(entity, "x", 2f);

        var effect = CreateEffect("x", new[] { "x" });
        Assert.Equal(0f, _service.GetDurationMultiplier(entity, effect), 5);
    }

    [Fact]
    public void Clear_RemovesAllModifiers()
    {
        var entity = CreateEntity(5);
        _service.AddImmunity(entity, "fire");
        _service.AddResistance(entity, "cold", 0.25f);

        _service.Clear(entity);

        Assert.False(_service.IsImmune(entity, "fire"));
        Assert.Equal(1f, _service.GetDurationMultiplier(entity, CreateEffect("cold", new[] { "cold" })), 5);
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
