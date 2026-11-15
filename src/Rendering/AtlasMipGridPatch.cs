using HarmonyLib;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace ArcanumLib.Rendering;

/// <summary>
/// Keeps every texture in the client atlases on an 8 px grid. The game packs textures edge to edge, so one texture with
/// a side of 4 mod 8 (a third-party 84×84 game board, a 12 px label) moves everything packed after it off the mip grid:
/// at mip level 3 an 8×8 atlas cell then mixes two textures and far terrain shows black/orange lines along block edges.
/// Each texture gets a place rounded up to 8 px; the texture still uses only its own size, and the spare strip repeats
/// its edge pixels, so the mips of its border keep its own colour.
/// </summary>
internal static class AtlasMipGridPatch
{
    private const string HarmonyId = "arcanumlib.atlasmipgrid";
    private const int Grid = 8;

    private static readonly AccessTools.FieldRef<TextureAtlas, int[]> AtlasPixels =
        AccessTools.FieldRefAccess<TextureAtlas, int[]>("atlasPixels");
    private static readonly System.Reflection.FieldInfo NodeBounds =
        AccessTools.Field(AccessTools.TypeByName("Vintagestory.Client.NoObf.TextureAtlasNode"), "bounds");

    private static Harmony? _harmony;

    /// <summary>Real size and the padded place of the last node handed out on this thread.</summary>
    [ThreadStatic] private static int _w, _h;
    [ThreadStatic] private static QuadBoundsi? _placed;
    [ThreadStatic] private static int _padW, _padH;

    internal static void Apply()
    {
        if (_harmony != null) return;
        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(AccessTools.Method(typeof(TextureAtlas), "GetFreeNode"),
            prefix: new HarmonyMethod(typeof(AtlasMipGridPatch), nameof(RoundUp)),
            postfix: new HarmonyMethod(typeof(AtlasMipGridPatch), nameof(ShrinkToTexture)));
        _harmony.Patch(AccessTools.Method(typeof(TextureAtlas), nameof(TextureAtlas.InsertTexture), new[] { typeof(int), typeof(int), typeof(int), typeof(int[]) }),
            postfix: new HarmonyMethod(typeof(AtlasMipGridPatch), nameof(FillPadding)));
    }

    internal static void Dispose()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
    }

    internal static int Round(int size) => (size + Grid - 1) / Grid * Grid;

    private static void RoundUp(ref int width, ref int height)
    {
        _w = width;
        _h = height;
        width = Round(width);
        height = Round(height);
        _placed = null;
    }

    private static void ShrinkToTexture(object? __result)
    {
        if (__result == null || NodeBounds.GetValue(__result) is not QuadBoundsi b) return;
        _padW = b.x2 - b.x1;
        _padH = b.y2 - b.y1;
        if (_padW == _w && _padH == _h) return;

        // The node is the parent split's whole padded cell, nothing else gets the spare strip
        b.x2 = b.x1 + _w;
        b.y2 = b.y1 + _h;
        _placed = b;
    }

    private static void FillPadding(TextureAtlas __instance, bool __result, int width, int height, int[] pixels)
    {
        var b = _placed;
        _placed = null;
        if (!__result || pixels == null || b == null || b.x2 - b.x1 != width || b.y2 - b.y1 != height) return;

        var atlas = AtlasPixels(__instance);
        int stride = __instance.width;
        for (int y = 0; y < _padH; y++)
        {
            int row = (b.y1 + Math.Min(y, height - 1)) * stride + b.x1;
            int dst = (b.y1 + y) * stride + b.x1;
            if (y >= height) Array.Copy(atlas, row, atlas, dst, width);
            int edge = atlas[dst + width - 1];
            for (int x = width; x < _padW; x++) atlas[dst + x] = edge;
        }
    }
}
