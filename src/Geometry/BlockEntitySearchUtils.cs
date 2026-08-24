using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Geometry
{
    /// <summary>
    /// Helpers for searching and counting block entities within a region.
    /// </summary>
    public static class BlockEntitySearchUtils
    {
        /// <summary>
        /// Counts block entities matching a predicate within the given radii around a position.
        /// Iterates chunk-by-chunk for efficiency.
        /// </summary>
        /// <param name="pos">The position.</param>
        /// <param name="radiusX">The radius x value.</param>
        /// <param name="radiusY">The radius y value.</param>
        /// <param name="radiusZ">The radius z value.</param>
        /// <param name="blockAccessor">The block accessor value.</param>
        /// <param name="matcher">The matcher value.</param>
        /// <returns>The count block entities.</returns>
        public static int CountBlockEntities(Vec3i pos, int radiusX, int radiusY, int radiusZ, IBlockAccessor blockAccessor, System.Func<BlockEntity, bool> matcher)
        {
            int blockCount = 0;
            int chunksize = GlobalConstants.ChunkSize;
            for (int x = pos.X - radiusX; x <= pos.X + radiusX; x += chunksize)
            {
                for (int y = pos.Y - radiusY; y <= pos.Y + radiusY; y += chunksize)
                {
                    for (int z = pos.Z - radiusZ; z <= pos.Z + radiusZ; z += chunksize)
                    {
                        var chunk = blockAccessor.GetChunkAtBlockPos(new BlockPos(x, y, z, 0));
                        if (chunk == null) { continue; }
                        foreach (var blockEntity in chunk.BlockEntities.Values)
                        {
                            if (matcher.Invoke(blockEntity))
                            {
                                blockCount++;
                            }
                        }
                    }
                }
            }
            return blockCount;
        }
    }
}
