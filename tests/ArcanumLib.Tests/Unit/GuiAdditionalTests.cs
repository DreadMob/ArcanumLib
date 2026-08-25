using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Gui.Hud;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.Theme;
using NSubstitute;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class HudDefinitionTests
{
    private class TestElement : HudElementDefinition { }

    [Fact]
    public void EffectiveTheme_PrefersTheme_WhenSet()
    {
        var def = new HudDefinition<TestElement>
        {
            layout = "default",
            style = "bone",
            theme = "custom",
            elements = new List<TestElement>()
        };

        Assert.Equal("custom", def.EffectiveTheme);
    }

    [Fact]
    public void EffectiveTheme_FallsBackToStyle_WhenThemeEmpty()
    {
        var def = new HudDefinition<TestElement>
        {
            layout = "default",
            style = "bone",
            theme = "",
            elements = new List<TestElement>()
        };

        Assert.Equal("bone", def.EffectiveTheme);
    }

    [Fact]
    public void EffectiveTheme_FallsBackToDefault_WhenBothEmpty()
    {
        var def = new HudDefinition<TestElement>
        {
            layout = "default",
            style = "",
            theme = "",
            elements = new List<TestElement>()
        };

        Assert.Equal("default", def.EffectiveTheme);
    }

    [Fact]
    public void EffectiveTheme_FallsBackToDefault_WhenBothWhitespace()
    {
        var def = new HudDefinition<TestElement>
        {
            layout = "default",
            style = "   ",
            theme = "   ",
            elements = new List<TestElement>()
        };

        Assert.Equal("default", def.EffectiveTheme);
    }

    [Fact]
    public void Enabled_DefaultsToTrue()
    {
        var def = new HudDefinition<TestElement>
        {
            layout = "default",
            style = "default",
            theme = "default",
            elements = new List<TestElement>()
        };

        Assert.True(def.enabled);
    }

    [Fact]
    public void PlayerBoardPosition_DefaultsToBottom()
    {
        var def = new HudDefinition<TestElement>
        {
            layout = "default",
            style = "default",
            theme = "default",
            elements = new List<TestElement>()
        };

        Assert.Equal("bottom", def.playerBoardPosition);
    }
}

public class HudElementDefinitionTests
{
    [Fact]
    public void Enabled_DefaultsToTrue()
    {
        var el = new HudElementDefinition();
        Assert.True(el.enabled);
    }

    [Fact]
    public void Position_DefaultsToTopCenter()
    {
        var el = new HudElementDefinition();
        Assert.Equal("top-center", el.position);
    }

    [Fact]
    public void FontScale_DefaultsToOne()
    {
        var el = new HudElementDefinition();
        Assert.Equal(1.0f, el.fontScale);
    }

    [Fact]
    public void OffsetY_DefaultsToZero()
    {
        var el = new HudElementDefinition();
        Assert.Equal(0, el.offsetY);
    }

    [Fact]
    public void OffsetX_DefaultsToZero()
    {
        var el = new HudElementDefinition();
        Assert.Equal(0, el.offsetX);
    }

    [Fact]
    public void Type_CanBeSetViaInit()
    {
        var el = new HudElementDefinition { type = "bar" };
        Assert.Equal("bar", el.type);
    }

    [Fact]
    public void Format_CanBeSetViaInit()
    {
        var el = new HudElementDefinition { format = "{0}/{1}" };
        Assert.Equal("{0}/{1}", el.format);
    }

    [Fact]
    public void TextKey_CanBeSetViaInit()
    {
        var el = new HudElementDefinition { textKey = "mykey" };
        Assert.Equal("mykey", el.textKey);
    }

    [Fact]
    public void Icon_CanBeSetViaInit()
    {
        var el = new HudElementDefinition { icon = "myicon" };
        Assert.Equal("myicon", el.icon);
    }

    [Fact]
    public void ShowBar_DefaultsToFalse()
    {
        var el = new HudElementDefinition();
        Assert.False(el.showBar);
    }

    [Fact]
    public void ShowIf_CanBeSetViaInit()
    {
        var el = new HudElementDefinition { showIf = "cond1,cond2" };
        Assert.Equal("cond1,cond2", el.showIf);
    }
}

public class VectorIconTests
{
    [Fact]
    public void Constructor_NullDraw_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new VectorIcon(null!));
    }

    [Fact]
    public void Draw_WithColor_InvokesDelegate()
    {
        bool invoked = false;
        var icon = new VectorIcon((ctx, cx, cy, r, color) => invoked = true);

        icon.Draw(null!, 0, 0, 10, new RGBA(1, 0, 0, 1));

        Assert.True(invoked);
    }

    [Fact]
    public void Draw_WithoutColor_InvokesDelegateWithDefault()
    {
        RGBA captured = default;
        var icon = new VectorIcon((ctx, cx, cy, r, color) => captured = color);

        icon.Draw(null!, 0, 0, 10);

        Assert.Equal(default(RGBA), captured);
    }
}

