using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ArcanumLib.Rendering;

/// <summary>
/// Enables back-face culling for held or dropped items so that an outline shell is visible without internal back faces.
/// </summary>
public sealed class CollectibleBehaviorCullFaces : CollectibleBehavior
{
    /// <summary>
    /// Creates a behavior that enables back-face culling for the supplied collectible.
    /// </summary>
    /// <param name="collectible">The collectible that owns this behavior.</param>
    public CollectibleBehaviorCullFaces(CollectibleObject collectible) : base(collectible)
    {
    }

    /// <summary>
    /// Enables back-face culling for held or ground items. GUI rendering is left unchanged so inventory/HUDs are not affected.
    /// </summary>
    /// <param name="capi">The client API.</param>
    /// <param name="itemstack">The item stack being rendered.</param>
    /// <param name="target">The current item render target.</param>
    /// <param name="renderInfo">The standard render information to update.</param>
    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderInfo)
    {
        if (target == EnumItemRenderTarget.Gui)
        {
            return;
        }

        renderInfo.CullFaces = true;
        capi.Render.GlEnableCullFace();
    }
}
