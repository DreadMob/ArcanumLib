using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ArcanumLib.Blocks;

/// <summary>Helpers for the cells of a vanilla <c>Multiblock</c> structure. <c>offset</c> is what the game passes to
/// <see cref="IMultiBlockInteract"/>: from the filler cell to the controller.</summary>
public static class MultiblockCells
{
    /// <summary>The selection moved onto the controller, hit point included (as seen from the controller cell).</summary>
    public static BlockSelection ForController(BlockSelection sel, Vec3i offset)
    {
        var moved = sel.AddPosCopy(offset);
        var hit = sel.HitPosition ?? new Vec3d(0.5, 0.5, 0.5);
        moved.HitPosition = hit.AddCopy(-offset.X, -offset.Y, -offset.Z);
        return moved;
    }

    /// <summary>The controller's boxes as seen from the filler cell.</summary>
    public static Cuboidf[] Shift(Cuboidf[]? boxes, Vec3i offset)
    {
        if (boxes == null) return Array.Empty<Cuboidf>();
        var moved = new Cuboidf[boxes.Length];
        for (int i = 0; i < boxes.Length; i++) moved[i] = boxes[i].OffsetCopy(offset.X, offset.Y, offset.Z);
        return moved;
    }
}

/// <summary>
/// For a <see cref="Block"/> that is the controller of a multiblock: every click, hold, help line, pick and sound on a
/// filler cell is handed to the controller's own methods, the selection moved onto it. Declare the interface on the
/// block class; a member the class implements itself wins over these.
/// </summary>
public interface IMultiBlockForwarding : IMultiBlockInteract
{
    private Block Self => (Block)this;

    bool IMultiBlockInteract.MBDoPartialSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
        => Self.DoPartialSelection(world, pos.AddCopy(offset));

    bool IMultiBlockInteract.MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        => Self.OnBlockInteractStart(world, byPlayer, MultiblockCells.ForController(blockSel, offset));

    bool IMultiBlockInteract.MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        => Self.OnBlockInteractStep(secondsUsed, world, byPlayer, MultiblockCells.ForController(blockSel, offset));

    void IMultiBlockInteract.MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        => Self.OnBlockInteractStop(secondsUsed, world, byPlayer, MultiblockCells.ForController(blockSel, offset));

    bool IMultiBlockInteract.MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
        => Self.OnBlockInteractCancel(secondsUsed, world, byPlayer, MultiblockCells.ForController(blockSel, offset), cancelReason);

    ItemStack IMultiBlockInteract.MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
        => Self.OnPickBlock(world, pos.AddCopy(offset));

    WorldInteraction[] IMultiBlockInteract.MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
        => Self.GetPlacedBlockInteractionHelp(world, MultiblockCells.ForController(blockSel, offset), forPlayer);

    BlockSounds IMultiBlockInteract.MBGetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack stack, Vec3i offset)
        => Self.GetSounds(blockAccessor, MultiblockCells.ForController(blockSel, offset), stack);
}

/// <summary>
/// For a multiblock controller whose boxes span the whole structure: each filler cell shows the controller's selection
/// and collision boxes, shifted into its own frame, so the outline and the collision stay where the model is (unlike
/// <c>offsetHitboxes</c>, this also works with partial selection).
/// </summary>
public interface IMultiBlockForwardingBoxes : IMultiBlockColSelBoxes
{
    private Block Self => (Block)this;

    Cuboidf[] IMultiBlockColSelBoxes.MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        => MultiblockCells.Shift(Self.GetCollisionBoxes(blockAccessor, pos.AddCopy(offset)), offset);

    Cuboidf[] IMultiBlockColSelBoxes.MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        => MultiblockCells.Shift(Self.GetSelectionBoxes(blockAccessor, pos.AddCopy(offset)), offset);
}
