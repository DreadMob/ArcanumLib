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

    /// <summary>Ground storage overrides getOrCreateMesh since 1.22: hooking it alone left shelves on vanilla, which
    /// shrank the shared pile mesh (patterns on shelves drawn as tiny bundles).</summary>
    [Fact]
    public void Shelves_Get_The_Mesh_Hook_Too()
    {
        GroundPiles.Apply();
        try
        {
            var display = AccessTools.DeclaredMethod(typeof(BlockEntityDisplay), "getOrCreateMesh");
            Assert.Contains("arcanumlib.groundpiles", Harmony.GetPatchInfo(display)?.Owners ?? Enumerable.Empty<string>());
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

    /// <summary>The mesh hook reads the cache key of any display. Bound to ground storage's override it threw on every
    /// shelf and display case ("does not match target type"), so patterns put there were invisible.</summary>
    [Theory]
    [InlineData(typeof(BlockEntityShelf))]
    [InlineData(typeof(BlockEntityDisplayCase))]
    public void Mesh_Key_Is_Read_On_Shelves_And_Cases(System.Type display)
    {
        var be = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(display);
        var item = new Vintagestory.API.Common.Item { Code = new Vintagestory.API.Common.AssetLocation("alegacyattire", "pattern-herbdoc") };
        var slot = new Vintagestory.API.Common.DummySlot(new Vintagestory.API.Common.ItemStack(item));
        Assert.Equal("alegacyattire:pattern-herbdoc", GroundPiles.MeshKey.Invoke(be, new object[] { slot }));
    }

    [Fact]
    public void Mesh_Key_Still_Reaches_The_Ground_Storage_Override() =>
        Assert.Equal(GroundPiles.MeshKey, AccessTools.Method(typeof(BlockEntityGroundStorage), "getMeshCacheKey").GetBaseDefinition());

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
