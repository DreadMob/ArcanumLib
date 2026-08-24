using ArcanumLib.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class DamageHelperTests
{
    [Fact]
    public void CreatePlayer_SetsSourceAndType()
    {
        var source = new DummyEntity();
        var damage = DamageHelper.CreatePlayer(source, EnumDamageType.BluntAttack);

        Assert.Equal(EnumDamageSource.Player, damage.Source);
        Assert.Same(source, damage.SourceEntity);
        Assert.Equal(EnumDamageType.BluntAttack, damage.Type);
        Assert.True(damage.IgnoreInvFrames);
    }

    [Fact]
    public void Create_Entity_SetsDefaults()
    {
        var source = new DummyEntity();
        var damage = DamageHelper.Create(source, EnumDamageType.PiercingAttack);

        Assert.Equal(EnumDamageSource.Entity, damage.Source);
        Assert.Same(source, damage.SourceEntity);
        Assert.Equal(EnumDamageType.PiercingAttack, damage.Type);
    }

    [Fact]
    public void Create_WithCause_SetsCauseEntity()
    {
        var source = new DummyEntity();
        var cause = new DummyEntity();
        var damage = DamageHelper.Create(source, cause, EnumDamageType.SlashingAttack);

        Assert.Same(source, damage.SourceEntity);
        Assert.Same(cause, damage.CauseEntity);
    }

    [Fact]
    public void Create_WithTier_SetsDamageTier()
    {
        var source = new DummyEntity();
        var damage = DamageHelper.Create(source, EnumDamageType.PiercingAttack, 3);

        Assert.Equal(3, damage.DamageTier);
    }

    [Fact]
    public void CreateWeather_SetsSourcePosAndKnockback()
    {
        var pos = new Vec3d(1, 2, 3);
        var damage = DamageHelper.CreateWeather(pos, EnumDamageType.Fire, 2.5f);

        Assert.Equal(EnumDamageSource.Weather, damage.Source);
        Assert.Same(pos, damage.SourcePos);
        Assert.Equal(2.5f, damage.KnockbackStrength);
    }

    [Fact]
    public void CreateInternal_SetsSourceToInternal()
    {
        var damage = DamageHelper.CreateInternal(EnumDamageType.Poison);

        Assert.Equal(EnumDamageSource.Internal, damage.Source);
        Assert.Equal(EnumDamageType.Poison, damage.Type);
    }

    [Fact]
    public void CreateHeal_SetsHealType()
    {
        var damage = DamageHelper.CreateHeal(false);

        Assert.Equal(EnumDamageType.Heal, damage.Type);
        Assert.False(damage.IgnoreInvFrames);
    }

    [Fact]
    public void Create_WithPositions_SetsSourceAndHitPosition()
    {
        var source = new DummyEntity();
        var cause = new DummyEntity();
        var sourcePos = new Vec3d(1, 2, 3);
        var hitPos = new Vec3d(4, 5, 6);
        var damage = DamageHelper.Create(source, cause, EnumDamageType.BluntAttack, 2, sourcePos, hitPos);

        Assert.Same(sourcePos, damage.SourcePos);
        Assert.Same(hitPos, damage.HitPosition);
    }

    private sealed class DummyEntity : Entity
    {
        public DummyEntity()
        {
            Class = "dummy";
        }
    }
}
