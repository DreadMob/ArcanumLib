using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Hud;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class HudThemeColorsTests
{
    private static HudThemeColors MakeBase() => new()
    {
        bgTop = "#000000", bgBottom = "#111111", bgAlpha = 0.5,
        border = "#222222", borderAlpha = 0.6,
        textPrimary = "#333333", textSecondary = "#444444",
        accent = "#555555", accentSecondary = "#666666",
        danger = "#777777", success = "#888888",
        barBg = "#999999", barFill = "#AAAAAA", barFillLow = "#BBBBBB",
        parchment = "#CCCCCC", title = "#DDDDDD", pending = "#EEEEEE"
    };

    [Fact]
    public void Merge_NullOther_ReturnsSelf()
    {
        var c = MakeBase();

        var merged = c.Merge(null!);

        Assert.Same(c, merged);
    }

    [Fact]
    public void Merge_KeepsBaseValues_WhenOtherFieldsNull()
    {
        var c = MakeBase();
        var overlay = new HudThemeColors
        {
            bgTop = "#FFFFFF",
            bgBottom = "#111111", border = "#222222", textPrimary = "#333333",
            textSecondary = "#444444", accent = "#555555", accentSecondary = "#666666",
            danger = "#777777", success = "#888888", barBg = "#999999",
            barFill = "#AAAAAA", barFillLow = "#BBBBBB", parchment = "#CCCCCC",
            title = "#DDDDDD", pending = "#EEEEEE"
        };

        var merged = c.Merge(overlay);

        Assert.Equal("#FFFFFF", merged.bgTop);
        Assert.Equal(0.5, merged.bgAlpha);
        Assert.Equal("#222222", merged.border);
    }

    [Fact]
    public void Merge_OverridesAll_WhenOtherFullySpecified()
    {
        var c = MakeBase();
        var overlay = new HudThemeColors
        {
            bgTop = "#AAA", bgBottom = "#BBB", bgAlpha = 0.1,
            border = "#CCC", borderAlpha = 0.2,
            textPrimary = "#DDD", textSecondary = "#EEE",
            accent = "#FFF", accentSecondary = "#000",
            danger = "#111", success = "#222",
            barBg = "#333", barFill = "#444", barFillLow = "#555",
            parchment = "#666", title = "#777", pending = "#888"
        };

        var merged = c.Merge(overlay);

        Assert.Equal("#AAA", merged.bgTop);
        Assert.Equal(0.1, merged.bgAlpha);
        Assert.Equal("#888", merged.pending);
    }
}

public class HudThemeFontsTests
{
    [Fact]
    public void Merge_NullOther_ReturnsSelf()
    {
        var f = new HudThemeFonts { label = 10, value = 12, timer = 14, title = 16 };

        Assert.Same(f, f.Merge(null!));
    }

    [Fact]
    public void Merge_OverridesOnlyNonNullFields()
    {
        var f = new HudThemeFonts { label = 10, value = 12, timer = 14, title = 16 };
        var overlay = new HudThemeFonts { label = 20 };

        var merged = f.Merge(overlay);

        Assert.Equal(20, merged.label);
        Assert.Equal(12, merged.value);
        Assert.Equal(14, merged.timer);
        Assert.Equal(16, merged.title);
    }
}

public class HudThemeLayoutTests
{
    [Fact]
    public void Default_HasAllFieldsSet()
    {
        var d = HudThemeLayout.Default;

        Assert.NotNull(d.lineHeight);
        Assert.NotNull(d.headerHeight);
        Assert.NotNull(d.barHeight);
        Assert.NotNull(d.barWidth);
        Assert.NotNull(d.iconSize);
    }

    [Fact]
    public void Merge_NullOther_ReturnsSelf()
    {
        var l = new HudThemeLayout();

        Assert.Same(l, l.Merge(null!));
    }

    [Fact]
    public void Merge_OverridesNonNullFields()
    {
        var l = new HudThemeLayout();
        var overlay = new HudThemeLayout { lineHeight = 30, barHeight = 20 };

        var merged = l.Merge(overlay);

        Assert.Equal(30, merged.lineHeight);
        Assert.Equal(20, merged.barHeight);
    }

    [Fact]
    public void Merge_FallsBackToDefault_WhenBothNull()
    {
        var l = new HudThemeLayout();
        var overlay = new HudThemeLayout { lineHeight = 25 };

        var merged = l.Merge(overlay);

        // lineHeight overridden, but barWidth falls back to Default
        Assert.Equal(25, merged.lineHeight);
        Assert.Equal(HudThemeLayout.Default.barWidth, merged.barWidth);
    }
}

