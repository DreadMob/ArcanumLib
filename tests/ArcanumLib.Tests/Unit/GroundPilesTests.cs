using System.Linq;
using ArcanumLib.Blocks;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ArcanumLib.Tests.Unit;

public class GroundPilesTests
{
    /// <summary>Harmony binds hook parameters by name only when patching, so a renamed vanilla parameter breaks the
    /// game silently: apply the real patches once and undo them.</summary>
    [Fact]
    public void Patches_Bind_To_The_Game()
    {
        GroundPiles.Apply();
        try
        {
            var patched = Harmony.GetAllPatchedMethods().Where(m => Harmony.GetPatchInfo(m)!.Owners.Contains("arcanumlib.groundpiles"))
                .Select(m => m.Name).ToList();
            foreach (var name in new[] { "FromTreeAttributes", "get_Capacity", "get_DisplayedItems", "GetLayoutOffset", "genTransformationMatrices",
                         "putOrGetItemSingle", "regenCollisionSelectionBox", "UpdateLegacyStorageLayouts", "getMeshCacheKey", "getOrCreateMesh", "TryCreateKiln" })
                Assert.Contains(name, patched);
        }
        finally { GroundPiles.Dispose(); }
    }

    /// <summary>The mesh-key / mesh patches land on BlockEntityDisplay (shared by shelves and display cases), so their
    /// hooks must take the base type and filter ground storage themselves.</summary>
    [Fact]
    public void Mesh_Hooks_Accept_Any_Display()
    {
        foreach (var name in new[] { nameof(GroundPiles.MeshKeyPostfix), nameof(GroundPiles.MeshPrefix) })
            Assert.Equal(typeof(BlockEntityDisplay), AccessTools.Method(typeof(GroundPiles), name).GetParameters()[0].ParameterType);
    }

    [Fact]
    public void Fan_Turns_Cover_Every_Slot() => Assert.True(GroundPiles.FanYaw.Length >= GroundPiles.MaxSlots);

    [Fact]
    public void Flat_Pile_Height_Is_Layers_Times_Step()
    {
        var pile = new JsonObject(JToken.FromObject(new { step = 0.08125f }));
        Assert.Equal(12 * 0.08125f, GroundPiles.PileHeight(pile, 12), 4);
        Assert.Equal(0f, GroundPiles.PileHeight(pile, 0));
    }
}
