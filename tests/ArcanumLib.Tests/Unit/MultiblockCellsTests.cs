using ArcanumLib.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Tests.Unit;

public class MultiblockCellsTests
{
    [Fact]
    public void Selection_Moves_Onto_The_Controller_With_Its_Hit_Point()
    {
        var sel = new BlockSelection { Position = new BlockPos(10, 5, 10), HitPosition = new Vec3d(0.25, 0.5, 0.75), Face = BlockFacing.NORTH };
        var moved = MultiblockCells.ForController(sel, new Vec3i(-2, -1, 1));
        Assert.Equal(new BlockPos(8, 4, 11), moved.Position);
        // the same world point, seen from the controller cell
        Assert.Equal(2.25, moved.HitPosition.X, 6);
        Assert.Equal(1.5, moved.HitPosition.Y, 6);
        Assert.Equal(-0.25, moved.HitPosition.Z, 6);
        Assert.Equal(new BlockPos(10, 5, 10), sel.Position);
    }

    [Fact]
    public void Boxes_Shift_Into_The_Filler_Frame()
    {
        var boxes = MultiblockCells.Shift(new[] { new Cuboidf(0, 0, 0, 1, 2, 1) }, new Vec3i(1, -1, 0));
        Assert.Equal(new Cuboidf(1, -1, 0, 2, 1, 1), boxes[0]);
        Assert.Empty(MultiblockCells.Shift(null, new Vec3i(1, 0, 0)));
    }
}
