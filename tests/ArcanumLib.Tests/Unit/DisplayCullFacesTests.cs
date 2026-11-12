using ArcanumLib.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class DisplayCullFacesTests
{
    private const short ExtraBit = 1 << 10;

    private static MeshData Mesh() => new MeshData
    {
        RenderPassesAndExtraBits = new short[]
        {
            (short)EnumChunkRenderPass.OpaqueNoCull,
            (short)EnumChunkRenderPass.BlendNoCull,
            (short)EnumChunkRenderPass.Transparent,
            (short)((int)EnumChunkRenderPass.OpaqueNoCull | ExtraBit),
        },
    };

    private static ItemSlot Slot(bool cull)
    {
        var item = new Item();
        if (cull) item.CollectibleBehaviors = new CollectibleBehavior[] { new CollectibleBehaviorCullFaces(item) };
        return new DummySlot(new ItemStack(item));
    }

    [Fact]
    public void CullItems_MoveNoCullFacesToOpaque_KeepingOtherPassesAndExtraBits()
    {
        var mesh = Mesh();
        DisplayCullFacesPatch.ApplyCullFaces(Slot(cull: true), mesh);

        Assert.Equal(new short[]
        {
            (short)EnumChunkRenderPass.Opaque,
            (short)EnumChunkRenderPass.Opaque,
            (short)EnumChunkRenderPass.Transparent,
            (short)((int)EnumChunkRenderPass.Opaque | ExtraBit),
        }, mesh.RenderPassesAndExtraBits);
    }

    [Fact]
    public void OtherItems_AreLeftAlone()
    {
        var mesh = Mesh();
        var before = (short[])mesh.RenderPassesAndExtraBits.Clone();
        DisplayCullFacesPatch.ApplyCullFaces(Slot(cull: false), mesh);
        Assert.Equal(before, mesh.RenderPassesAndExtraBits);
    }

    [Fact]
    public void Patch_AppliesAndDisposes()
    {
        DisplayCullFacesPatch.Apply();
        DisplayCullFacesPatch.Dispose();
    }
}
