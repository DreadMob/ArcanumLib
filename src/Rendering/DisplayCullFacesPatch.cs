using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ArcanumLib.Rendering;

/// <summary>
/// Items with <see cref="CollectibleBehaviorCullFaces" /> shown on shelves, in display cases and other
/// <see cref="BlockEntityDisplay" />/<see cref="BEBehaviorDisplay" /> blocks: their chunk-mesh faces move from the
/// no-cull passes to <see cref="EnumChunkRenderPass.Opaque" />, so the back faces are culled there too. Other passes
/// (glass, liquids) and the extra bits are kept.
/// </summary>
internal static class DisplayCullFacesPatch
{
    private const string HarmonyId = "arcanumlib.displaycullfaces";
    private const int PassMask = 0x3FF;   // lower 10 bits = render pass, upper 6 = extra tesselator bits

    private static Harmony? _harmony;

    internal static void Apply()
    {
        if (_harmony != null) return;
        _harmony = new Harmony(HarmonyId);
        _harmony.CreateClassProcessor(typeof(BlockEntityDisplayPatch)).Patch();
        _harmony.CreateClassProcessor(typeof(BehaviorDisplayPatch)).Patch();
    }

    internal static void Dispose()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
    }

    internal static void ApplyCullFaces(ItemSlot? slot, MeshData? mesh)
    {
        if (mesh?.RenderPassesAndExtraBits == null || slot?.Itemstack?.Collectible?.GetBehavior<CollectibleBehaviorCullFaces>() == null) return;

        var passes = mesh.RenderPassesAndExtraBits;
        for (int i = 0; i < passes.Length; i++)
        {
            int pass = passes[i] & PassMask;
            if (pass is (int)EnumChunkRenderPass.OpaqueNoCull or (int)EnumChunkRenderPass.BlendNoCull)
                passes[i] = (short)((passes[i] & ~PassMask) | (int)EnumChunkRenderPass.Opaque);
        }
    }

    [HarmonyPatch(typeof(BlockEntityDisplay), "getOrCreateMesh")]
    private static class BlockEntityDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemSlot slot, MeshData __result) => ApplyCullFaces(slot, __result);
    }

    [HarmonyPatch(typeof(BEBehaviorDisplay), "getOrCreateMesh")]
    private static class BehaviorDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemSlotDisplay slot, MeshData __result) => ApplyCullFaces(slot, __result);
    }
}
