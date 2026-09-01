using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ArcanumLib.Rendering;

internal static class DisplayCullFacesPatch
{
    private static void ApplyCullFaces(ItemSlot slot, MeshData? mesh)
    {
        if (mesh == null || slot.Itemstack?.Collectible.GetBehavior<CollectibleBehaviorCullFaces>() == null) return;

        mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.Opaque);
    }

    [HarmonyPatch(typeof(BlockEntityDisplay), "getOrCreateMesh")]
    private static class BlockEntityDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemSlot slot, MeshData __result)
        {
            ApplyCullFaces(slot, __result);
        }
    }

    [HarmonyPatch(typeof(BEBehaviorDisplay), "getOrCreateMesh")]
    private static class BehaviorDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemSlotDisplay slot, MeshData __result)
        {
            ApplyCullFaces(slot, __result);
        }
    }
}
