using System.Collections.Generic;
using ArcanumLib.Geometry;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class BlockEntitySearchUtilsTests
{
    [Fact]
    public void CountBlockEntities_MatchesPredicate()
    {
        var blockAccessor = Substitute.For<IBlockAccessor>();
        var chunk = Substitute.For<IWorldChunk>();

        var be1 = Substitute.For<BlockEntity>();
        var be2 = Substitute.For<BlockEntity>();

        chunk.BlockEntities.Returns(new Dictionary<BlockPos, BlockEntity>
        {
            [new BlockPos(0, 0, 0)] = be1,
            [new BlockPos(1, 0, 0)] = be2
        });

        blockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns(chunk);

        int count = BlockEntitySearchUtils.CountBlockEntities(new Vec3i(0, 0, 0), 2, 2, 2, blockAccessor, be => be == be1);

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountBlockEntities_NullChunk_Skips()
    {
        var blockAccessor = Substitute.For<IBlockAccessor>();
        blockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns((IWorldChunk?)null);

        int count = BlockEntitySearchUtils.CountBlockEntities(new Vec3i(0, 0, 0), 2, 2, 2, blockAccessor, _ => true);

        Assert.Equal(0, count);
    }

    [Fact]
    public void CountBlockEntities_MultipleChunks()
    {
        var blockAccessor = Substitute.For<IBlockAccessor>();
        var chunk1 = Substitute.For<IWorldChunk>();
        var chunk2 = Substitute.For<IWorldChunk>();

        chunk1.BlockEntities.Returns(new Dictionary<BlockPos, BlockEntity>
        {
            [new BlockPos(0, 0, 0)] = Substitute.For<BlockEntity>()
        });

        chunk2.BlockEntities.Returns(new Dictionary<BlockPos, BlockEntity>
        {
            [new BlockPos(40, 0, 0)] = Substitute.For<BlockEntity>()
        });

        blockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns(callInfo =>
        {
            int x = callInfo.Arg<BlockPos>().X;
            return x < 32 ? chunk1 : chunk2;
        });

        int count = BlockEntitySearchUtils.CountBlockEntities(new Vec3i(0, 0, 0), 60, 2, 2, blockAccessor, _ => true);

        Assert.Equal(4, count);
    }
}