public class HudThemeTests
{
    [Fact]
    public void CreateDefault_HasExpectedValues()
    {
        var t = HudTheme.CreateDefault();

        Assert.Equal("cartouche", t.frame);
        Assert.Equal("ankh", t.frameSymbol);
        Assert.Equal(260, t.panelWidth);
        Assert.Equal(600, t.maxPanelWidth);
        Assert.Equal(10, t.padding);
        Assert.False(t.textShadow);
        Assert.NotNull(t.colors);
        Assert.NotNull(t.fonts);
        Assert.NotNull(t.layout);
    }

    [Fact]
    public void Merge_NullOverlay_ReturnsSelf()
    {
        var t = HudTheme.CreateDefault();

        Assert.Same(t, t.Merge(null!));
    }

    [Fact]
    public void Merge_OverridesNonNullFields()
    {
        var t = HudTheme.CreateDefault();
        var overlay = new HudTheme
        {
            frame = "bone",
            frameSymbol = "crossbone",
            colors = t.colors,
            fonts = t.fonts,
            layout = t.layout
        };

        var merged = t.Merge(overlay);

        Assert.Equal("bone", merged.frame);
        Assert.Equal("crossbone", merged.frameSymbol);
        Assert.Equal(260, merged.panelWidth); // not overridden
    }

    [Fact]
    public void Merge_PreservesBaseColors_WhenOverlayColorsNull()
    {
        var t = HudTheme.CreateDefault();
        var overlay = new HudTheme
        {
            frame = "glass",
            frameSymbol = "void",
            colors = null!,
            fonts = null!,
            layout = null!
        };

        var merged = t.Merge(overlay);

        Assert.Same(t.colors, merged.colors);
        Assert.Same(t.fonts, merged.fonts);
        Assert.Same(t.layout, merged.layout);
    }
}

public class HudThemeResolverTests
{
    private static HudTheme MakeTheme(string frame) => new()
    {
        frame = frame,
        frameSymbol = "ankh",
        colors = new HudThemeColors
        {
            bgTop = "#000", bgBottom = "#111", border = "#222",
            textPrimary = "#333", textSecondary = "#444",
            accent = "#555", accentSecondary = "#666",
            danger = "#777", success = "#888",
            barBg = "#999", barFill = "#AAA", barFillLow = "#BBB",
            parchment = "#CCC", title = "#DDD", pending = "#EEE"
        },
        fonts = new HudThemeFonts(),
        layout = new HudThemeLayout()
    };

    [Fact]
    public void Resolve_EmptyName_ReturnsBase()
    {
        var baseTheme = MakeTheme("base");

        var result = HudThemeResolver.Resolve("", null, null, baseTheme);

        Assert.Same(baseTheme, result);
    }

    [Fact]
    public void Resolve_WhitespaceName_ReturnsBase()
    {
        var baseTheme = MakeTheme("base");

        var result = HudThemeResolver.Resolve("   ", null, null, baseTheme);

        Assert.Same(baseTheme, result);
    }

    [Fact]
    public void Resolve_NullName_ReturnsBase()
    {
        var baseTheme = MakeTheme("base");

        var result = HudThemeResolver.Resolve(null!, null, null, baseTheme);

        Assert.Same(baseTheme, result);
    }

    [Fact]
    public void Resolve_UnknownName_NoFactory_ReturnsBase()
    {
        var baseTheme = MakeTheme("base");

        var result = HudThemeResolver.Resolve("unknown", null, null, baseTheme);

        Assert.Same(baseTheme, result);
    }

    [Fact]
    public void Resolve_CustomTheme_OverridesBuiltIn()
    {
        var baseTheme = MakeTheme("base");
        var custom = MakeTheme("custom");
        var builtIn = MakeTheme("builtin");
        var customThemes = new Dictionary<string, HudTheme> { ["mytheme"] = custom };

        var result = HudThemeResolver.Resolve("mytheme", customThemes, name => builtIn, baseTheme);

        // Custom takes priority; merged over builtIn, then over base
        Assert.Equal("custom", result.frame);
    }

    [Fact]
    public void Resolve_BuiltInOnly_WhenNoCustom()
    {
        var baseTheme = MakeTheme("base");
        var builtIn = MakeTheme("builtin");

        var result = HudThemeResolver.Resolve("builtin", null, name => builtIn, baseTheme);

        Assert.Equal("builtin", result.frame);
    }

    [Fact]
    public void Resolve_CustomWithoutBuiltIn_MergesOverBase()
    {
        var baseTheme = MakeTheme("base");
        var custom = MakeTheme("custom");
        var customThemes = new Dictionary<string, HudTheme> { ["mytheme"] = custom };

        var result = HudThemeResolver.Resolve("mytheme", customThemes, null, baseTheme);

        Assert.Equal("custom", result.frame);
    }
}
