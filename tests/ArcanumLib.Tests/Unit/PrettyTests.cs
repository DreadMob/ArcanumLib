using ArcanumLib.Text;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PrettyTests
{
    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", Pretty.Sanitize(null));
        Assert.Equal("", Pretty.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_CollapsesNewlinesAndSpaces()
    {
        Assert.Equal("hello world", Pretty.Sanitize("hello\n\r\n\nworld"));
        Assert.Equal("a b", Pretty.Sanitize("a   <br> \n b"));
    }

    [Fact]
    public void Sanitize_LiteralBackslashN_TreatedAsSpace()
    {
        Assert.Equal("line one line two", Pretty.Sanitize("line one \\nline two"));
    }

    [Fact]
    public void Readable_ConvertsDashesAndUnderscores()
    {
        Assert.Equal("Metalbit Uranium", Pretty.Readable("metalbit-uranium"));
        Assert.Equal("Hollow Trials", Pretty.Readable("hollow_trials"));
        Assert.Equal("Game Long Dark", Pretty.Readable("game:long-dark"));
    }

    [Fact]
    public void Readable_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal("", Pretty.Readable(null));
        Assert.Equal("", Pretty.Readable("   "));
    }

    [Fact]
    public void LastSegment_ReturnsLastPrettySegment()
    {
        Assert.Equal("Bear", Pretty.LastSegment("game:creature:bear"));
        Assert.Equal("Iron", Pretty.LastSegment("iron"));
    }

    [Fact]
    public void TargetCode_StripsDomainAndWildcards()
    {
        Assert.Equal("Flower", Pretty.TargetCode("game:flower-*"));
        Assert.Equal("Metalbit", Pretty.TargetCode("game:metalbit-*"));
    }

    [Fact]
    public void TargetCode_CollapsesRunsOfDashes()
    {
        Assert.Equal("Iron Ore", Pretty.TargetCode("game:iron--*-ore"));
    }
}
