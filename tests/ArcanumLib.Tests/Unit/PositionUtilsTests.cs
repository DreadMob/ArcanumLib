using System;
using ArcanumLib.Geometry;
using Vintagestory.API.MathTools;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PositionUtilsTests
{
    [Fact]
    public void HorizontalDistanceTo_IgnoresY()
    {
        var a = new Vec3d(0, 100, 0);
        var b = new Vec3d(3, -50, 4);

        Assert.Equal(5.0, PositionUtils.HorizontalDistanceTo(a, b), 5);
    }

    [Fact]
    public void DistanceTo_UsesAllAxes()
    {
        var a = new Vec3d(0, 0, 0);
        var b = new Vec3d(1, 2, 2);

        Assert.Equal(3.0, PositionUtils.DistanceTo(a, b), 5);
    }

    [Fact]
    public void SquareDistanceTo_ComputesCorrectly()
    {
        var a = new Vec3d(1, 1, 1);
        var b = new Vec3d(4, 5, 1);

        Assert.Equal(25.0, PositionUtils.SquareDistanceTo(a, b), 5);
    }

    [Fact]
    public void IsWithinRange_WorksFor3D()
    {
        var a = new Vec3d(0, 0, 0);
        var b = new Vec3d(3, 0, 4);

        Assert.True(PositionUtils.IsWithinRange(a, b, 5.0));
        Assert.False(PositionUtils.IsWithinRange(a, b, 4.9));
    }

    [Fact]
    public void IsWithinHorizontalRange_WorksForXZ()
    {
        var a = new Vec3d(0, 100, 0);
        var b = new Vec3d(3, 0, 4);

        Assert.True(PositionUtils.IsWithinHorizontalRange(a, b, 5.0));
        Assert.False(PositionUtils.IsWithinHorizontalRange(a, b, 4.9));
    }

    [Fact]
    public void GetDirectionTo_ReturnsNormalizedVector()
    {
        var from = new Vec3d(0, 0, 0);
        var to = new Vec3d(3, 0, 4);

        var dir = PositionUtils.GetDirectionTo(from, to);

        Assert.Equal(0.6f, dir.X, 5);
        Assert.Equal(0.8f, dir.Z, 5);
        Assert.Equal(0f, dir.Y);
    }

    [Fact]
    public void GetDirectionTo_SamePosition_ReturnsZero()
    {
        var pos = new Vec3d(5, 5, 5);

        var dir = PositionUtils.GetDirectionTo(pos, pos);

        Assert.Equal(Vec3f.Zero, dir);
    }

    [Fact]
    public void GetAngleTo_ReturnsZero_WhenSamePosition()
    {
        var pos = new Vec3d(5, 5, 5);

        Assert.Equal(0.0, PositionUtils.GetAngleTo(pos, pos));
    }

    [Fact]
    public void GetRandomPointInCircle_KeepsYAndStaysWithinRadius()
    {
        var center = new Vec3d(10, 20, 30);
        var random = new Random(0);

        for (int i = 0; i < 20; i++)
        {
            var point = PositionUtils.GetRandomPointInCircle(center, 5.0, random);
            double dist = PositionUtils.HorizontalDistanceTo(center, point);

            Assert.Equal(20.0, point.Y, 5);
            Assert.True(dist <= 5.0 + 1e-9);
        }
    }

    [Fact]
    public void GetRandomPointInLine_KeepsYAndStaysWithinBounds()
    {
        var origin = new Vec3d(0, 10, 0);
        var direction = new Vec3d(10, 0, 0);
        var random = new Random(0);

        for (int i = 0; i < 20; i++)
        {
            var point = PositionUtils.GetRandomPointInLine(origin, direction, 10.0, 2.0, random);

            Assert.Equal(10.0, point.Y, 5);
            Assert.InRange(point.X, 0, 10);
            Assert.InRange(Math.Abs(point.Z), 0, 1.0 + 1e-9);
        }
    }

    [Fact]
    public void GetRandomHorizontalOffset_StaysBetweenMinAndMax()
    {
        var center = new Vec3d(0, 0, 0);
        var random = new Random(0);

        for (int i = 0; i < 20; i++)
        {
            var point = PositionUtils.GetRandomHorizontalOffset(center, 3.0, 7.0, random);
            double dist = PositionUtils.HorizontalDistanceTo(center, point);

            Assert.InRange(dist, 3.0, 7.0 + 1e-9);
        }
    }
}
