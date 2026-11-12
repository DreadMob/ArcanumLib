#nullable enable
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ArcanumLib.Rendering;

/// <summary>
/// Meshes of stacks a block entity adds to its chunk mesh (a part in a basket, a vessel under a tap, a kit on a
/// turntable), every texture in the block atlas (see <see cref="BlockAtlasItemSource"/>). A collectible that draws
/// itself (<see cref="IContainedMeshSource"/>) is asked with the block atlas; a block gets its default block mesh; any
/// other item is tesselated with <see cref="BlockAtlasItemSource"/>. Meshes are cached per client by
/// <see cref="Key"/> and handed out as copies, so the caller may scale and move them. Main thread only: build them
/// when the stack changes, then add them in OnTesselation.
/// </summary>
public static class ChunkItemMeshes
{
    private const string CacheKey = "arcanumlib-chunkitemmeshes";

    /// <summary>A copy of the stack's mesh in the block atlas, or null when it has none.</summary>
    /// <param name="at">Passed to <see cref="IContainedMeshSource.GenMesh"/>.</param>
    public static MeshData? Get(ICoreClientAPI capi, ItemStack? stack, BlockPos? at = null)
    {
        if (stack?.Collectible == null) return null;
        var cache = ObjectCacheUtil.GetOrCreate(capi, CacheKey, () => new Dictionary<string, MeshData?>());
        string key = Key(stack);
        if (!cache.TryGetValue(key, out var mesh)) cache[key] = mesh = Build(capi, stack, at);
        return mesh?.Clone();
    }

    /// <summary>What the cached mesh depends on: the collectible's code, plus the collectible's own mesh cache key
    /// when it draws itself (attributes that change its look).</summary>
    public static string Key(ItemStack stack)
    {
        var collectible = stack.Collectible;
        string code = (stack.Class == EnumItemClass.Block ? "block:" : "item:") + collectible.Code;
        return collectible.GetCollectibleInterface<IContainedMeshSource>() is { } source
            ? code + "|" + source.GetMeshCacheKey(new DummySlot(stack))
            : code;
    }

    private static MeshData? Build(ICoreClientAPI capi, ItemStack stack, BlockPos? at)
    {
        var mesh = stack.Collectible.GetCollectibleInterface<IContainedMeshSource>()?.GenMesh(new DummySlot(stack), capi.BlockTextureAtlas, at);
        if (mesh == null && stack.Block is { } block) mesh = capi.TesselatorManager.GetDefaultBlockMesh(block)?.Clone();
        if (mesh == null && stack.Item is { } item) mesh = BlockAtlasItemSource.Mesh(capi, item);
        return mesh;
    }

    /// <summary>Forgets every cached mesh (after a resource reload).</summary>
    public static void Clear(ICoreClientAPI capi) => ObjectCacheUtil.Delete(capi, CacheKey);
}
