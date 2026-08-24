using System;
using ArcanumLib.Inventory;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class InventoryFingerprintTests
{
    [Fact]
    public void GetStableStackHash_NullStack_ReturnsZero()
    {
        Assert.Equal(0, InventoryFingerprint.GetStableStackHash(null!));
    }

    [Fact]
    public void GetStableStackHash_SameStacks_HaveSameHash()
    {
        var stackA = new ItemStack(new DummyItem())
        {
            Attributes = new TreeAttribute()
        };
        var stackB = new ItemStack(new DummyItem())
        {
            Attributes = new TreeAttribute()
        };

        // Ensure same code.
        stackA.Collectible.Code = new AssetLocation("game:test");
        stackB.Collectible.Code = new AssetLocation("game:test");

        Assert.Equal(InventoryFingerprint.GetStableStackHash(stackA), InventoryFingerprint.GetStableStackHash(stackB));
    }

    [Fact]
    public void GetStableStackHash_DifferentCodes_HaveDifferentHashes()
    {
        var stackA = new ItemStack(new DummyItem("game:a"));
        var stackB = new ItemStack(new DummyItem("game:b"));

        Assert.NotEqual(InventoryFingerprint.GetStableStackHash(stackA), InventoryFingerprint.GetStableStackHash(stackB));
    }

    [Fact]
    public void GetStableAttributeHash_RoundsFloats()
    {
        var tree = new TreeAttribute();
        tree.SetFloat("value", 1.234f);

        var tree2 = new TreeAttribute();
        tree2.SetFloat("value", 1.235f);

        // Rounded to 2 decimal places (123 vs 124)
        Assert.NotEqual(InventoryFingerprint.GetStableAttributeHash(tree), InventoryFingerprint.GetStableAttributeHash(tree2));
    }

    [Fact]
    public void GetStableAttributeHash_NullTree_ReturnsZero()
    {
        Assert.Equal(0, InventoryFingerprint.GetStableAttributeHash(null));
    }

    [Fact]
    public void GetStableAttributeHash_EmptyTree_IsDeterministic()
    {
        Assert.Equal(InventoryFingerprint.GetStableAttributeHash(new TreeAttribute()),
                     InventoryFingerprint.GetStableAttributeHash(new TreeAttribute()));
    }

    private class DummyItem : Item
    {
        public DummyItem(string? code = null)
        {
            if (code != null) Code = new AssetLocation(code);
        }
    }
}