public class CustomIconRegistryTests
{
    private class TestRenderer : ICustomIconRenderer
    {
        public void Draw(Cairo.Context ctx, double cx, double cy, double radius, RGBA color) { }
        public void Draw(Cairo.Context ctx, double cx, double cy, double radius) { }
    }

    [Fact]
    public void Register_NullKey_DoesNothing()
    {
        CustomIconRegistry.Register(null!, new TestRenderer());
        Assert.False(CustomIconRegistry.Has(null!));
    }

    [Fact]
    public void Register_EmptyKey_DoesNothing()
    {
        CustomIconRegistry.Register("", new TestRenderer());
        Assert.False(CustomIconRegistry.Has(""));
    }

    [Fact]
    public void Register_NullRenderer_DoesNothing()
    {
        CustomIconRegistry.Register("test-null-renderer", (ICustomIconRenderer)null!);
        Assert.False(CustomIconRegistry.Has("test-null-renderer"));
    }

    [Fact]
    public void Register_AndTryGet_RoundTrip()
    {
        var renderer = new TestRenderer();
        try
        {
            CustomIconRegistry.Register("test-roundtrip", renderer);

            Assert.True(CustomIconRegistry.TryGet("test-roundtrip", out var retrieved));
            Assert.Same(renderer, retrieved);
        }
        finally
        {
            CustomIconRegistry.Unregister("test-roundtrip");
        }
    }

    [Fact]
    public void Register_DelegateOverload_CreatesVectorIcon()
    {
        try
        {
            CustomIconRegistry.Register("test-delegate", (ctx, cx, cy, r, color) => { });

            Assert.True(CustomIconRegistry.Has("test-delegate"));
            Assert.True(CustomIconRegistry.TryGet("test-delegate", out var renderer));
            Assert.IsType<VectorIcon>(renderer);
        }
        finally
        {
            CustomIconRegistry.Unregister("test-delegate");
        }
    }

    [Fact]
    public void Register_DelegateOverload_NullDelegate_DoesNothing()
    {
        CustomIconRegistry.Register("test-null-delegate", (Action<Cairo.Context, double, double, double, RGBA>)null!);
        Assert.False(CustomIconRegistry.Has("test-null-delegate"));
    }

    [Fact]
    public void Register_OverwritesExisting()
    {
        var first = new TestRenderer();
        var second = new TestRenderer();
        try
        {
            CustomIconRegistry.Register("test-overwrite", first);
            CustomIconRegistry.Register("test-overwrite", second);

            Assert.True(CustomIconRegistry.TryGet("test-overwrite", out var retrieved));
            Assert.Same(second, retrieved);
        }
        finally
        {
            CustomIconRegistry.Unregister("test-overwrite");
        }
    }

    [Fact]
    public void Register_CaseInsensitive()
    {
        var renderer = new TestRenderer();
        try
        {
            CustomIconRegistry.Register("TestCase", renderer);
            Assert.True(CustomIconRegistry.Has("testcase"));
            Assert.True(CustomIconRegistry.Has("TESTCASE"));
        }
        finally
        {
            CustomIconRegistry.Unregister("TestCase");
        }
    }

    [Fact]
    public void TryGet_NullKey_ReturnsFalse()
    {
        Assert.False(CustomIconRegistry.TryGet(null!, out _));
    }

    [Fact]
    public void TryGet_EmptyKey_ReturnsFalse()
    {
        Assert.False(CustomIconRegistry.TryGet("", out _));
    }

    [Fact]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        Assert.False(CustomIconRegistry.TryGet("nonexistent-icon", out _));
    }

    [Fact]
    public void Has_NullKey_ReturnsFalse()
    {
        Assert.False(CustomIconRegistry.Has(null!));
    }

    [Fact]
    public void Has_EmptyKey_ReturnsFalse()
    {
        Assert.False(CustomIconRegistry.Has(""));
    }

    [Fact]
    public void Unregister_ExistingKey_ReturnsTrue()
    {
        CustomIconRegistry.Register("test-unregister", new TestRenderer());

        Assert.True(CustomIconRegistry.Unregister("test-unregister"));
        Assert.False(CustomIconRegistry.Has("test-unregister"));
    }

    [Fact]
    public void Unregister_UnknownKey_ReturnsFalse()
    {
        Assert.False(CustomIconRegistry.Unregister("never-registered"));
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        CustomIconRegistry.Register("clear-1", new TestRenderer());
        CustomIconRegistry.Register("clear-2", new TestRenderer());

        CustomIconRegistry.Clear();

        Assert.False(CustomIconRegistry.Has("clear-1"));
        Assert.False(CustomIconRegistry.Has("clear-2"));
    }
}

