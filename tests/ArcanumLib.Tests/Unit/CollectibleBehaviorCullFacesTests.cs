using ArcanumLib.Rendering;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class CollectibleBehaviorCullFacesTests
{
    [Fact]
    public void OnBeforeRender_LeavesGuiCullingUnchanged()
    {
        var behavior = new CollectibleBehaviorCullFaces(new Item());
        var capi = Substitute.For<ICoreClientAPI>();
        var render = Substitute.For<IRenderAPI>();
        capi.Render.Returns(render);
        var renderInfo = new ItemRenderInfo { CullFaces = false };

        behavior.OnBeforeRender(capi, null!, EnumItemRenderTarget.Gui, ref renderInfo);

        Assert.False(renderInfo.CullFaces);
        render.DidNotReceive().GlEnableCullFace();
    }

    [Fact]
    public void OnBeforeRender_EnablesBackFaceCullingForHeldOrGround()
    {
        var behavior = new CollectibleBehaviorCullFaces(new Item());
        var capi = Substitute.For<ICoreClientAPI>();
        var render = Substitute.For<IRenderAPI>();
        capi.Render.Returns(render);
        var renderInfo = new ItemRenderInfo { CullFaces = false };

        behavior.OnBeforeRender(capi, null!, EnumItemRenderTarget.HandTp, ref renderInfo);

        Assert.True(renderInfo.CullFaces);
        render.Received(1).GlEnableCullFace();
    }

    [Fact]
    public void GroundStoragePatch_AppliesAndDisposes()
    {
        GroundStorageCullFacesPatch.Apply();

        GroundStorageCullFacesPatch.Dispose();
    }
}
