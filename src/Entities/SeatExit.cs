#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Entities;

/// <summary>Where someone getting up from a seat or a bed ends up. Left at the seat they would stand inside the block's collision boxes,
/// and the physics does not push out of a box the player is already in: they sink through the furniture.
/// They step onto the floor around the block, at the free place nearest the seat; only the ring right around the block's footprint counts, so nobody ends up behind a wall.</summary>
public static class SeatExit
{
    private const double Lift = 0.001;

    /// <summary>Moves the entity out of the block's way, if it is in the way. The server tells the client by the usual teleport.</summary>
    /// <param name="beside">A bed: the sleeper gets up beside it on the floor, as from a vanilla bed, even when the mattress would hold them.</param>
    public static void StandUp(IWorldAccessor world, EntityAgent agent, Block block, BlockPos at, Vec3d seat, bool beside = false)
    {
        if (world.Side != EnumAppSide.Server) return;
        var spot = Spot(world, agent, block, at, seat, beside);
        if (spot == null) return;
        spot.Y %= 32768;    // a teleport stays in the entity's dimension
        agent.TeleportTo(spot);
    }

    /// <summary>The place to stand, in internal coordinates; null when the entity is free where it is or there is nowhere better.</summary>
    public static Vec3d? Spot(IWorldAccessor world, EntityAgent agent, Block block, BlockPos at, Vec3d seat, bool beside = false)
    {
        var size = agent.Properties.CollisionBoxSize;
        var body = new Cuboidf(-size.X / 2, 0, -size.X / 2, size.X / 2, size.Y, size.X / 2);
        var ba = world.BlockAccessor;
        var tester = world.CollisionTester;
        if (!beside && !tester.IsColliding(ba, body, new Vec3d(seat.X, at.InternalY + (seat.Y - at.Y), seat.Z), false)) return null;

        var boxes = block.GetCollisionBoxes(ba, at);
        if (boxes == null || boxes.Length == 0) return null;
        int x0 = at.X + (int)Math.Floor(boxes.Min(b => b.X1)), x1 = at.X + (int)Math.Ceiling(boxes.Max(b => b.X2)) - 1;
        int z0 = at.Z + (int)Math.Floor(boxes.Min(b => b.Z1)), z1 = at.Z + (int)Math.Ceiling(boxes.Max(b => b.Z2)) - 1;

        var ring = Ring(x0, x1, z0, z1).OrderBy(c => Square(c.x + 0.5 - seat.X, c.z + 0.5 - seat.Z)).ToList();
        foreach (int dy in new[] { 0, 1, -1 })
            foreach (var (x, z) in ring)
                if (FloorSpot(ba, tester, body, x, at.InternalY + dy, z) is { } spot) return spot;

        // walled in all around: on top of the block, and only then a free place with nothing under it
        var top = new Vec3d(seat.X, at.InternalY + boxes.Max(b => b.Y2) + Lift, seat.Z);
        if (!tester.IsColliding(ba, body, top, false)) return top;
        foreach (var (x, z) in ring)
        {
            var open = new Vec3d(x + 0.5, at.InternalY + Lift, z + 0.5);
            if (!tester.IsColliding(ba, body, open, false)) return open;
        }
        return null;
    }

    /// <summary>A standing place in this cell: on top of what is in it, or on the block below when the cell is empty.</summary>
    private static Vec3d? FloorSpot(IBlockAccessor ba, CollisionTester tester, Cuboidf body, int x, int y, int z)
    {
        float top = Top(ba, x, y, z);
        if (top > 1f || (top <= 0f && Top(ba, x, y - 1, z) < 1f)) return null;
        var spot = new Vec3d(x + 0.5, y + top + Lift, z + 0.5);
        return tester.IsColliding(ba, body, spot, false) ? null : spot;
    }

    private static float Top(IBlockAccessor ba, int x, int y, int z)
    {
        var pos = new BlockPos(x, y, z);
        var boxes = ba.GetBlock(pos).GetCollisionBoxes(ba, pos);
        return boxes == null || boxes.Length == 0 ? 0f : boxes.Max(b => b.Y2);
    }

    private static IEnumerable<(int x, int z)> Ring(int x0, int x1, int z0, int z1)
    {
        for (int x = x0 - 1; x <= x1 + 1; x++)
        {
            yield return (x, z0 - 1);
            yield return (x, z1 + 1);
        }
        for (int z = z0; z <= z1; z++)
        {
            yield return (x0 - 1, z);
            yield return (x1 + 1, z);
        }
    }

    private static double Square(double x, double z) => x * x + z * z;
}
