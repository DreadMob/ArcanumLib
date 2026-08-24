using System;
using System.Collections.Generic;
using ArcanumLib.Commands;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class CommandArgsTests
{
    [Fact]
    public void String_ReturnsValue_WhenPresent()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Alice"
        });

        Assert.Equal("Alice", args.String("name"));
    }

    [Fact]
    public void String_ReturnsEmpty_WhenNotString()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["count"] = 42
        });

        Assert.Equal("", args.String("count"));
    }

    [Fact]
    public void Int_ReturnsValue_WhenPresent()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["count"] = 7
        });

        Assert.Equal(7, args.Int("count"));
    }

    [Fact]
    public void Float_ReturnsValue_WhenPresent()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["factor"] = 1.5f
        });

        Assert.Equal(1.5f, args.Float("factor"), 3);
    }

    [Fact]
    public void Bool_ReturnsTrue_WhenTrue()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["enabled"] = true
        });

        Assert.True(args.Bool("enabled"));
    }

    [Fact]
    public void Bool_ReturnsFalse_WhenFalse()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["enabled"] = false
        });

        Assert.False(args.Bool("enabled"));
    }

    [Fact]
    public void StringOr_ReturnsFallback_WhenMissing()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("default", args.StringOr("missing", "default"));
    }

    [Fact]
    public void IntOr_ReturnsFallback_WhenMissing()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(10, args.IntOr("missing", 10));
    }

    [Fact]
    public void Has_ReturnsTrue_WhenArgumentProvided()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Alice"
        });

        Assert.True(args.Has("name"));
        Assert.False(args.Has("missing"));
    }

    [Fact]
    public void Lookup_IsCaseInsensitive()
    {
        var args = new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PlayerName"] = "Alice"
        });

        Assert.Equal("Alice", args.String("playername"));
    }
}
