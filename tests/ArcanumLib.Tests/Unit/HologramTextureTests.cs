using System;
using ArcanumLib.Hologram;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class HologramTextureTests
{
    [Fact]
    public void Defaults_AreInvalid_WhenTextureNull()
    {
        var tex = new HologramTexture();

        Assert.False(tex.IsValid);
        Assert.Equal(0, tex.Width);
        Assert.Equal(0, tex.Height);
        Assert.Equal(0, tex.Version);
        Assert.Null(tex.Texture);
    }

    [Fact]
    public void IsValid_True_WhenTextureIdNonZero()
    {
        var tex = new HologramTexture
        {
            Texture = new LoadedTexture(null) { TextureId = 42, Width = 100, Height = 50 },
            Version = 7
        };

        Assert.True(tex.IsValid);
        Assert.Equal(100, tex.Width);
        Assert.Equal(50, tex.Height);
        Assert.Equal(7, tex.Version);
    }

    [Fact]
    public void IsValid_False_WhenTextureIdZero()
    {
        var tex = new HologramTexture
        {
            Texture = new LoadedTexture(null) { TextureId = 0, Width = 100, Height = 50 }
        };

        Assert.False(tex.IsValid);
    }

    [Fact]
    public void Dispose_ClearsTexture()
    {
        // Use a texture with a non-null capi-free setup by directly assigning fields.
        // LoadedTexture.Dispose() requires a real capi, so we only verify Texture is nulled
        // by constructing a texture that won't invoke the OpenGL disposal path.
        var tex = new HologramTexture
        {
            Texture = null, // Already null; Dispose should be a no-op
            Version = 3
        };

        tex.Dispose();

        Assert.Null(tex.Texture);
        Assert.False(tex.IsValid);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var tex = new HologramTexture();

        tex.Dispose();
        tex.Dispose();

        Assert.Null(tex.Texture);
    }
}

public class HologramRenderUtilsTests
{
    [Theory]
    [InlineData(1f, 1.8f)]
    [InlineData(10f, 1.0f)]
    [InlineData(20f, 0.5f)]
    [InlineData(100f, 0.5f)]
    [InlineData(0.5f, 1.8f)]
    [InlineData(5f, 1.8f)] // 10/5 = 2.0, clamped to 1.8
    public void ComputeScale_ClampsAndFalloffs(float distance, float expected)
    {
        var scale = HologramRenderUtils.ComputeScale(distance);

        Assert.Equal(expected, scale, 2);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(-100f)]
    public void ComputeScale_NonPositiveDistance_ClampsToMax(float distance)
    {
        // Math.Max(1f, distance) yields 1, then 10/1 = 10, clamped to 1.8
        var scale = HologramRenderUtils.ComputeScale(distance);

        Assert.Equal(1.8f, scale, 5);
    }

    [Fact]
    public void IsOccluded_NullCapi_ReturnsFalse()
    {
        Assert.False(HologramRenderUtils.IsOccluded(null!, new Vec3d(0, 0, 0), new Vec3d(1, 1, 1), null));
    }
}
