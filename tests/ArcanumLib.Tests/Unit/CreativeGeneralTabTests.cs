using ArcanumLib.Inventory;
using Vintagestory.API.Common;

namespace ArcanumLib.Tests.Unit;

public class CreativeGeneralTabTests
{
    private static Item Item(string code, params string[] tabs) => new() { Code = new AssetLocation(code), CreativeInventoryTabs = tabs };

    [Fact]
    public void Mod_Item_In_Own_Tab_Gets_General()
    {
        var item = Item("alegacysteamworks:boiler", "contraptions");
        Assert.True(CreativeGeneralTab.Add(item));
        Assert.Equal(new[] { "contraptions", "general" }, item.CreativeInventoryTabs);
        Assert.False(CreativeGeneralTab.Add(item));
    }

    [Fact]
    public void Hidden_Game_And_Already_General_Stay()
    {
        var hidden = Item("alegacysteamworks:hidden");
        var game = Item("game:blockcopy", "items");
        var both = Item("albase:key", "general", "alegacy-misc");
        Assert.False(CreativeGeneralTab.Add(hidden));
        Assert.False(CreativeGeneralTab.Add(game));
        Assert.False(CreativeGeneralTab.Add(both));
        Assert.Empty(hidden.CreativeInventoryTabs);
        Assert.Equal(new[] { "items" }, game.CreativeInventoryTabs);
        Assert.Equal(new[] { "general", "alegacy-misc" }, both.CreativeInventoryTabs);
    }

    [Fact]
    public void Shared_Stack_Lists_Are_Replaced_Not_Changed()
    {
        var shared = new CreativeTabAndStackList { Tabs = new[] { "alegacycuriosities" }, Stacks = new JsonItemStack[0] };
        var a = new Item { Code = new AssetLocation("alegacycuriosities:rug-red"), CreativeInventoryTabs = new string[0], CreativeInventoryStacks = new[] { shared } };
        var b = new Item { Code = new AssetLocation("alegacycuriosities:rug-blue"), CreativeInventoryTabs = new string[0], CreativeInventoryStacks = new[] { shared } };

        Assert.True(CreativeGeneralTab.Add(a));
        Assert.True(CreativeGeneralTab.Add(b));

        Assert.Equal(new[] { "alegacycuriosities" }, shared.Tabs);
        Assert.Equal(new[] { "alegacycuriosities", "general" }, a.CreativeInventoryStacks[0].Tabs);
        Assert.Equal(new[] { "alegacycuriosities", "general" }, b.CreativeInventoryStacks[0].Tabs);
        Assert.Same(shared.Stacks, a.CreativeInventoryStacks[0].Stacks);
        Assert.Empty(a.CreativeInventoryTabs);
    }
}
