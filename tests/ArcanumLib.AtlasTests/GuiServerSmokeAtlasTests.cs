using System.Threading.Tasks;
using ArcanumLib.Gui.Hud;
using ArcanumLib.Gui.RadialMenu;
using Xunit;

namespace ArcanumLib.AtlasTests;

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class GuiServerSmokeAtlasTests : AtlasScenarioBase
{
    [AtlasScenario]
    public async Task HudThemeResolver_Resolve_Default_ReturnsBaseTheme()
    {
        await World.Ticks(5);

        var baseTheme = HudTheme.CreateDefault();
        var result = HudThemeResolver.Resolve("", null, null, baseTheme);

        Assert.Same(baseTheme, result);
    }

    [AtlasScenario]
    public async Task HudThemeResolver_Resolve_Unknown_ReturnsBaseTheme()
    {
        await World.Ticks(5);

        var baseTheme = HudTheme.CreateDefault();
        var result = HudThemeResolver.Resolve("nonexistent-theme", null, null, baseTheme);

        Assert.Same(baseTheme, result);
    }

    [AtlasScenario]
    public async Task HudTheme_CreateDefault_HasExpectedValues()
    {
        await World.Ticks(5);

        var theme = HudTheme.CreateDefault();

        Assert.Equal("cartouche", theme.frame);
        Assert.Equal("ankh", theme.frameSymbol);
        Assert.NotNull(theme.colors);
        Assert.NotNull(theme.fonts);
        Assert.NotNull(theme.layout);
    }

    [AtlasScenario]
    public async Task HudTheme_Merge_OverridesNonNullFields()
    {
        await World.Ticks(5);

        var baseTheme = HudTheme.CreateDefault();
        var overlay = new HudTheme
        {
            frame = "bone",
            frameSymbol = "crossbone",
            colors = baseTheme.colors,
            fonts = baseTheme.fonts,
            layout = baseTheme.layout
        };

        var merged = baseTheme.Merge(overlay);

        Assert.Equal("bone", merged.frame);
        Assert.Equal("crossbone", merged.frameSymbol);
    }

    [AtlasScenario]
    public async Task RadialMenuStyleRegistry_Default_AlwaysAvailable()
    {
        await World.Ticks(5);

        var style = RadialMenuStyleRegistry.GetOrDefault("nonexistent");

        Assert.Equal("default", style.Key);
    }

    [AtlasScenario]
    public async Task RadialMenuStyleRegistry_RegisterAndRetrieve_RoundTrip()
    {
        await World.Ticks(5);

        var testStyle = new TestAtlasStyle();
        try
        {
            RadialMenuStyleRegistry.Register(testStyle);

            Assert.True(RadialMenuStyleRegistry.IsRegistered("atlas-test-style"));
            var retrieved = RadialMenuStyleRegistry.GetOrDefault("atlas-test-style");
            Assert.Same(testStyle, retrieved);
        }
        finally
        {
            RadialMenuStyleRegistry.Unregister("atlas-test-style");
        }
    }

    [AtlasScenario]
    public async Task RadialMenuStyleRegistry_Unregister_Default_ReturnsFalse()
    {
        await World.Ticks(5);

        Assert.False(RadialMenuStyleRegistry.Unregister("default"));
    }

    [AtlasScenario]
    public async Task HudTextResolver_Resolve_PlainText_ReturnsAsIs()
    {
        await World.Ticks(5);

        var result = HudTextResolver.Resolve("Hello World");

        Assert.Equal("Hello World", result);
    }

    [AtlasScenario]
    public async Task HudTextResolver_Resolve_Empty_ReturnsEmpty()
    {
        await World.Ticks(5);

        var result = HudTextResolver.Resolve("");

        Assert.Equal("", result);
    }

    [AtlasScenario]
    public async Task HudTextResolver_Resolve_Null_ReturnsEmpty()
    {
        await World.Ticks(5);

        var result = HudTextResolver.Resolve(null!);

        Assert.Equal("", result);
    }

    private class TestAtlasStyle : IRadialMenuStyle
    {
        public string Key => "atlas-test-style";
        public void DrawSector(Cairo.Context ctx, float cx, float cy, float a0, float a1,
            bool hovered, bool isActive, bool disabled,
            float outerRadius, float innerRadius) { }
        public void DrawCenterButton(Cairo.Context ctx, float cx, float cy, float innerRadius) { }
        public (float r, float g, float b, float a) GetIconColor(bool disabled) => (1, 1, 1, 1);
    }
}
