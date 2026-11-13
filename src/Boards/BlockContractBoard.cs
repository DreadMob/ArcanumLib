using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// A contract board block: a click opens the board named by <c>attributes.boardKey</c> (see
    /// <see cref="BoardRegistry" />). Contracts are not stored in the block: every block with the same key shows the same
    /// board. Registered as <c>arcanumlib.ContractBoard</c> (block entity: the same name).
    /// </summary>
    public class BlockContractBoard : Block
    {
        public const string ClassName = "arcanumlib.ContractBoard";

        /// <summary>Board key when the blocktype has no <c>boardKey</c> attribute.</summary>
        protected virtual string DefaultBoardKey => "default";

        /// <summary>Chat text when the board is not registered (yet).</summary>
        protected virtual string NotReadyText => Lang.Get("arcanumlib:board-not-ready");

        public string BoardKey => Attributes?["boardKey"]?.AsString(null) ?? DefaultBoardKey;

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            if (byPlayer != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                failureCode = "claimed";
                return false;
            }
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world == null || byPlayer == null || blockSel == null) return false;
            if (world.Side == EnumAppSide.Client) return true;
            if (byPlayer is not IServerPlayer sp) return true;

            var registry = world.Api.ModLoader.GetModSystem<BoardRegistry>();
            if (registry == null || !registry.Open(sp, BoardKey))
                sp.SendMessage(GlobalConstants.GeneralChatGroup, NotReadyText, EnumChatType.Notification);
            return true;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);
            if (world?.BlockAccessor == null || EntityClass == null) return;
            world.BlockAccessor.RemoveBlockEntity(pos);
        }
    }

    /// <summary>Block entity of a board: keeps no data (the board is global), only anchors the block.</summary>
    public class BlockEntityContractBoard : BlockEntity
    {
    }
}
