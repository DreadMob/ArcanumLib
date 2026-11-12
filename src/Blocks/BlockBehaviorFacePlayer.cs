using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ArcanumLib.Blocks;

/// <summary>
/// HorizontalOrientable for models whose "north" variant has its front on −Z (rotateY 0/270/180/90): it places the
/// variant OPPOSITE to the player's look, so the front faces the placing player like a vanilla block's (vanilla
/// picks the look direction and the model ends up turned away). Multiblock footprints follow the variant and grow
/// away from the player. Drop, pick block, schematic rotation and the placement preview work as in vanilla.
/// Registered by ArcanumLib as <c>arcanumlib.FacePlayer</c>; the variant group is <c>horizontalorientation</c> when the
/// block has one, else <c>side</c>, as in the vanilla behaviour.
/// </summary>
public class BlockBehaviorFacePlayer : BlockBehaviorHorizontalOrientable, ILookAwarePlacement
{
    public const string ClassName = "arcanumlib.FacePlayer";

    public BlockBehaviorFacePlayer(Block block) : base(block) { }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
    {
        handling = EnumHandling.PreventDefault;
        Block variant = GetLookAwareBlockVariant(byPlayer, itemstack, blockSel);
        if (!variant.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;
        variant.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        return true;
    }

    public new Block GetLookAwareBlockVariant(IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
    {
        BlockFacing look = Block.SuggestedHVOrientation(byPlayer, blockSel)[0];
        AssetLocation code = block.CodeWithVariant(variantCode, look.Opposite.Code);
        return byPlayer.Entity.World.BlockAccessor.GetBlock(code)
            ?? throw new NullReferenceException("Unable to find a rotated block with code " + code);
    }
}
