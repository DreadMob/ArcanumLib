using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Data;
using ArcanumLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class TagMatcherTests
{
    [Theory]
    [InlineData("game:ingot-copper", "game:ingot-copper", true)]
    [InlineData("game:ingot-copper", "game:ingot-iron", false)]
    [InlineData("game:ingot-copper", "game:ingot-*", true)]
    [InlineData("game:ingot-copper", "game:*", true)]
    [InlineData("game:ingot-copper", "game:plate-*", false)]
    public void MatchesPattern_PrefixAndExact(string code, string pattern, bool expected)
    {
        Assert.Equal(expected, CollectibleNameResolver.MatchesPattern(code, pattern));
    }

    [Fact]
    public void MatchesPattern_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(CollectibleNameResolver.MatchesPattern(null!, "game:*"));
        Assert.False(CollectibleNameResolver.MatchesPattern("game:ingot", null!));
        Assert.False(CollectibleNameResolver.MatchesPattern("", "game:*"));
        Assert.False(CollectibleNameResolver.MatchesPattern("game:ingot", ""));
    }

    [Fact]
    public void TagMatcher_Matches_NullCollectible_ReturnsFalse()
    {
        var matcher = new TagMatcher().AddCodePattern("game:ingot-*");

        Assert.False(matcher.Matches((CollectibleObject?)null));
        Assert.False(matcher.Matches((ItemStack?)null));
    }

    [Fact]
    public void TagMatcher_Filter_Null_ReturnsEmpty()
    {
        var matcher = new TagMatcher();

        Assert.Empty(matcher.Filter(null!));
        Assert.Empty(matcher.FilterStacks(null!));
    }

    [Fact]
    public void TagMatcher_Fluid_Builder_ReturnsSameMatcher()
    {
        var matcher = new TagMatcher()
            .AddCodePattern("game:ingot-*")
            .SetTagMode(TagMatcher.MatchMode.All);

        Assert.NotNull(matcher);
    }

    [Fact]
    public void TagMatcher_CodePattern_MatchesWildcards()
    {
        var collectible = new DummyCollectible { Code = new AssetLocation("game:ingot-copper") };

        var matcher = new TagMatcher().AddCodePattern("game:ingot-*");

        Assert.True(matcher.Matches(collectible));
    }

    [Fact]
    public void TagMatcher_CodePattern_NonMatching_ReturnsFalse()
    {
        var collectible = new DummyCollectible { Code = new AssetLocation("game:plate-iron") };

        var matcher = new TagMatcher().AddCodePattern("game:ingot-*");

        Assert.False(matcher.Matches(collectible));
    }

    [Fact]
    public void TagMatcher_Filter_ExcludesNonMatching()
    {
        var items = new CollectibleObject[]
        {
            new DummyCollectible { Code = new AssetLocation("game:ingot-copper") },
            new DummyCollectible { Code = new AssetLocation("game:plate-iron") }
        };

        var matcher = new TagMatcher().AddCodePattern("game:ingot-*");

        var result = matcher.Filter(items).ToList();

        Assert.Single(result);
        Assert.Equal("game:ingot-copper", result[0].Code.ToString());
    }

    private class DummyCollectible : CollectibleObject
    {
        public override EnumItemClass ItemClass => EnumItemClass.Item;
        public override int Id => 0;

        public new AssetLocation? Code
        {
            get => base.Code;
            set => base.Code = value;
        }
    }
}
