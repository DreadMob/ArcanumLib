using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ArcanumLib.Blocks.Clock;

/// <summary>
/// Clock block: right click opens the chime-hour settings (see <see cref="BlockEntityClock"/>).
/// Name/description lang keys may omit the trailing facing variant (<c>block-mymod:clock</c> for <c>clock-north</c>).
/// Register under your own class name: <c>api.RegisterBlockClass("mymod.BlockClock", typeof(BlockClock))</c>.
/// </summary>
public class BlockClock : Block
{
    public override string GetHeldItemName(ItemStack itemStack)
    {
        string? baseKey = BaseLangKey("block");
        if (baseKey != null)
        {
            string name = Lang.GetMatching(baseKey);
            if (!string.IsNullOrEmpty(name) && name != baseKey) return name;
        }
        return base.GetHeldItemName(itemStack);
    }

    public override string GetItemDescText()
    {
        string? baseKey = BaseLangKey("blockdesc");
        if (baseKey != null)
        {
            string desc = Lang.GetMatching(baseKey);
            if (!string.IsNullOrEmpty(desc) && desc != baseKey) return desc + "\n";
        }
        return base.GetItemDescText();
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world == null || byPlayer == null || blockSel == null) return false;

        var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityClock;
        be?.OnInteract(byPlayer);
        return true;
    }

    private string? BaseLangKey(string prefix)
    {
        if (Code == null) return null;

        string path = Code.Path;
        int dash = path.LastIndexOf('-');
        if (dash <= 0) return null;

        string suffix = path.Substring(dash + 1);
        if (suffix != "north" && suffix != "east" && suffix != "south" && suffix != "west") return null;

        string basePath = path.Substring(0, dash);
        return $"{Code.Domain}{AssetLocation.LocationSeparator}{prefix}-{basePath}";
    }
}
