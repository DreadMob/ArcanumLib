using ArcanumLib.Text;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class WildcardTests
{
    [Theory]
    [InlineData("game:ingot-copper", "game:ingot-*", true)]
    [InlineData("game:ingot-copper", "game:*", true)]
    [InlineData("game:ingot-copper", "game:ingot-cop?er", true)]
    [InlineData("game:ingot-copper", "game:ingot-iron", false)]
    [InlineData("game:ingot-copper", "game:plate-*", false)]
    [InlineData("game:ingot-copper", "game:ingot-cop??er", false)]
    [InlineData("game:axe-iron", "game:axe-*-iron", false)] // mid-wildcard not supported by regex? let's verify
    [InlineData("game:axe-iron", "game:axe-*", true)]
    public void Match_Wildcards(string input, string pattern, bool expected)
    {
        Assert.Equal(expected, Wildcard.Match(input, pattern));
    }

    [Fact]
    public void Match_NullInputOrPattern_ReturnsFalse()
    {
        Assert.False(Wildcard.Match(null!, "game:*"));
        Assert.False(Wildcard.Match("game:ingot", null!));
    }

    [Theory]
    [InlineData("game:ingot-*", true)]
    [InlineData("game:ingot-*-iron", false)]
    [InlineData("game:ingot", false)]
    [InlineData("*", true)]
    public void IsSimplePrefix_DetectsTrailingStar(string pattern, bool expected)
    {
        Assert.Equal(expected, Wildcard.IsSimplePrefix(pattern));
    }
}
