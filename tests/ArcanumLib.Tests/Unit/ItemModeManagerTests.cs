using System.Collections.Generic;
using ArcanumLib.Actions;
using ArcanumLib.Items;
using Newtonsoft.Json;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ItemModeManagerTests
{
    [Fact]
    public void TryGetModes_NullAttributes_ReturnsFalse()
    {
        Assert.False(ItemModeManager.TryGetModes((ITreeAttribute?)null, out var modes));
        Assert.Empty(modes);
    }

    [Fact]
    public void TryGetModes_MissingKey_ReturnsFalse()
    {
        var attr = new TreeAttribute();
        Assert.False(ItemModeManager.TryGetModes(attr, out var modes));
        Assert.Empty(modes);
    }

    [Fact]
    public void TryGetModes_ValidJson_ReturnsModes()
    {
        var attr = new TreeAttribute();
        var modes = new List<ItemMode>
        {
            new() { Id = "strike", Name = "Strike" },
            new() { Id = "block", Name = "Block" }
        };
        attr.SetString("arcanumlib:modes", JsonConvert.SerializeObject(modes));

        Assert.True(ItemModeManager.TryGetModes(attr, out var parsed));
        Assert.Equal(2, parsed.Count);
        Assert.Equal("strike", parsed[0].Id);
        Assert.Equal("Block", parsed[1].Name);
    }

    [Fact]
    public void TryGetModes_InvalidJson_ReturnsFalse()
    {
        var attr = new TreeAttribute();
        attr.SetString("arcanumlib:modes", "not json");

        Assert.False(ItemModeManager.TryGetModes(attr, out var modes));
        Assert.Empty(modes);
    }

    [Fact]
    public void GetActiveModeIndex_ClampedToRange()
    {
        var attr = new TreeAttribute();
        attr.SetInt("arcanumlib:mode", 5);

        Assert.Equal(2, ItemModeManager.GetActiveModeIndex(attr, 3));
        Assert.Equal(0, ItemModeManager.GetActiveModeIndex(attr, 0));
    }

    [Fact]
    public void GetActiveModeIndex_Negative_ClampedToZero()
    {
        var attr = new TreeAttribute();
        attr.SetInt("arcanumlib:mode", -1);

        Assert.Equal(0, ItemModeManager.GetActiveModeIndex(attr, 3));
    }

    [Fact]
    public void GetActiveModeIndex_NoAttributes_ReturnsZero()
    {
        Assert.Equal(0, ItemModeManager.GetActiveModeIndex(null, 3));
    }

    [Fact]
    public void GetActiveMode_ReturnsSelectedMode()
    {
        var attr = new TreeAttribute();
        var modes = new List<ItemMode>
        {
            new() { Id = "a" },
            new() { Id = "b" }
        };
        attr.SetInt("arcanumlib:mode", 1);

        var mode = ItemModeManager.GetActiveMode(attr, modes);

        Assert.NotNull(mode);
        Assert.Equal("b", mode!.Id);
    }

    [Fact]
    public void TryGetActiveModeId_WithModes_ReturnsId()
    {
        var attr = new TreeAttribute();
        var modes = new List<ItemMode> { new() { Id = "range" } };
        attr.SetString("arcanumlib:modes", JsonConvert.SerializeObject(modes));

        Assert.True(ItemModeManager.TryGetActiveModeId(attr, out var id));
        Assert.Equal("range", id);
    }

    [Fact]
    public void TryGetActiveModeId_NoModes_ReturnsFalse()
    {
        var attr = new TreeAttribute();

        Assert.False(ItemModeManager.TryGetActiveModeId(attr, out var id));
        Assert.Null(id);
    }

    [Fact]
    public void TryGetActiveModeActions_WithActions_ReturnsActions()
    {
        var attr = new TreeAttribute();
        var mode = new ItemMode
        {
            Id = "cast",
            Actions = new()
            {
                new() { Id = "fireball" },
                new() { Id = "heal" }
            }
        };
        attr.SetString("arcanumlib:modes", JsonConvert.SerializeObject(new List<ItemMode> { mode }));

        Assert.True(ItemModeManager.TryGetActiveModeActions(attr, out var actions));
        Assert.Equal(2, actions.Count);
        Assert.Equal("heal", actions[1].Id);
    }

    [Fact]
    public void TryGetActiveModeActions_NoActions_ReturnsFalse()
    {
        var attr = new TreeAttribute();
        var mode = new ItemMode { Id = "empty" };
        attr.SetString("arcanumlib:modes", JsonConvert.SerializeObject(new List<ItemMode> { mode }));

        Assert.False(ItemModeManager.TryGetActiveModeActions(attr, out var actions));
        Assert.Empty(actions);
    }

    [Fact]
    public void SetActiveModeIndex_WritesValue()
    {
        var attr = new TreeAttribute();

        ItemModeManager.SetActiveModeIndex(attr, 2);

        Assert.Equal(2, attr.GetInt("arcanumlib:mode"));
    }

    [Fact]
    public void SetActiveModeIndex_Null_DoesNotThrow()
    {
        var ex = Record.Exception(() => ItemModeManager.SetActiveModeIndex((ITreeAttribute?)null, 2));
        Assert.Null(ex);
    }

    [Fact]
    public void ShouldRunForMode_EmptyEffectId_ReturnsTrue()
    {
        Assert.True(ItemModeManager.ShouldRunForMode("", "any"));
    }

    [Fact]
    public void ShouldRunForMode_Matching_ReturnsTrue()
    {
        Assert.True(ItemModeManager.ShouldRunForMode("RANGE", "range"));
    }

    [Fact]
    public void ShouldRunForMode_Different_ReturnsFalse()
    {
        Assert.False(ItemModeManager.ShouldRunForMode("melee", "range"));
    }

    [Fact]
    public void CycleActiveMode_Forward_WrapsAround()
    {
        var attr = new TreeAttribute();
        var modes = new List<ItemMode>
        {
            new() { Id = "a" },
            new() { Id = "b" },
            new() { Id = "c" }
        };
        attr.SetString("arcanumlib:modes", JsonConvert.SerializeObject(modes));
        attr.SetInt("arcanumlib:mode", 2);

        var next = ItemModeManager.CycleActiveMode(attr, 1);

        Assert.Equal("a", next);
        Assert.Equal(0, attr.GetInt("arcanumlib:mode"));
    }

    [Fact]
    public void CycleActiveMode_Backward_WrapsAround()
    {
        var attr = new TreeAttribute();
        var modes = new List<ItemMode>
        {
            new() { Id = "a" },
            new() { Id = "b" },
            new() { Id = "c" }
        };
        attr.SetString("arcanumlib:modes", JsonConvert.SerializeObject(modes));
        attr.SetInt("arcanumlib:mode", 0);

        var next = ItemModeManager.CycleActiveMode(attr, -1);

        Assert.Equal("c", next);
        Assert.Equal(2, attr.GetInt("arcanumlib:mode"));
    }

    [Fact]
    public void CycleActiveMode_NoModes_ReturnsNull()
    {
        var attr = new TreeAttribute();
        Assert.Null(ItemModeManager.CycleActiveMode(attr, 1));
    }
}
