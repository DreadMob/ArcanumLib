using ArcanumLib.Common;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class MachineClaimsTests
{
    private static readonly BlockPos Cell = new(10, 5, 10);

    private static (IWorldAccessor world, ILandClaimAPI claims) World(params LandClaim[] here)
    {
        var world = Substitute.For<IWorldAccessor>();
        var claims = Substitute.For<ILandClaimAPI>();
        claims.Get(Arg.Any<BlockPos>()).Returns(here);
        world.Claims.Returns(claims);
        return (world, claims);
    }

    private static BlockEntity Machine(IWorldAccessor world)
    {
        var be = Substitute.For<BlockEntity>();
        var api = Substitute.For<ICoreAPI>();
        api.World.Returns(world);
        be.Api = api;
        return be;
    }

    [Fact]
    public void Owner_Offline_Works_Unclaimed_Land()
    {
        var (world, claims) = World();
        Assert.True(MachineClaims.MayWork(Machine(world), null, "owner", Cell, EnumBlockAccessFlags.BuildOrBreak));
        claims.DidNotReceive().TestAccess(Arg.Any<IPlayer>(), Arg.Any<BlockPos>(), Arg.Any<EnumBlockAccessFlags>());
    }

    [Fact]
    public void Owner_Offline_Works_Only_In_Own_Claims()
    {
        var own = new LandClaim { OwnedByPlayerUid = "owner" };
        var other = new LandClaim { OwnedByPlayerUid = "someone" };
        Assert.True(MachineClaims.MayWork(Machine(World(own).world), null, "owner", Cell, EnumBlockAccessFlags.BuildOrBreak));
        Assert.False(MachineClaims.MayWork(Machine(World(own, other).world), null, "owner", Cell, EnumBlockAccessFlags.BuildOrBreak));
        Assert.False(MachineClaims.MayWork(Machine(World(own).world), null, null, Cell, EnumBlockAccessFlags.BuildOrBreak));
    }

    [Fact]
    public void Driver_Is_Asked_Quietly()
    {
        var (world, claims) = World(new LandClaim { OwnedByPlayerUid = "someone" });
        var machine = Substitute.For<Entity>();
        machine.World = world;
        IPlayer? driver = null;   // IPlayer cannot be proxied (internal IsInInteractionRangeOf)

        claims.TestAccess(driver, Cell, EnumBlockAccessFlags.BuildOrBreak).Returns(EnumWorldAccessResponse.Granted);
        Assert.True(MachineClaims.MayWork(machine, driver, Cell, EnumBlockAccessFlags.BuildOrBreak));

        claims.TestAccess(driver, Cell, EnumBlockAccessFlags.BuildOrBreak).Returns(EnumWorldAccessResponse.LandClaimed);
        Assert.False(MachineClaims.MayWork(machine, driver, Cell, EnumBlockAccessFlags.BuildOrBreak));
        claims.DidNotReceive().TryAccess(Arg.Any<IPlayer>(), Arg.Any<BlockPos>(), Arg.Any<EnumBlockAccessFlags>());
    }

    [Fact]
    public void Refusals_Are_Told_At_Most_Every_Few_Seconds_Per_Machine()
    {
        object a = new(), b = new();
        Assert.True(MachineClaims.TimeToTell(a, 1000));
        Assert.False(MachineClaims.TimeToTell(a, 1000 + MachineClaims.TellEveryMs - 1));
        Assert.True(MachineClaims.TimeToTell(b, 1500));
        Assert.True(MachineClaims.TimeToTell(a, 1000 + MachineClaims.TellEveryMs));
    }
}
