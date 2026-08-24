using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Inventory;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class InventoryExtensionsTests
{
    [Fact]
    public void TryGiveOrDrop_Success_ReturnsTrue()
    {
        var player = Substitute.For<IPlayer>();
        var stack = new ItemStack(new DummyItem("game:gem"));
        player.InventoryManager.TryGiveItemstack(stack).Returns(true);

        Assert.True(player.TryGiveOrDrop(stack, null));
    }

    [Fact]
    public void TryGiveOrDrop_FailureWithDropPosition_SpawnsAndReturnsFalse()
    {
        var player = Substitute.For<IPlayer>();
        player.InventoryManager.TryGiveItemstack(Arg.Any<ItemStack>()).Returns(false);

        var world = Substitute.For<IWorldAccessor>();
        var stack = new ItemStack(new DummyItem("game:rock"));
        var drop = new Vec3d(1, 2, 3);

        Assert.False(player.TryGiveOrDrop(stack, world, drop));
        world.Received(1).SpawnItemEntity(stack, drop);
    }

    [Fact]
    public void TryGiveOrDrop_NullPlayer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IPlayer)null!).TryGiveOrDrop(new ItemStack(new DummyItem("game:x")), null));
    }

    [Fact]
    public void TryGiveOrDrop_NullStack_Throws()
    {
        var player = Substitute.For<IPlayer>();
        Assert.Throws<ArgumentNullException>(() => player.TryGiveOrDrop(null!, null));
    }

    [Fact]
    public void TryGiveOrDrop_ServerPlayer_Success()
    {
        var player = Substitute.For<IServerPlayer>();
        var stack = new ItemStack(new DummyItem("game:coin"));
        player.InventoryManager.TryGiveItemstack(stack).Returns(true);

        Assert.True(player.TryGiveOrDrop(stack));
    }

    [Fact]
    public void CountItems_SumsMatchingSlots()
    {
        var slot1 = CreateSlot("game:apple", 3);
        var slot2 = CreateSlot("game:stick", 5);

        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot> { slot1, slot2 }.GetEnumerator());

        int count = inventory.CountItems(s => s?.Itemstack?.Collectible?.Code?.ToString() == "game:apple");

        Assert.Equal(3, count);
    }

    [Fact]
    public void CountItem_ByAssetLocation_Sums()
    {
        var slot1 = CreateSlot("game:apple", 4);
        var slot2 = CreateSlot("game:stick", 2);

        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot> { slot1, slot2 }.GetEnumerator());

        Assert.Equal(4, inventory.CountItem(new AssetLocation("game:apple")));
    }

    [Fact]
    public void FindFirst_ReturnsFirstMatch()
    {
        var slot1 = CreateSlot("game:stone", 1);
        var slot2 = CreateSlot("game:pebble", 1);

        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot> { slot1, slot2 }.GetEnumerator());

        var found = inventory.FindFirst(s => s?.Itemstack?.Collectible?.Code?.ToString() == "game:pebble");

        Assert.Same(slot2, found);
    }

    [Fact]
    public void FindFirst_NoMatch_ReturnsNull()
    {
        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot>().GetEnumerator());

        Assert.Null(inventory.FindFirst(_ => true));
    }

    [Fact]
    public void ConsumeItems_RemovesUpToQuantity()
    {
        var slot1 = CreateSlot("game:wood", 5);
        var slot2 = CreateSlot("game:wood", 4);

        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot> { slot1, slot2 }.GetEnumerator());

        int removed = inventory.ConsumeItems("game:wood", 7);

        Assert.Equal(7, removed);
        Assert.Null(slot1.Itemstack);
        Assert.Equal(2, slot2.Itemstack?.StackSize);
    }

    [Fact]
    public void ConsumeItems_NotEnough_RemovesAllAvailable()
    {
        var slot1 = CreateSlot("game:wood", 3);

        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot> { slot1 }.GetEnumerator());

        int removed = inventory.ConsumeItems("game:wood", 10);

        Assert.Equal(3, removed);
        Assert.Null(slot1.Itemstack);
    }

    [Fact]
    public void HasAtLeast_TrueWhenEnough()
    {
        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot> { CreateSlot("game:berry", 12) }.GetEnumerator());

        Assert.True(inventory.HasAtLeast("game:berry", 10));
    }

    [Fact]
    public void HasAtLeast_FalseWhenNotEnough()
    {
        var inventory = Substitute.For<IInventory>();
        inventory.GetEnumerator().Returns(new List<ItemSlot> { CreateSlot("game:berry", 3) }.GetEnumerator());

        Assert.False(inventory.HasAtLeast("game:berry", 5));
    }

    private static ItemSlot CreateSlot(string code, int stackSize)
    {
        var slot = new DummySlot(new ItemStack(new DummyItem(code)) { StackSize = stackSize });
        return slot;
    }

    private class DummyItem : Item
    {
        public DummyItem(string code)
        {
            Code = new AssetLocation(code);
        }
    }

    private class DummySlot : ItemSlot
    {
        public DummySlot(ItemStack stack) : base(null!)
        {
            Itemstack = stack;
        }

        public override ItemStack TakeOut(int quantity)
        {
            if (Itemstack == null) return null!;

            int remove = Math.Min(quantity, Itemstack.StackSize);
            ItemStack taken;
            if (remove >= Itemstack.StackSize)
            {
                taken = Itemstack;
                Itemstack = null;
            }
            else
            {
                taken = Itemstack.Clone();
                taken.StackSize = remove;
                Itemstack.StackSize -= remove;
            }
            return taken;
        }

        public override void MarkDirty() { }
    }
}
