using System;
using ArcanumLib.Gui.RadialMenu;
using NSubstitute;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class RadialMenuItemTests
{
    [Fact]
    public void Defaults_AreSane()
    {
        var item = new RadialMenuItem();

        Assert.Equal(string.Empty, item.Label);
        Assert.Equal(string.Empty, item.Description);
        Assert.Equal(string.Empty, item.Icon);
        Assert.Null(item.CustomIconDraw);
        Assert.Null(item.Action);
        Assert.True(item.CloseAfterClick);
        Assert.False(item.IsActive);
        Assert.False(item.Disabled);
        Assert.NotNull(item.SubItems);
        Assert.Empty(item.SubItems);
    }

    [Fact]
    public void Action_FiresWhenInvoked()
    {
        var fired = false;
        var item = new RadialMenuItem { Action = () => fired = true };

        item.Action!.Invoke();

        Assert.True(fired);
    }

    [Fact]
    public void SubItems_CanBeAdded()
    {
        var item = new RadialMenuItem();

        item.SubItems.Add(new RadialMenuItem { Label = "child" });

        Assert.Single(item.SubItems);
        Assert.Equal("child", item.SubItems[0].Label);
    }
}

public class RadialMenuStyleRegistryTests
{
    private class TestStyle : IRadialMenuStyle
    {
        public string Key => "test-style";
        public void DrawSector(Cairo.Context ctx, float cx, float cy, float a0, float a1,
            bool hovered, bool isActive, bool disabled,
            float outerRadius, float innerRadius) { }
        public void DrawCenterButton(Cairo.Context ctx, float cx, float cy, float innerRadius) { }
        public (float r, float g, float b, float a) GetIconColor(bool disabled) => (1, 1, 1, 1);
    }

    private class AnotherStyle : IRadialMenuStyle
    {
        public string Key => "another";
        public void DrawSector(Cairo.Context ctx, float cx, float cy, float a0, float a1,
            bool hovered, bool isActive, bool disabled,
            float outerRadius, float innerRadius) { }
        public void DrawCenterButton(Cairo.Context ctx, float cx, float cy, float innerRadius) { }
        public (float r, float g, float b, float a) GetIconColor(bool disabled) => (0, 0, 0, 1);
    }

    [Fact]
    public void GetOrDefault_UnknownKey_ReturnsDefaultStyle()
    {
        var style = RadialMenuStyleRegistry.GetOrDefault("nonexistent");

        Assert.Equal("default", style.Key);
    }

    [Fact]
    public void GetOrDefault_NullKey_ReturnsDefaultStyle()
    {
        var style = RadialMenuStyleRegistry.GetOrDefault(null);

        Assert.Equal("default", style.Key);
    }

    [Fact]
    public void GetOrDefault_EmptyKey_ReturnsDefaultStyle()
    {
        var style = RadialMenuStyleRegistry.GetOrDefault("");

        Assert.Equal("default", style.Key);
    }

    [Fact]
    public void Register_AndRetrieve_RoundTrip()
    {
        var test = new TestStyle();
        try
        {
            RadialMenuStyleRegistry.Register(test);

            Assert.True(RadialMenuStyleRegistry.IsRegistered("test-style"));
            var retrieved = RadialMenuStyleRegistry.GetOrDefault("test-style");
            Assert.Same(test, retrieved);
        }
        finally
        {
            RadialMenuStyleRegistry.Unregister("test-style");
        }
    }

    [Fact]
    public void Register_NullStyle_DoesNothing()
    {
        RadialMenuStyleRegistry.Register(null!);

        Assert.False(RadialMenuStyleRegistry.IsRegistered("anything"));
    }

    [Fact]
    public void Register_OverwritesExistingStyle()
    {
        var first = new TestStyle();
        var second = new TestStyle();
        try
        {
            RadialMenuStyleRegistry.Register(first);
            RadialMenuStyleRegistry.Register(second);

            var retrieved = RadialMenuStyleRegistry.GetOrDefault("test-style");
            Assert.Same(second, retrieved);
        }
        finally
        {
            RadialMenuStyleRegistry.Unregister("test-style");
        }
    }

    [Fact]
    public void Unregister_RemovesStyle()
    {
        var test = new TestStyle();
        RadialMenuStyleRegistry.Register(test);

        var removed = RadialMenuStyleRegistry.Unregister("test-style");

        Assert.True(removed);
        Assert.False(RadialMenuStyleRegistry.IsRegistered("test-style"));
    }

    [Fact]
    public void Unregister_DefaultStyle_ReturnsFalse()
    {
        var removed = RadialMenuStyleRegistry.Unregister("default");

        Assert.False(removed);
    }

    [Fact]
    public void Unregister_EmptyKey_ReturnsFalse()
    {
        var removed = RadialMenuStyleRegistry.Unregister("");

        Assert.False(removed);
    }

    [Fact]
    public void Unregister_NullKey_ReturnsFalse()
    {
        var removed = RadialMenuStyleRegistry.Unregister(null!);

        Assert.False(removed);
    }

    [Fact]
    public void Unregister_UnknownKey_ReturnsFalse()
    {
        var removed = RadialMenuStyleRegistry.Unregister("never-registered");

        Assert.False(removed);
    }

    [Fact]
    public void IsRegistered_NullKey_ReturnsFalse()
    {
        Assert.False(RadialMenuStyleRegistry.IsRegistered(null!));
    }

    [Fact]
    public void IsRegistered_EmptyKey_ReturnsFalse()
    {
        Assert.False(RadialMenuStyleRegistry.IsRegistered(""));
    }

    [Fact]
    public void IsRegistered_CaseInsensitive()
    {
        var test = new TestStyle();
        try
        {
            RadialMenuStyleRegistry.Register(test);

            Assert.True(RadialMenuStyleRegistry.IsRegistered("TEST-STYLE"));
            Assert.True(RadialMenuStyleRegistry.IsRegistered("test-style"));
        }
        finally
        {
            RadialMenuStyleRegistry.Unregister("test-style");
        }
    }
}

public class DefaultRadialMenuStyleTests
{
    [Fact]
    public void Key_IsDefault()
    {
        var style = new DefaultRadialMenuStyle();

        Assert.Equal("default", style.Key);
    }

    [Fact]
    public void GetIconColor_Disabled_ReturnsDimmedColor()
    {
        var style = new DefaultRadialMenuStyle();

        var (r, g, b, a) = style.GetIconColor(true);

        Assert.Equal(0.35f, r);
        Assert.Equal(0.35f, g);
        Assert.Equal(0.35f, b);
        Assert.Equal(0.50f, a);
    }

    [Fact]
    public void GetIconColor_Enabled_ReturnsBrightColor()
    {
        var style = new DefaultRadialMenuStyle();

        var (r, g, b, a) = style.GetIconColor(false);

        Assert.Equal(0.95f, r);
        Assert.Equal(0.92f, g);
        Assert.Equal(0.88f, b);
        Assert.Equal(1.0f, a);
    }
}
