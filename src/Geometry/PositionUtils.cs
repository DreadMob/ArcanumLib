using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Geometry
{
    /// <summary>
    /// Helpers for computing random positions, ground-level searches, distance checks,
    /// and direction calculations around entities and world coordinates.
    /// </summary>
    public static class PositionUtils
    {
        // ========================================
        // RANDOM HORIZONTAL OFFSETS
        // ========================================

        /// <summary>
        /// Returns a random position on the horizontal plane around <paramref name="center" />,
        /// at <paramref name="center" />.Y, between <paramref name="minDist" /> and <paramref name="maxDist" />.
        /// Distribution is uniform by angle and radius.
        /// </summary>
        /// <param name="center">The center position.</param>
        /// <param name="minDist">The min dist value.</param>
        /// <param name="maxDist">The max dist value.</param>
        /// <param name="rand">The random number generator.</param>
        /// <returns>The random horizontal offset.</returns>
        public static Vec3d GetRandomHorizontalOffset(Vec3d center, double minDist, double maxDist, Random rand)
        {
            double angle = rand.NextDouble() * Math.PI * 2;
            double dist = minDist + rand.NextDouble() * (maxDist - minDist);
            return new Vec3d(
                center.X + Math.Cos(angle) * dist,
                center.Y,
                center.Z + Math.Sin(angle) * dist);
        }

        /// <summary>
        /// Returns a random horizontal offset around an entity's position.
        /// </summary>
        /// <param name="center">The center position.</param>
        /// <param name="minDist">The min dist value.</param>
        /// <param name="maxDist">The max dist value.</param>
        /// <param name="rand">The random number generator.</param>
        /// <returns>The random horizontal offset.</returns>
        public static Vec3d GetRandomHorizontalOffset(Entity center, double minDist, double maxDist, Random rand)
            => GetRandomHorizontalOffset(center?.Pos?.XYZ ?? new Vec3d(), minDist, maxDist, rand);

        // ========================================
        // RANDOM POINT IN SHAPES
        // ========================================

        /// <summary>
        /// Returns a uniformly distributed random point inside a circle of <paramref name="radius" />
        /// centered at <paramref name="center" /> on the horizontal plane.
        /// Uses sqrt sampling so points are not clustered near the center.
        /// </summary>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="rand">The random number generator.</param>
        /// <returns>The random point in circle.</returns>
        public static Vec3d GetRandomPointInCircle(Vec3d center, double radius, Random rand)
        {
            double a = rand.NextDouble() * Math.PI * 2;
            double r = Math.Sqrt(rand.NextDouble()) * radius;
            return new Vec3d(
                center.X + Math.Cos(a) * r,
                center.Y,
                center.Z + Math.Sin(a) * r);
        }

        /// <summary>
        /// Returns a random point inside a cone (circular sector) starting at <paramref name="apex" />,
        /// pointing toward <paramref name="direction" /> with the given <paramref name="radius" />
        /// and <paramref name="halfAngleDegrees" /> (half of the total cone opening).
        /// </summary>
        /// <param name="apex">The three-dimensional vector.</param>
        /// <param name="direction">The direction vector.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="halfAngleDegrees">The half-angle in degrees.</param>
        /// <param name="rand">The random number generator.</param>
        /// <returns>The random point in cone.</returns>
        public static Vec3d GetRandomPointInCone(Vec3d apex, Vec3d direction, double radius, double halfAngleDegrees, Random rand)
        {
            double dirAngle = Math.Atan2(direction.Z, direction.X);
            double halfAngleRad = halfAngleDegrees * Math.PI / 180.0;
            double a = dirAngle + (rand.NextDouble() - 0.5) * 2 * halfAngleRad;
            double r = Math.Sqrt(rand.NextDouble()) * radius;
            return new Vec3d(
                apex.X + Math.Cos(a) * r,
                apex.Y,
                apex.Z + Math.Sin(a) * r);
        }

        /// <summary>
        /// Returns a random point inside a cone (circular sector) starting at <paramref name="apex" />,
        /// pointing toward <paramref name="direction" /> (Vec3f) with the given <paramref name="radius" />
        /// and <paramref name="halfAngleDegrees" /> (half of the total cone opening).
        /// </summary>
        /// <param name="apex">The three-dimensional vector.</param>
        /// <param name="direction">The direction vector.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="halfAngleDegrees">The half-angle in degrees.</param>
        /// <param name="rand">The random number generator.</param>
        /// <returns>The random point in cone.</returns>
        public static Vec3d GetRandomPointInCone(Vec3d apex, Vec3f direction, double radius, double halfAngleDegrees, Random rand)
            => GetRandomPointInCone(apex, new Vec3d(direction.X, direction.Y, direction.Z), radius, halfAngleDegrees, rand);

        /// <summary>
        /// Returns a random point inside a rectangular strip (line) starting at <paramref name="origin" />,
        /// extending along <paramref name="direction" /> for <paramref name="length" /> blocks,
        /// with a perpendicular width of <paramref name="width" /> blocks.
        /// </summary>
        /// <param name="origin">The origin position.</param>
        /// <param name="direction">The direction vector.</param>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="rand">The random number generator.</param>
        /// <returns>The random point in line.</returns>
        public static Vec3d GetRandomPointInLine(Vec3d origin, Vec3d direction, double length, double width, Random rand)
        {
            double dirLen = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
            if (dirLen < 0.001) return origin?.Clone() ?? new Vec3d();

            double ndx = direction.X / dirLen;
            double ndz = direction.Z / dirLen;

            double along = rand.NextDouble() * length;
            double perp = (rand.NextDouble() - 0.5) * width;

            return new Vec3d(
                origin.X + ndx * along + (-ndz) * perp,
                origin.Y,
                origin.Z + ndz * along + ndx * perp);
        }

        /// <summary>
        /// Returns a random point inside a rectangular strip (line) starting at <paramref name="origin" />,
        /// extending along <paramref name="direction" /> (Vec3f) for <paramref name="length" /> blocks,
        /// with a perpendicular width of <paramref name="width" /> blocks.
        /// </summary>
        /// <param name="origin">The origin position.</param>
        /// <param name="direction">The direction vector.</param>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="rand">The random number generator.</param>
        /// <returns>The random point in line.</returns>
        public static Vec3d GetRandomPointInLine(Vec3d origin, Vec3f direction, double length, double width, Random rand)
            => GetRandomPointInLine(origin, new Vec3d(direction.X, direction.Y, direction.Z), length, width, rand);

        // ========================================
        // GROUND-LEVEL SEARCH
        // ========================================

        /// <summary>
        /// Tries to find a random ground-level position around an entity.
        /// Returns false if the terrain height at the chosen location is at or below 0.
        /// </summary>
        /// <param name="center">The center position.</param>
        /// <param name="minDist">The min dist value.</param>
        /// <param name="maxDist">The max dist value.</param>
        /// <param name="blockAccessor">The block accessor value.</param>
        /// <param name="rand">The random number generator.</param>
        /// <param name="groundPos">The three-dimensional vector.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryGetRandomGroundPositionAround(Entity center, double minDist, double maxDist, IBlockAccessor blockAccessor, Random rand, out Vec3d? groundPos)
        {
            groundPos = null;
            if (center?.Pos == null) return false;

            var basePos = GetRandomHorizontalOffset(center, minDist, maxDist, rand);
            int y = blockAccessor.GetTerrainMapheightAt(new BlockPos((int)basePos.X, 0, (int)basePos.Z, center.Pos.Dimension));
            if (y <= 0) return false;

            groundPos = new Vec3d(basePos.X, y + 1, basePos.Z);
            return true;
        }

        /// <summary>
        /// Searches a vertical column around <paramref name="anchorY" /> for a passable floor:
        /// a block where feet and head are passable but the block below is solid.
        /// Returns true and sets <paramref name="feetY" /> when a suitable floor is found.
        /// </summary>
        /// <param name="ba">The ba value.</param>
        /// <param name="pos">The block position.</param>
        /// <param name="anchorY">The anchor y value.</param>
        /// <param name="maxDelta">The max delta value.</param>
        /// <param name="feetY">When this method returns, contains the <paramref name="feetY" /> value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryFindLocalFloor(IBlockAccessor ba, BlockPos pos, int anchorY, int maxDelta, out int feetY)
        {
            feetY = anchorY;
            if (ba == null || pos == null) return false;

            var bp = pos.Copy();
            int minY = anchorY - maxDelta;
            int maxY = anchorY + maxDelta;
            if (minY < 0) minY = 0;

            for (int y = maxY; y >= minY; y--)
            {
                bp.Y = y;
                Block feet = ba.GetBlock(bp);
                if (IsPassable(feet, ba, bp))
                {
                    bp.Y = y + 1;
                    Block head = ba.GetBlock(bp);
                    if (IsPassable(head, ba, bp))
                    {
                        bp.Y = y - 1;
                        Block ground = ba.GetBlock(bp);
                        if (!IsPassable(ground, ba, bp))
                        {
                            feetY = y;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the block has no collision boxes (or is null), i.e. entities can pass through it.
        /// </summary>
        /// <param name="block">The block value.</param>
        /// <param name="ba">The ba value.</param>
        /// <param name="pos">The block position.</param>
        /// <returns>true if passable; otherwise, false.</returns>
        public static bool IsPassable(Block block, IBlockAccessor ba, BlockPos pos)
        {
            if (block == null) return true;
            var boxes = block.GetCollisionBoxes(ba, pos);
            return boxes == null || boxes.Length == 0;
        }

        // ========================================
        // HORIZONTAL DISTANCE
        // ========================================

        /// <summary>
        /// Returns the horizontal (XZ-plane) distance between two positions, ignoring Y.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>The horizontal distance to.</returns>
        public static double HorizontalDistanceTo(Vec3d a, Vec3d b)
        {
            if (a == null || b == null) return double.MaxValue;
            double dx = a.X - b.X;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Returns the horizontal (XZ-plane) distance between two entities, ignoring Y.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>The horizontal distance to.</returns>
        public static double HorizontalDistanceTo(Entity a, Entity b)
        {
            if (a?.Pos == null || b?.Pos == null) return double.MaxValue;
            return HorizontalDistanceTo(a.Pos.XYZ, b.Pos.XYZ);
        }

        /// <summary>
        /// Returns the squared horizontal (XZ-plane) distance between two positions, ignoring Y.
        /// Faster than <see cref="HorizontalDistanceTo(Vec3d, Vec3d)" /> for range comparisons.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>The horizontal square distance to.</returns>
        public static double HorizontalSquareDistanceTo(Vec3d a, Vec3d b)
        {
            if (a == null || b == null) return double.MaxValue;
            double dx = a.X - b.X;
            double dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// Returns the 3D distance between two positions.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>The distance to.</returns>
        public static double DistanceTo(Vec3d a, Vec3d b)
        {
            if (a == null || b == null) return double.MaxValue;
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Returns the 3D distance between an entity and a position.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="point">The three-dimensional vector.</param>
        /// <returns>The distance to.</returns>
        public static double DistanceTo(Entity entity, Vec3d point)
        {
            if (entity?.Pos == null || point == null) return double.MaxValue;
            return DistanceTo(entity.Pos.XYZ, point);
        }

        /// <summary>
        /// Returns the squared 3D distance between two positions.
        /// Faster than <see cref="DistanceTo(Vec3d, Vec3d)" /> for range comparisons.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>The square distance to.</returns>
        public static double SquareDistanceTo(Vec3d a, Vec3d b)
        {
            if (a == null || b == null) return double.MaxValue;
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// Returns true if <paramref name="point" /> is within <paramref name="range" /> blocks of
        /// <paramref name="center" /> in 3D.
        /// </summary>
        /// <param name="point">The three-dimensional vector.</param>
        /// <param name="center">The center position.</param>
        /// <param name="range">The range value.</param>
        /// <returns>true if within range; otherwise, false.</returns>
        public static bool IsWithinRange(Vec3d point, Vec3d center, double range)
            => DistanceTo(point, center) <= range;

        /// <summary>
        /// Returns true if <paramref name="entity" /> is within <paramref name="range" /> blocks of
        /// <paramref name="center" /> in 3D.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="center">The center position.</param>
        /// <param name="range">The range value.</param>
        /// <returns>true if within range; otherwise, false.</returns>
        public static bool IsWithinRange(Entity entity, Vec3d center, double range)
        {
            if (entity?.Pos == null) return false;
            return IsWithinRange(entity.Pos.XYZ, center, range);
        }

        /// <summary>
        /// Returns true if <paramref name="point" /> is within <paramref name="range" /> blocks of
        /// <paramref name="center" /> on the horizontal plane (ignoring Y).
        /// </summary>
        /// <param name="point">The three-dimensional vector.</param>
        /// <param name="center">The center position.</param>
        /// <param name="range">The range value.</param>
        /// <returns>true if within horizontal range; otherwise, false.</returns>
        public static bool IsWithinHorizontalRange(Vec3d point, Vec3d center, double range)
            => HorizontalDistanceTo(point, center) <= range;

        /// <summary>
        /// Returns true if <paramref name="entity" /> is within <paramref name="range" /> blocks of
        /// <paramref name="center" /> on the horizontal plane (ignoring Y).
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="center">The center position.</param>
        /// <param name="range">The range value.</param>
        /// <returns>true if within horizontal range; otherwise, false.</returns>
        public static bool IsWithinHorizontalRange(Entity entity, Vec3d center, double range)
        {
            if (entity?.Pos == null) return false;
            return HorizontalDistanceTo(entity.Pos.XYZ, center) <= range;
        }

        // ========================================
        // DIRECTION & ANGLE
        // ========================================

        /// <summary>
        /// Returns the normalized horizontal direction from <paramref name="from" /> to <paramref name="to" />
        /// as a <see cref="Vec3f" /> with Y = 0. Returns <see cref="Vec3f.Zero" /> if the positions coincide.
        /// </summary>
        /// <param name="from">The three-dimensional vector.</param>
        /// <param name="to">The three-dimensional vector.</param>
        /// <returns>The direction to.</returns>
        public static Vec3f GetDirectionTo(Vec3d from, Vec3d to)
        {
            if (from == null || to == null) return Vec3f.Zero;
            double dx = to.X - from.X;
            double dz = to.Z - from.Z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.0001) return Vec3f.Zero;
            return new Vec3f((float)(dx / len), 0f, (float)(dz / len));
        }

        /// <summary>
        /// Returns the horizontal angle (atan2) from <paramref name="from" /> to <paramref name="to" />
        /// in radians. 0 points along +X, increasing clockwise toward +Z.
        /// </summary>
        /// <param name="from">The three-dimensional vector.</param>
        /// <param name="to">The three-dimensional vector.</param>
        /// <returns>The angle to.</returns>
        public static double GetAngleTo(Vec3d from, Vec3d to)
        {
            if (from == null || to == null) return 0;
            return Math.Atan2(to.Z - from.Z, to.X - from.X);
        }

        /// <summary>
        /// Linearly interpolates between <paramref name="a" /> and <paramref name="b" /> by <paramref name="t" />.
        /// </summary>
        /// <param name="a">The three-dimensional vector.</param>
        /// <param name="b">The three-dimensional vector.</param>
        /// <param name="t">The t value.</param>
        /// <returns>The lerp position.</returns>
        public static Vec3d LerpPosition(Vec3d a, Vec3d b, double t)
        {
            if (a == null) return b?.Clone() ?? new Vec3d();
            if (b == null) return a.Clone();
            if (t <= 0) return a.Clone();
            if (t >= 1) return b.Clone();
            return new Vec3d(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }
    }
}
