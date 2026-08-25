using System;
using ArcanumLib.Gui.Theme;
using ArcanumLib.Hologram;
using Cairo;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class HologramTextureOptionsTests
{
    [Fact]
    public void Defaults_AreSane()
    {
        var opts = new HologramTextureOptions();

        Assert.Equal(24f, opts.FontSize);
        Assert.Equal(520f, opts.LineWidth);
        Assert.Equal(1.45f, opts.LineHeightMultiplier);
        Assert.Equal(0, opts.MaxLines);
        Assert.Equal(10, opts.PaddingTop);
        Assert.Equal(14, opts.PaddingBottom);
        Assert.Equal(12, opts.PaddingX);
        Assert.True(opts.Centered);
        Assert.True(opts.DrawBackground);
        Assert.Equal("Sans", opts.FontFace);
        Assert.NotNull(opts.BackgroundColor);
        Assert.NotNull(opts.BorderColor);
        Assert.NotNull(opts.ShadowColor);
        Assert.Null(opts.TextColor);
    }

    [Fact]
    public void Clone_PreservesAllFields()
    {
        var opts = new HologramTextureOptions
        {
            FontSize = 32f,
            LineWidth = 600f,
            LineHeightMultiplier = 1.6f,
            MaxLines = 5,
            PaddingTop = 5,
            PaddingBottom = 8,
            PaddingX = 10,
            Centered = false,
            BackgroundColor = new RGBA(0.1, 0.2, 0.3, 0.4),
            BorderColor = new RGBA(0.5, 0.6, 0.7, 0.8),
            TextColor = new RGBA(0.9, 0.95, 1.0, 1.0),
            DrawBackground = false,
            ShadowColor = null,
            FontFace = "Serif",
            FontWeight = FontWeight.Normal,
            FontSlant = FontSlant.Italic
        };

        var clone = opts.Clone();

        Assert.Equal(opts.FontSize, clone.FontSize);
        Assert.Equal(opts.LineWidth, clone.LineWidth);
        Assert.Equal(opts.LineHeightMultiplier, clone.LineHeightMultiplier);
        Assert.Equal(opts.MaxLines, clone.MaxLines);
        Assert.Equal(opts.PaddingTop, clone.PaddingTop);
        Assert.Equal(opts.PaddingBottom, clone.PaddingBottom);
        Assert.Equal(opts.PaddingX, clone.PaddingX);
        Assert.Equal(opts.Centered, clone.Centered);
        Assert.Equal(opts.BackgroundColor, clone.BackgroundColor);
        Assert.Equal(opts.BorderColor, clone.BorderColor);
        Assert.Equal(opts.TextColor, clone.TextColor);
        Assert.Equal(opts.DrawBackground, clone.DrawBackground);
        Assert.Equal(opts.ShadowColor, clone.ShadowColor);
        Assert.Equal(opts.FontFace, clone.FontFace);
        Assert.Equal(opts.FontWeight, clone.FontWeight);
        Assert.Equal(opts.FontSlant, clone.FontSlant);
    }

    [Fact]
    public void Clone_IsIndependent_FromOriginal()
    {
        var opts = new HologramTextureOptions { FontSize = 16f };
        var clone = opts.Clone();

        clone.FontSize = 99f;

        Assert.Equal(16f, opts.FontSize);
        Assert.Equal(99f, clone.FontSize);
    }
}
