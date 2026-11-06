#nullable enable
using ArcanumLib.Gui.Controls;
using Xunit;

namespace ArcanumLib.Tests.Unit;

/// <summary>Outline of a model under the GUI transform, which sizes slot icons by their real shape.</summary>
public class GuiItemFitTests
{
    /// <summary>Corners of an axis-aligned box as a vertex array.</summary>
    private static float[] Box(float x0, float y0, float z0, float x1, float y1, float z1)
    {
        var v = new System.Collections.Generic.List<float>();
        foreach (var x in new[] { x0, x1 })
            foreach (var y in new[] { y0, y1 })
                foreach (var z in new[] { z0, z1 })
                    v.AddRange(new[] { x, y, z });
        return v.ToArray();
    }

    private static (double w, double h) Size(float[] xyz, float sx = 1, float rx = 0, float ry = 0, bool block = false, bool spin = false)
    {
        var r = GuiItemFit.ProjectRaw(xyz, xyz.Length / 3, 0.5f, 0.5f, 0.5f, sx, sx, rx, ry, 0, block, spin)!.Value;
        return (r.maxX - r.minX, r.maxY - r.minY);
    }

    [Fact]
    public void UnitCube_FacingFront_IsOneByOne()
    {
        var (w, h) = Size(Box(0, 0, 0, 1, 1, 1), block: true);
        Assert.Equal(1, w, 3);
        Assert.Equal(1, h, 3);
    }

    [Fact]
    public void TallModel_IsTallerThanAUnitCube()
    {
        var (w, h) = Size(Box(0, 0, 0, 1, 2.5f, 1), block: true);
        Assert.Equal(1, w, 3);
        Assert.Equal(2.5, h, 3);
    }

    [Fact]
    public void GuiScale_ScalesTheOutline()
    {
        var (w, _) = Size(Box(0, 0, 0, 1, 1, 1), sx: 2);
        Assert.Equal(2, w, 3);
    }

    [Fact]
    public void Turned45_ShowsTheDiagonal()
    {
        var (w, _) = Size(Box(0, 0, 0, 1, 1, 1), ry: 45);
        Assert.Equal(System.Math.Sqrt(2), w, 3);
    }

    [Fact]
    public void Spinning_CoversEveryTurn()
    {
        var (still, _) = Size(Box(0, 0, 0, 1, 1, 1));
        var (spun, _) = Size(Box(0, 0, 0, 1, 1, 1), spin: true);
        Assert.True(spun > still + 0.3);
    }

    [Fact]
    public void FlatPlate_SeenFromAbove_KeepsItsWidth()
    {
        // vanilla-style -30° tilt: a thin plate still projects as wide as it is
        var (w, h) = Size(Box(0, 0, 0, 1, 0.06f, 1), rx: -30);
        Assert.Equal(1, w, 3);
        Assert.True(h < 0.6);
    }
}
