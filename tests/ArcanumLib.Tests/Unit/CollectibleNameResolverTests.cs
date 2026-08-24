using System.Collections.Generic;
using ArcanumLib.Helpers;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class CollectibleNameResolverTests
{
    public CollectibleNameResolverTests()
    {
        CollectibleNameResolver.Clear();
    }

    [Theory]
    [InlineData("game:item", "game:item-*", false)]
    [InlineData("game:item-sword", "game:item-*", true)]
    [InlineData("game:block", "game:item-*", false)]
    [InlineData("game:item", "game:item", true)]
    public void MatchesPattern_VariousPatterns_ReturnsExpected(string code, string pattern, bool expected)
    {
        Assert.Equal(expected, CollectibleNameResolver.MatchesPattern(code, pattern));
    }

    [Fact]
    public void IsValidDisplayName_RejectsCodeAndPath()
    {
        var item = new DummyItem("game:iron-ore");

        Assert.False(CollectibleNameResolver.IsValidDisplayName(item, "game:iron-ore"));
        Assert.False(CollectibleNameResolver.IsValidDisplayName(item, "iron-ore"));
        Assert.False(CollectibleNameResolver.IsValidDisplayName(item, "Iron Ore"));
        Assert.True(CollectibleNameResolver.IsValidDisplayName(item, "Some Custom Name"));
    }

    [Fact]
    public void ResolveIconCode_ConcreteCode_Existing_ReturnsCode()
    {
        var sapi = CreateApi();
        var item = new DummyItem("game:iron-ore");
        sapi.World.GetItem(Arg.Any<AssetLocation>()).Returns(item);

        string? icon = CollectibleNameResolver.ResolveIconCode(sapi, "game:iron-ore");

        Assert.Equal("game:iron-ore", icon);
    }

    [Fact]
    public void ResolveIconCode_Wildcard_FindsFirstMatch()
    {
        var sapi = CreateApi();
        var item = new DummyItem("game:iron-ore");
        sapi.World.Items.Returns(new List<Item> { item });
        sapi.World.Blocks.Returns(new List<Block>());

        string? icon = CollectibleNameResolver.ResolveIconCode(sapi, "game:iron-*");

        Assert.Equal("game:iron-ore", icon);
    }

    [Fact]
    public void ResolveIconCode_Unknown_ReturnsNull()
    {
        var sapi = CreateApi();
        sapi.World.GetItem(Arg.Any<AssetLocation>()).Returns((Item?)null);
        sapi.World.GetBlock(Arg.Any<AssetLocation>()).Returns((Block?)null);

        string? icon = CollectibleNameResolver.ResolveIconCode(sapi, "game:missing");

        Assert.Null(icon);
    }

    private static ICoreAPI CreateApi()
    {
        var sapi = Substitute.For<ICoreAPI>();
        var world = Substitute.For<IWorldAccessor>();
        sapi.World.Returns(world);
        return sapi;
    }

    private class DummyItem : Item
    {
        public DummyItem(string code)
        {
            Code = new AssetLocation(code);
        }
    }
}
