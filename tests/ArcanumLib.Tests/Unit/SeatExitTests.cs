using System.Collections.Generic;
using ArcanumLib.Entities;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class SeatExitTests
{
    private static readonly Block Air = new() { CollisionBoxes = null };
    private static readonly Block Solid = new() { CollisionBoxes = new[] { new Cuboidf(0, 0, 0, 1, 1, 1) } };
    private static readonly Block Bench = new() { CollisionBoxes = new[] { new Cuboidf(0, 0, 0, 1, 0.5f, 1) } };
    private static readonly BlockPos At = new(5, 1, 5);

    private sealed class Room
    {
        public readonly Dictionary<(int, int, int), Block> Blocks = new();

        public Block Get(BlockPos p) => p.Y <= 0 ? Solid : Blocks.GetValueOrDefault((p.X, p.Y, p.Z), Air);

        public IWorldAccessor World()
        {
            var ba = Substitute.For<IBlockAccessor>();
            ba.GetBlock(Arg.Any<BlockPos>()).Returns(ci => Get(ci.Arg<BlockPos>()));
            ba.GetBlock(Arg.Any<BlockPos>(), Arg.Any<int>()).Returns(ci => Get(ci.Arg<BlockPos>()));
            var world = Substitute.For<IWorldAccessor>();
            world.BlockAccessor.Returns(ba);
            world.CollisionTester.Returns(new CollisionTester());
            return world;
        }

        public void Wall(int x, int z) { for (int y = 1; y <= 3; y++) Blocks[(x, y, z)] = Solid; }
    }

    private sealed class Sitter : EntityAgent
    {
        public Sitter() => Properties = new EntityProperties { CollisionBoxSize = new Vec2f(0.6f, 1.85f) };
    }

    private static Vec3d? Spot(Room room, Vec3d seat, bool beside = false)
    {
        room.Blocks[(At.X, At.Y, At.Z)] = Bench;
        return SeatExit.Spot(room.World(), new Sitter(), Bench, At, seat, beside);
    }

    [Fact]
    public void Free_Rider_Stays_Where_They_Are()
    {
        Assert.Null(Spot(new Room(), new Vec3d(5.5, 1.5 + 0.01, 5.5)));
    }

    [Fact]
    public void Rider_Inside_The_Block_Steps_Onto_The_Nearest_Floor()
    {
        var spot = Spot(new Room(), new Vec3d(5.5, 1.2, 5.5));
        Assert.NotNull(spot);
        Assert.Equal(1.001, spot!.Y, 3);
        Assert.Equal(1.0, System.Math.Abs(spot.X - 5.5) + System.Math.Abs(spot.Z - 5.5), 3);
    }

    [Fact]
    public void Walls_Around_The_Seat_Are_Skipped()
    {
        var room = new Room();
        room.Wall(5, 4); room.Wall(5, 6); room.Wall(4, 5);
        var spot = Spot(room, new Vec3d(5.5, 1.2, 5.5));
        Assert.NotNull(spot);
        Assert.Equal(6.5, spot!.X, 3);
        Assert.Equal(5.5, spot.Z, 3);
    }

    [Fact]
    public void Walled_In_Rider_Gets_Up_On_Top()
    {
        var room = new Room();
        for (int x = 4; x <= 6; x++) for (int z = 4; z <= 6; z++) if (x != 5 || z != 5) room.Wall(x, z);
        var spot = Spot(room, new Vec3d(5.5, 1.2, 5.5));
        Assert.NotNull(spot);
        Assert.Equal(1.501, spot!.Y, 3);
    }

    [Fact]
    public void A_Bed_Always_Puts_The_Sleeper_Beside_It()
    {
        Assert.NotNull(Spot(new Room(), new Vec3d(5.5, 1.6, 5.5), beside: true));
    }
}
