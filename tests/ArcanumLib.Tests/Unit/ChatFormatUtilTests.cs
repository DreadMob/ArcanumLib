using ArcanumLib.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ChatFormatUtilTests
{
    [Fact]
    public void Font_WrapsTextWithColor()
    {
        Assert.Equal("<font color=\"#ff0000\">hello</font>", ChatFormatUtil.Font("hello", "#ff0000"));
    }

    [Fact]
    public void Font_EmptyColor_ReturnsOriginal()
    {
        Assert.Equal("hello", ChatFormatUtil.Font("hello", ""));
    }

    [Fact]
    public void Font_EmptyText_ReturnsEmpty()
    {
        Assert.Equal("", ChatFormatUtil.Font("   ", "#ff0000"));
    }

    [Fact]
    public void PrefixAlert_Default_ReturnsRedPrefixAndWhiteText()
    {
        var result = ChatFormatUtil.PrefixAlert("danger");

        Assert.Equal("<font color=\"#ff5555\">[!] </font><font color=\"#ffffff\">danger</font>", result);
    }

    [Fact]
    public void PrefixAlert_CustomColors()
    {
        var result = ChatFormatUtil.PrefixAlert("warn", "#ffff00", "#000000");

        Assert.Equal("<font color=\"#ffff00\">[!] </font><font color=\"#000000\">warn</font>", result);
    }

    [Fact]
    public void PrefixAlert_CustomPrefixAndColors()
    {
        var result = ChatFormatUtil.PrefixAlert("info", ">> ", "#00ff00", "#ffffff");

        Assert.Equal("<font color=\"#00ff00\">>> </font><font color=\"#ffffff\">info</font>", result);
    }
}
