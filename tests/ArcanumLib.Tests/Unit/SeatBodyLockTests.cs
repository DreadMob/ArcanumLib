using ArcanumLib.Entities;
using HarmonyLib;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace ArcanumLib.Tests.Unit;

/// <summary>The body of a player on a block seat or bed keeps the seat's yaw (<see cref="SeatBodyLock" />). The draw is client-side; this checks which seats take part and that the patch finds the game's method.</summary>
public class SeatBodyLockTests
{
    private static IMountableSeat Seat(EnumMountAngleMode mode, Entity? under)
    {
        var seat = Substitute.For<IMountableSeat>();
        seat.AngleMode.Returns(mode);
        seat.Entity.Returns(under);
        seat.SeatPosition.Returns(new EntityPos { Yaw = 1.25f });
        return seat;
    }

    [Theory]
    [InlineData(EnumMountAngleMode.FixateYaw)]
    [InlineData(EnumMountAngleMode.Fixate)]
    public void Block_Seat_With_Fixed_Yaw_Holds_The_Body(EnumMountAngleMode mode) =>
        Assert.Equal(1.25f, SeatBodyLock.FixedYaw(Seat(mode, null)));

    [Theory]
    [InlineData(EnumMountAngleMode.Unaffected)]
    [InlineData(EnumMountAngleMode.PushYaw)]
    public void Free_Seat_Is_Left_To_The_Game(EnumMountAngleMode mode) =>
        Assert.Null(SeatBodyLock.FixedYaw(Seat(mode, null)));

    [Fact]
    public void Rider_Of_An_Entity_Is_Left_To_The_Game() =>
        Assert.Null(SeatBodyLock.FixedYaw(Seat(EnumMountAngleMode.FixateYaw, Substitute.For<Entity>())));

    [Fact]
    public void Patch_Binds_To_The_Player_Renderer()
    {
        SeatBodyLock.Apply();
        try
        {
            var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(EntityPlayerShapeRenderer), nameof(EntityPlayerShapeRenderer.loadModelMatrixForPlayer)));
            Assert.NotNull(info);
            Assert.Contains(info.Prefixes, p => p.PatchMethod.DeclaringType == typeof(SeatBodyLock));
            Assert.Contains(info.Postfixes, p => p.PatchMethod.DeclaringType == typeof(SeatBodyLock));
        }
        finally { SeatBodyLock.Dispose(); }
    }
}
