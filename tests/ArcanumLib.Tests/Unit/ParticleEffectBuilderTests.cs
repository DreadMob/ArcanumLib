using System;
using ArcanumLib.Particles;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ParticleEffectBuilderTests
{
    [Fact]
    public void Build_Defaults_AreSane()
    {
        var props = new ParticleEffectBuilder().Build();

        Assert.Equal(5, props.MinQuantity);
        Assert.Equal(10, props.MinQuantity + props.AddQuantity);
        Assert.Equal(ParticleUtils.Colors.White, props.Color);
        Assert.Equal(1.0f, props.LifeLength);
        Assert.Equal(0.3f, props.MinSize);
        Assert.Equal(0.3f, props.MaxSize);
    }

    [Fact]
    public void Count_SetsMinMax()
    {
        var props = new ParticleEffectBuilder().Count(20, 50).Build();

        Assert.Equal(20, props.MinQuantity);
        Assert.Equal(50, props.MinQuantity + props.AddQuantity);
    }

    [Fact]
    public void Color_SetsColor()
    {
        var props = new ParticleEffectBuilder().Color(ParticleUtils.Colors.Fire).Build();

        Assert.Equal(ParticleUtils.Colors.Fire, props.Color);
    }

    [Fact]
    public void Position_CenterSpread_BuildsBox()
    {
        var center = new Vec3d(10, 20, 30);
        var props = new ParticleEffectBuilder().Position(center, 1f).Build();

        Assert.Equal(9, props.MinPos.X);
        Assert.Equal(19, props.MinPos.Y);
        Assert.Equal(29, props.MinPos.Z);
        // MaxPos = MinPos + AddPos
        Assert.Equal(11, props.MinPos.X + props.AddPos.X);
        Assert.Equal(21, props.MinPos.Y + props.AddPos.Y);
        Assert.Equal(31, props.MinPos.Z + props.AddPos.Z);
    }

    [Fact]
    public void Position_MinMax_BuildsBox()
    {
        var min = new Vec3d(0, 0, 0);
        var max = new Vec3d(5, 5, 5);
        var props = new ParticleEffectBuilder().Position(min, max).Build();

        Assert.Equal(0, props.MinPos.X);
        Assert.Equal(5, props.MinPos.X + props.AddPos.X);
    }

    [Fact]
    public void Velocity_SetsMinMax()
    {
        var min = new Vec3f(0.1f, 0.2f, 0.3f);
        var max = new Vec3f(0.4f, 0.5f, 0.6f);
        var props = new ParticleEffectBuilder().Velocity(min, max).Build();

        Assert.Equal(min, props.MinVelocity);
        Assert.Equal(max.X, props.MinVelocity.X + props.AddVelocity.X);
        Assert.Equal(max.Y, props.MinVelocity.Y + props.AddVelocity.Y);
        Assert.Equal(max.Z, props.MinVelocity.Z + props.AddVelocity.Z);
    }

    [Fact]
    public void VelocityUp_SetsUpwardRange()
    {
        var props = new ParticleEffectBuilder().VelocityUp(0.5f, 1f).Build();

        Assert.Equal(-0.05f, props.MinVelocity.X);
        Assert.Equal(0.5f, props.MinVelocity.Y);
        Assert.Equal(0.05f, props.MinVelocity.X + props.AddVelocity.X);
        Assert.Equal(1f, props.MinVelocity.Y + props.AddVelocity.Y);
    }

    [Fact]
    public void VelocityOutward_SetsOutwardRange()
    {
        var props = new ParticleEffectBuilder().VelocityOutward(0.4f).Build();

        Assert.Equal(-0.4f, props.MinVelocity.X);
        Assert.Equal(0.4f, props.MinVelocity.X + props.AddVelocity.X, 5);
        Assert.Equal(-0.4f * 0.3f, props.MinVelocity.Y, 5);
        Assert.Equal(0.4f, props.MinVelocity.Y + props.AddVelocity.Y, 5);
    }

    [Fact]
    public void Life_SetsLifeLength()
    {
        var props = new ParticleEffectBuilder().Life(2.5f).Build();

        Assert.Equal(2.5f, props.LifeLength);
    }

    [Fact]
    public void Gravity_SetsGravityEffect()
    {
        var props = new ParticleEffectBuilder().Gravity(-0.5f).Build();

        Assert.Equal(-0.5f, props.GravityEffect);
    }

    [Fact]
    public void Size_MinMax_SetsBoth()
    {
        var props = new ParticleEffectBuilder().Size(0.1f, 0.5f).Build();

        Assert.Equal(0.1f, props.MinSize);
        Assert.Equal(0.5f, props.MaxSize);
    }

    [Fact]
    public void Size_Uniform_SetsBothEqual()
    {
        var props = new ParticleEffectBuilder().Size(0.25f).Build();

        Assert.Equal(0.25f, props.MinSize);
        Assert.Equal(0.25f, props.MaxSize);
    }

    [Fact]
    public void Cube_SetsModelToCube()
    {
        var props = new ParticleEffectBuilder().Cube().Build();

        Assert.Equal(EnumParticleModel.Cube, props.ParticleModel);
    }

    [Fact]
    public void Quad_SetsModelToQuad()
    {
        var props = new ParticleEffectBuilder().Cube().Quad().Build();

        Assert.Equal(EnumParticleModel.Quad, props.ParticleModel);
    }

    [Fact]
    public void Model_SetsModel()
    {
        var props = new ParticleEffectBuilder().Model(EnumParticleModel.Cube).Build();

        Assert.Equal(EnumParticleModel.Cube, props.ParticleModel);
    }

    [Fact]
    public void Delay_ClampsToZero()
    {
        var builder = new ParticleEffectBuilder().Delay(-5);
        var world = Substitute.For<IWorldAccessor>();
        var api = Substitute.For<ICoreAPI>();
        api.World.Returns(world);
        world.Api.Returns(api);

        builder.Spawn(world);
        world.Received(1).SpawnParticles(Arg.Any<SimpleParticleProperties>());
    }

    [Fact]
    public void Repeat_ClampsIntervalToMinimum()
    {
        var builder = new ParticleEffectBuilder().Repeat(0.01f, 1f);
        var world = Substitute.For<IWorldAccessor>();
        var api = Substitute.For<ICoreAPI>();
        api.World.Returns(world);
        world.Api.Returns(api);
        world.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>()).Returns(1L);

        builder.Spawn(world);

        world.Received(1).RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>());
    }

    [Fact]
    public void FollowEntity_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ParticleEffectBuilder().FollowEntity(null!));
    }

    [Fact]
    public void Spawn_NullSapi_DoesNothing()
    {
        var builder = new ParticleEffectBuilder();
        builder.Spawn((ICoreServerAPI)null!);
    }

    [Fact]
    public void Spawn_NullWorld_DoesNothing()
    {
        var builder = new ParticleEffectBuilder();
        builder.Spawn((IWorldAccessor)null!);
    }

    [Fact]
    public void Spawn_WithWorld_CallsSpawnParticles()
    {
        var world = Substitute.For<IWorldAccessor>();
        var api = Substitute.For<ICoreAPI>();
        api.World.Returns(world);
        world.Api.Returns(api);

        new ParticleEffectBuilder().Spawn(world);

        world.Received(1).SpawnParticles(Arg.Any<SimpleParticleProperties>());
    }

    [Fact]
    public void Spawn_WithSapi_CallsSpawnParticles()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var world = Substitute.For<IServerWorldAccessor>();
        sapi.World.Returns(world);
        world.Api.Returns(sapi);

        new ParticleEffectBuilder().Spawn(sapi);

        world.Received(1).SpawnParticles(Arg.Any<SimpleParticleProperties>());
    }

    [Fact]
    public void AtEntity_NullEntity_DoesNothing()
    {
        var props = new ParticleEffectBuilder().AtEntity(null!).Build();

        // Defaults should remain
        Assert.Equal(0, props.MinPos.X);
        Assert.Equal(0, props.MinPos.X + props.AddPos.X);
    }

    [Fact]
    public void AtEntity_WithEntity_SetsPositionAroundEntity()
    {
        var entity = new DummyEntity();
        entity.Pos.X = 10;
        entity.Pos.Y = 20;
        entity.Pos.Z = 30;

        var props = new ParticleEffectBuilder().AtEntity(entity, 1f).Build();

        Assert.Equal(9, props.MinPos.X);
        Assert.Equal(11, props.MinPos.X + props.AddPos.X);
    }

    private sealed class DummyEntity : Entity
    {
        public DummyEntity()
        {
            EntityId = 1;
            Class = "dummy";
            SelectionBox = new Cuboidf(0, 0, 0, 1, 2, 1);
        }
    }
}
