using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace ArcanumLib.Rendering;

[HarmonyPatch(typeof(GroundStorageRenderer), nameof(GroundStorageRenderer.OnRenderFrame))]
internal static class GroundStorageCullFacesPatch
{
    private const string HarmonyId = "arcanumlib.rendering.groundstoragecullfaces";
    private static readonly MethodInfo DisableCullFaceMethod = AccessTools.Method(typeof(IRenderAPI), nameof(IRenderAPI.GlDisableCullFace));
    private static readonly MethodInfo ConfigureCullFaceMethod = AccessTools.Method(typeof(GroundStorageCullFacesPatch), nameof(ConfigureCullFace));
    private static readonly FieldInfo GroundStorageField = AccessTools.Field(typeof(GroundStorageRenderer), "groundStorage");
    private static Harmony? _harmony;

    [ThreadStatic]
    private static IRenderAPI? _renderToRestore;

    internal static void Apply()
    {
        if (_harmony != null) return;

        _harmony = new Harmony(HarmonyId);
        _harmony.CreateClassProcessor(typeof(GroundStorageCullFacesPatch)).Patch();
    }

    internal static void Dispose()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
        _renderToRestore = null;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(DisableCullFaceMethod))
            {
                yield return instruction;
                continue;
            }

            var loadRenderer = new CodeInstruction(OpCodes.Ldarg_0);
            loadRenderer.labels.AddRange(instruction.labels);
            loadRenderer.blocks.AddRange(instruction.blocks);
            yield return loadRenderer;
            yield return new CodeInstruction(OpCodes.Call, ConfigureCullFaceMethod);
        }
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        RestoreCullFace();
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        RestoreCullFace();
        return __exception;
    }

    private static void ConfigureCullFace(IRenderAPI render, GroundStorageRenderer renderer)
    {
        var groundStorage = (BlockEntityGroundStorage)GroundStorageField.GetValue(renderer)!;
        var cullFaces = groundStorage.Inventory.Any(slot =>
            slot.Itemstack?.Collectible.GetBehavior<CollectibleBehaviorCullFaces>() != null);

        if (cullFaces)
        {
            _renderToRestore = render;
            render.GlEnableCullFace();
        }
        else
        {
            render.GlDisableCullFace();
        }
    }

    private static void RestoreCullFace()
    {
        if (_renderToRestore == null) return;

        _renderToRestore.GlDisableCullFace();
        _renderToRestore = null;
    }
}
