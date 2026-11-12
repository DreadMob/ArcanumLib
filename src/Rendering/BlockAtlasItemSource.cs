#nullable enable
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Rendering;

/// <summary>
/// An item's textures, put into the BLOCK atlas — for items drawn as part of a block's chunk mesh (a blueprint
/// lying on a bench). The default <c>TesselateItem</c> source points into the item atlas; the chunk tesselator
/// only knows block atlas pages, so such a mesh throws RetryTesselation forever ("textureid N did not yet match
/// an atlas pool") and nothing placed in that chunk afterwards shows up until relog. Same lookup as vanilla
/// <c>BlockEntityDisplay</c>: the item's own code, then "all", then the shape's texture, then the code as a path.
/// Inserting into the atlas must happen on the main thread — build such meshes there, never in OnTesselation.
/// For stacks, <see cref="ChunkItemMeshes"/> caches the result.
/// </summary>
public sealed class BlockAtlasItemSource : ITexPositionSource
{
    private readonly ICoreClientAPI _capi;
    private readonly Item _item;
    private readonly Shape? _shape;

    public BlockAtlasItemSource(ICoreClientAPI capi, Item item)
    {
        _capi = capi;
        _item = item;
        _shape = item.Shape?.Base == null ? null : capi.TesselatorManager.GetCachedShape(item.Shape.Base);
    }

    public Size2i AtlasSize => _capi.BlockTextureAtlas.Size;

    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            AssetLocation? path = null;
            if (_item.Textures != null)
            {
                if (_item.Textures.TryGetValue(textureCode, out var own)) path = own.Baked?.BakedName ?? own.Base;
                else if (_item.Textures.TryGetValue("all", out var all)) path = all.Baked?.BakedName ?? all.Base;
            }
            if (path == null) _shape?.Textures?.TryGetValue(textureCode, out path);
            path ??= new AssetLocation(textureCode);

            var atlas = _capi.BlockTextureAtlas;
            var pos = atlas[path];
            if (pos == null && !atlas.GetOrInsertTexture(path, out _, out pos)) return atlas.UnknownTexturePosition;
            return pos ?? atlas.UnknownTexturePosition;
        }
    }

    /// <summary>A fresh mesh of the item with every texture in the block atlas. Main thread only.</summary>
    public static MeshData? Mesh(ICoreClientAPI capi, Item item)
    {
        capi.Tesselator.TesselateItem(item, out MeshData mesh, new BlockAtlasItemSource(capi, item));
        return mesh;
    }
}
