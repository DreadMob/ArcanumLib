using System;
using ArcanumLib.Data;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class WatchedAttributesExtensionsTests
{
    [Fact]
    public void GetOrCreateTreeAttribute_CreatesNestedTree()
    {
        var tree = new TreeAttribute();

        var nested = tree.GetOrCreateTreeAttribute("nested");

        Assert.NotNull(nested);
        Assert.True(tree.HasAttribute("nested"));
    }

    [Fact]
    public void GetOrCreateTreeAttribute_Throws_OnNullTree()
    {
        Assert.Throws<ArgumentNullException>(() =>
            (null as ITreeAttribute)!.GetOrCreateTreeAttribute("key"));
    }

    [Fact]
    public void GetOrCreateTreeAttribute_Throws_OnEmptyKey()
    {
        var tree = new TreeAttribute();
        Assert.Throws<ArgumentException>(() => tree.GetOrCreateTreeAttribute(""));
    }

    [Fact]
    public void GetOrCreateInt_WritesDefault_WhenMissing()
    {
        var tree = new TreeAttribute();

        Assert.Equal(42, tree.GetOrCreateInt("answer", 42));
        Assert.Equal(42, tree.GetInt("answer"));
    }

    [Fact]
    public void GetOrCreateInt_ReturnsExisting_WhenPresent()
    {
        var tree = new TreeAttribute();
        tree.SetInt("answer", 7);

        Assert.Equal(7, tree.GetOrCreateInt("answer", 42));
    }

    [Fact]
    public void GetOrCreateFloat_WritesDefault_WhenMissing()
    {
        var tree = new TreeAttribute();

        Assert.Equal(3.14f, tree.GetOrCreateFloat("pi", 3.14f), 5);
        Assert.Equal(3.14f, tree.GetFloat("pi"), 5);
    }

    [Fact]
    public void GetOrCreateBool_WritesDefault_WhenMissing()
    {
        var tree = new TreeAttribute();

        Assert.True(tree.GetOrCreateBool("flag", true));
        Assert.True(tree.GetBool("flag"));
    }

    [Fact]
    public void GetOrCreateString_WritesDefault_WhenMissing()
    {
        var tree = new TreeAttribute();

        Assert.Equal("hello", tree.GetOrCreateString("msg", "hello"));
        Assert.Equal("hello", tree.GetString("msg"));
    }

    [Fact]
    public void GetOrCreateLong_WritesDefault_WhenMissing()
    {
        var tree = new TreeAttribute();

        Assert.Equal(123456789L, tree.GetOrCreateLong("ticks", 123456789L));
        Assert.Equal(123456789L, tree.GetLong("ticks"));
    }

    [Fact]
    public void GetOrCreateDouble_WritesDefault_WhenMissing()
    {
        var tree = new TreeAttribute();

        Assert.Equal(2.718, tree.GetOrCreateDouble("e", 2.718), 5);
        Assert.Equal(2.718, tree.GetDouble("e"), 5);
    }

    [Fact]
    public void SetIntIfMissing_DoesNotOverwriteExisting()
    {
        var tree = new TreeAttribute();
        tree.SetInt("value", 10);

        tree.SetIntIfMissing("value", 20);

        Assert.Equal(10, tree.GetInt("value"));
    }

    [Fact]
    public void SetIntIfMissing_Sets_WhenMissing()
    {
        var tree = new TreeAttribute();

        tree.SetIntIfMissing("value", 20);

        Assert.Equal(20, tree.GetInt("value"));
    }

    [Fact]
    public void SetAndMarkDirty_DoesNothing_WhenEntityNull()
    {
        // Just ensures no exception.
        (null as Vintagestory.API.Common.Entities.Entity)!.SetAndMarkDirty("path", true);
    }
}
