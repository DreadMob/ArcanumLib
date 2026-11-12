using System.Collections.Generic;
using ArcanumLib.Rendering;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ChunkItemMeshesTests
{
    private sealed class SelfDrawn : Item, IContainedMeshSource
    {
        public int Built;
        public ITextureAtlasAPI? Atlas;
        public string Look = "red";

        public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            Built++;
            Atlas = targetAtlas;
            return new MeshData(4, 6);
        }

        public string GetMeshCacheKey(ItemSlot slot) => Look;
    }

    private static ICoreClientAPI Capi()
    {
        var capi = Substitute.For<ICoreClientAPI>();
        capi.ObjectCache.Returns(new Dictionary<string, object>());
        capi.BlockTextureAtlas.Returns(Substitute.For<IBlockTextureAtlasAPI>());
        return capi;
    }

    [Fact]
    public void Key_Tells_Items_Blocks_And_Looks_Apart()
    {
        var item = new Item { Code = new AssetLocation("mod", "part") };
        var block = new Block { Code = new AssetLocation("mod", "part") };
        var drawn = new SelfDrawn { Code = new AssetLocation("mod", "rocket") };
        Assert.Equal("item:mod:part", ChunkItemMeshes.Key(new ItemStack(item)));
        Assert.Equal("block:mod:part", ChunkItemMeshes.Key(new ItemStack(block)));
        Assert.Equal("item:mod:rocket|red", ChunkItemMeshes.Key(new ItemStack(drawn)));
    }

    [Fact]
    public void Self_Drawn_Stacks_Are_Asked_With_The_Block_Atlas_Once_And_Copied()
    {
        var capi = Capi();
        var drawn = new SelfDrawn { Code = new AssetLocation("mod", "rocket") };
        var stack = new ItemStack(drawn);

        var a = ChunkItemMeshes.Get(capi, stack);
        var b = ChunkItemMeshes.Get(capi, stack);
        Assert.NotNull(a);
        Assert.NotSame(a, b);
        Assert.Equal(1, drawn.Built);
        Assert.Same(capi.BlockTextureAtlas, drawn.Atlas);

        drawn.Look = "blue";
        ChunkItemMeshes.Get(capi, stack);
        Assert.Equal(2, drawn.Built);
    }

    [Fact]
    public void No_Stack_No_Mesh()
    {
        Assert.Null(ChunkItemMeshes.Get(Capi(), null));
    }

    [Fact]
    public void Item_Textures_Resolve_In_The_Block_Atlas()
    {
        var capi = Capi();
        var atlas = capi.BlockTextureAtlas;
        var own = new TextureAtlasPosition();
        var fallback = new TextureAtlasPosition();
        atlas[new AssetLocation("mod", "item/own")].Returns(own);
        atlas[new AssetLocation("mod", "item/all")].Returns(fallback);
        var item = new Item
        {
            Code = new AssetLocation("mod", "part"),
            Textures = new Dictionary<string, CompositeTexture>
            {
                ["own"] = new(new AssetLocation("mod", "item/own")),
                ["all"] = new(new AssetLocation("mod", "item/all"))
            }
        };
        var source = new BlockAtlasItemSource(capi, item);
        Assert.Same(own, source["own"]);
        Assert.Same(fallback, source["other"]);
    }
}
