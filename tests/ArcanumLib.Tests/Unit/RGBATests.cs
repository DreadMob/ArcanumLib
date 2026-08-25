using ArcanumLib.Gui.Theme;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class RGBATests
{
    [Fact]
    public void From_NormalizesToZeroToOne()
    {
        var c = RGBA.From(255, 128, 0, 0.5);

        Assert.Equal(1.0, c.R, 5);
        Assert.Equal(128 / 255.0, c.G, 5);
        Assert.Equal(0.0, c.B, 5);
        Assert.Equal(0.5, c.A, 5);
    }

    [Fact]
    public void ParseHexColor_SixDigit_ReturnsColor()
    {
        var c = RGBA.ParseHexColor("#FF8040");

        Assert.NotNull(c);
        Assert.Equal(1.0, c!.Value.R, 5);
        Assert.Equal(128 / 255.0, c.Value.G, 5);
        Assert.Equal(64 / 255.0, c.Value.B, 5);
        Assert.Equal(1.0, c.Value.A, 5);
    }

    [Fact]
    public void ParseHexColor_ThreeDigit_DoublesDigits()
    {
        var c = RGBA.ParseHexColor("#F84");

        Assert.NotNull(c);
        Assert.Equal(1.0, c!.Value.R, 5);
        Assert.Equal(136 / 255.0, c.Value.G, 5);
        Assert.Equal(68 / 255.0, c.Value.B, 5);
    }

    [Fact]
    public void ParseHexColor_WithoutHash_Parses()
    {
        var c = RGBA.ParseHexColor("00FF00");

        Assert.NotNull(c);
        Assert.Equal(0.0, c!.Value.R, 5);
        Assert.Equal(1.0, c.Value.G, 5);
        Assert.Equal(0.0, c.Value.B, 5);
    }

    [Fact]
    public void ParseHexColor_InvalidLength_ReturnsNull()
    {
        Assert.Null(RGBA.ParseHexColor("#12345"));
        Assert.Null(RGBA.ParseHexColor(""));
        Assert.Null(RGBA.ParseHexColor(null!));
        Assert.Null(RGBA.ParseHexColor("   "));
    }

    [Fact]
    public void FromArgb_UnpacksChannels()
    {
        var c = RGBA.FromArgb(unchecked((int)0x80FF8040));

        Assert.Equal(0x80 / 255.0, c.A, 5);
        Assert.Equal(1.0, c.R, 5);
        Assert.Equal(128 / 255.0, c.G, 5);
        Assert.Equal(64 / 255.0, c.B, 5);
    }

    [Fact]
    public void WithAlpha_ReturnsNewColor()
    {
        var c = RGBA.From(10, 20, 30, 1.0);

        var faded = c.WithAlpha(0.25);

        Assert.Equal(0.25, faded.A, 5);
        Assert.Equal(c.R, faded.R);
    }

    [Fact]
    public void Lerp_Midpoint_BlendsChannels()
    {
        var a = new RGBA(0, 0, 0, 0);
        var b = new RGBA(1, 1, 1, 1);

        var mid = a.Lerp(b, 0.5);

        Assert.Equal(0.5, mid.R, 5);
        Assert.Equal(0.5, mid.G, 5);
        Assert.Equal(0.5, mid.B, 5);
        Assert.Equal(0.5, mid.A, 5);
    }

    [Fact]
    public void Lerp_ClampsT()
    {
        var a = new RGBA(0, 0, 0, 0);
        var b = new RGBA(1, 1, 1, 1);

        Assert.Equal(a, a.Lerp(b, -1));
        Assert.Equal(b, a.Lerp(b, 2));
    }
}