public class IconKeyAttributeTests
{
    [Fact]
    public void Constructor_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IconKeyAttribute(null!));
    }

    [Fact]
    public void Constructor_PreservesKey()
    {
        var attr = new IconKeyAttribute("mymod:myicon");
        Assert.Equal("mymod:myicon", attr.Key);
    }

    [Fact]
    public void Constructor_WithAliases_PreservesAliases()
    {
        var attr = new IconKeyAttribute("mymod:myicon", "alias1", "alias2");
        Assert.Equal(2, attr.Aliases.Length);
        Assert.Contains("alias1", attr.Aliases);
        Assert.Contains("alias2", attr.Aliases);
    }

    [Fact]
    public void Constructor_WithoutAliases_EmptyArray()
    {
        var attr = new IconKeyAttribute("mymod:myicon");
        Assert.Empty(attr.Aliases);
    }

    [Fact]
    public void Constructor_NullAliases_EmptyArray()
    {
        var attr = new IconKeyAttribute("mymod:myicon", null!);
        Assert.Empty(attr.Aliases);
    }

    [IconKey("test:attr-target")]
    private class AnnotatedClass { }

    [Fact]
    public void Attribute_CanBeRetrievedFromType()
    {
        var attr = typeof(AnnotatedClass)
            .GetCustomAttributes(typeof(IconKeyAttribute), false)
            .Cast<IconKeyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("test:attr-target", attr!.Key);
    }
}

public class ArcanumGuiThemeTests
{
    [Fact]
    public void SurfaceDeepest_HasExpectedValues()
    {
        Assert.Equal(0x1F / 255.0, ArcanumGuiTheme.SurfaceDeepest.R, 5);
        Assert.Equal(0x18 / 255.0, ArcanumGuiTheme.SurfaceDeepest.G, 5);
        Assert.Equal(0x10 / 255.0, ArcanumGuiTheme.SurfaceDeepest.B, 5);
        Assert.Equal(0.96, ArcanumGuiTheme.SurfaceDeepest.A, 5);
    }

    [Fact]
    public void Accent_HasExpectedValues()
    {
        Assert.Equal(0xC5 / 255.0, ArcanumGuiTheme.Accent.R, 5);
        Assert.Equal(0x89 / 255.0, ArcanumGuiTheme.Accent.G, 5);
        Assert.Equal(0x48 / 255.0, ArcanumGuiTheme.Accent.B, 5);
        Assert.Equal(1.0, ArcanumGuiTheme.Accent.A, 5);
    }

    [Fact]
    public void TextPrimary_HasExpectedValues()
    {
        Assert.Equal(0xE9 / 255.0, ArcanumGuiTheme.TextPrimary.R, 5);
        Assert.Equal(0xDD / 255.0, ArcanumGuiTheme.TextPrimary.G, 5);
        Assert.Equal(0xCE / 255.0, ArcanumGuiTheme.TextPrimary.B, 5);
        Assert.Equal(1.0, ArcanumGuiTheme.TextPrimary.A, 5);
    }

    [Fact]
    public void Radius_Small_IsFour()
    {
        Assert.Equal(4.0, ArcanumGuiTheme.Radius.Small);
    }

    [Fact]
    public void Radius_Medium_IsEight()
    {
        Assert.Equal(8.0, ArcanumGuiTheme.Radius.Medium);
    }

    [Fact]
    public void Radius_Large_IsTwelve()
    {
        Assert.Equal(12.0, ArcanumGuiTheme.Radius.Large);
    }

    [Fact]
    public void Radius_Pill_IsTwenty()
    {
        Assert.Equal(20.0, ArcanumGuiTheme.Radius.Pill);
    }

    [Fact]
    public void Spacing_Xs_IsFour()
    {
        Assert.Equal(4.0, ArcanumGuiTheme.Spacing.Xs);
    }

    [Fact]
    public void Spacing_Sm_IsEight()
    {
        Assert.Equal(8.0, ArcanumGuiTheme.Spacing.Sm);
    }

    [Fact]
    public void Spacing_Md_IsTwelve()
    {
        Assert.Equal(12.0, ArcanumGuiTheme.Spacing.Md);
    }

    [Fact]
    public void Spacing_Lg_IsEighteen()
    {
        Assert.Equal(18.0, ArcanumGuiTheme.Spacing.Lg);
    }

    [Fact]
    public void Spacing_Xl_IsTwentyEight()
    {
        Assert.Equal(28.0, ArcanumGuiTheme.Spacing.Xl);
    }

    [Fact]
    public void StatusColors_AreDistinct()
    {
        Assert.NotEqual(ArcanumGuiTheme.StatusAvailable, ArcanumGuiTheme.StatusActive);
        Assert.NotEqual(ArcanumGuiTheme.StatusComplete, ArcanumGuiTheme.StatusFailed);
        Assert.NotEqual(ArcanumGuiTheme.StatusAvailable, ArcanumGuiTheme.StatusLocked);
    }
}

public class HudSnapshotInterfaceTests
{
    private class TestSnapshot : IHudSnapshot
    {
        public bool Removed { get; private set; }
        public bool IsRemoved() => Removed;
        public void MarkRemoved() => Removed = true;
    }

    [Fact]
    public void IsRemoved_DefaultsToFalse()
    {
        var snap = new TestSnapshot();
        Assert.False(snap.IsRemoved());
    }

    [Fact]
    public void MarkRemoved_SetsRemoved()
    {
        var snap = new TestSnapshot();
        snap.MarkRemoved();
        Assert.True(snap.IsRemoved());
    }
}
