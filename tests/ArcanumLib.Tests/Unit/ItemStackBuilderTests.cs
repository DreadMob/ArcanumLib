using ArcanumLib.Inventory;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ItemStackBuilderTests
{
    [Fact]
    public void Build_NullApi_ReturnsNull()
    {
        var builder = new ItemStackBuilder().Code("game:stick");

        Assert.Null(builder.Build(null!));
    }
    [Fact]
    public void Build_NoCode_ReturnsNull()
    {
        var api = CreateApi(out _);

        Assert.Null(new ItemStackBuilder().Build(api));
    }

    [Fact]
    public void Build_UnknownCode_ReturnsNull()
    {
        var api = CreateApi(out var world);
        world.GetItem(Arg.Any<AssetLocation>()).Returns((Item)null!);
        world.GetBlock(Arg.Any<AssetLocation>()).Returns((Block)null!);

        var builder = new ItemStackBuilder().Code("game:does-not-exist");

        Assert.Null(builder.Build(api));
    }

    [Fact]
    public void BuildOrThrow_UnknownCode_Throws()
    {
        var api = CreateApi(out var world);
        world.GetItem(Arg.Any<AssetLocation>()).Returns((Item)null!);
        world.GetBlock(Arg.Any<AssetLocation>()).Returns((Block)null!);

        var builder = new ItemStackBuilder().Code("game:missing");

        Assert.Throws<System.InvalidOperationException>(() => builder.BuildOrThrow(api));
    }

    [Fact]
    public void Build_WithItemCode_ReturnsStackWithAttributes()
    {
        var api = CreateApi(out var world);
        var item = new DummyItem("game:gem");
        world.GetItem(new AssetLocation("game:gem")).Returns(item);
        world.GetBlock(Arg.Any<AssetLocation>()).Returns((Block)null!);

        var stack = new ItemStackBuilder()
            .Code("game:gem")
            .Count(7)
            .Durability(120)
            .Attribute("rarity", "epic")
            .Attribute("charges", 3)
            .WatchedAttribute("owner", "player1")
            .Build(api);

        Assert.NotNull(stack);
        Assert.Equal(7, stack!.StackSize);
        Assert.Equal("epic", stack.Attributes.GetString("rarity"));
        Assert.Equal(3, stack.Attributes.GetInt("charges"));
        Assert.Equal(120, stack.Attributes.GetInt("durability"));
        Assert.Equal("player1", stack.Attributes.GetString("owner"));
    }

    [Fact]
    public void Build_WithBlockClass_LooksUpBlock()
    {
        var api = CreateApi(out var world);
        var block = new DummyBlock("game:stone");
        world.GetBlock(new AssetLocation("game:stone")).Returns(block);
        world.GetItem(Arg.Any<AssetLocation>()).Returns((Item)null!);

        var stack = new ItemStackBuilder()
            .Code("game:stone")
            .ItemClass(EnumItemClass.Block)
            .Count(2)
            .Build(api);

        Assert.NotNull(stack);
        Assert.Equal(2, stack!.StackSize);
        Assert.Same(block, stack.Collectible);
    }

    [Fact]
    public void Count_ClampsToOne()
    {
        var api = CreateApi(out var world);
        var item = new DummyItem("game:stick");
        world.GetItem(new AssetLocation("game:stick")).Returns(item);
        world.GetBlock(Arg.Any<AssetLocation>()).Returns((Block)null!);

        var stack = new ItemStackBuilder()
            .Code("game:stick")
            .Count(-5)
            .Build(api);

        Assert.Equal(1, stack!.StackSize);
    }

    [Fact]
    public void Clear_ResetsBuilder()
    {
        var builder = new ItemStackBuilder()
            .Code("game:stick")
            .Count(10)
            .Durability(50)
            .Attribute("k", "v");

        builder.Clear();

        var api = CreateApi(out _);
        Assert.Null(builder.Build(api));
    }

    [Fact]
    public void Constructor_FromStack_CopiesValues()
    {
        var api = CreateApi(out var world);
        var item = new DummyItem("game:coin");
        world.GetItem(new AssetLocation("game:coin")).Returns(item);
        world.GetBlock(Arg.Any<AssetLocation>()).Returns((Block)null!);

        var original = new ItemStackBuilder()
            .Code("game:coin")
            .Count(4)
            .Attribute("mint", "gold")
            .Build(api)!;

        var copy = new ItemStackBuilder(original)
            .Count(99)
            .Build(api);

        Assert.NotNull(copy);
        Assert.Equal(99, copy!.StackSize);
        Assert.Equal("gold", copy.Attributes.GetString("mint"));
    }

    private static ICoreAPI CreateApi(out IWorldAccessor world)
    {
        world = Substitute.For<IWorldAccessor>();

        var api = Substitute.For<ICoreAPI>();
        api.World.Returns(world);
        return api;
    }

    private class DummyItem : Item
    {
        public DummyItem(string code) { Code = new AssetLocation(code); }
    }

    private class DummyBlock : Block
    {
        public DummyBlock(string code) { Code = new AssetLocation(code); }
    }
}
