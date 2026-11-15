using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ArcanumLib.Inventory;

/// <summary>
/// Puts every mod item and block that sits in a creative tab into the <c>general</c> tab too. The creative search runs
/// over that tab, and vanilla lists almost everything there next to its own tab, while mods usually name only their
/// own tab, so their things could not be found by name.
/// <para>Runs on the server once the types are loaded; clients get the tabs with the item and block packets. The
/// game's own things (domain <c>game</c>) are left alone: those missing from <c>general</c> are meant to be.</para>
/// </summary>
public class CreativeGeneralTab : ModSystem
{
    public const string General = "general";

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override double ExecuteOrder() => 1.0;

    public override void AssetsFinalize(ICoreAPI api)
    {
        int added = 0;
        foreach (CollectibleObject obj in api.World.Blocks.Cast<CollectibleObject>().Concat(api.World.Items))
            if (Add(obj)) added++;
        if (added > 0) api.Logger.Notification("[ArcanumLib] {0} mod items and blocks added to the general creative tab", added);
    }

    /// <summary>Adds <paramref name="obj"/> to <see cref="General"/> where it shows in other tabs only. Returns whether
    /// anything changed.</summary>
    public static bool Add(CollectibleObject? obj)
    {
        if (obj?.Code == null || obj.Code.Domain == GlobalConstants.DefaultDomain) return false;
        var tabs = obj.CreativeInventoryTabs;
        var stacks = obj.CreativeInventoryStacks;
        bool inTabs = tabs is { Length: > 0 }, inStacks = stacks?.Any(s => s?.Tabs is { Length: > 0 }) == true;
        if (!inTabs && !inStacks) return false;
        if (tabs?.Contains(General) == true || stacks?.Any(s => s?.Tabs?.Contains(General) == true) == true) return false;

        if (inTabs) obj.CreativeInventoryTabs = tabs!.Append(General).ToArray();
        if (inStacks)
        {
            // The lists are shared by all variants of a blocktype (the array is cloned shallow), so they are replaced,
            // never changed in place.
            obj.CreativeInventoryStacks = stacks!.Select(s => s?.Tabs is not { Length: > 0 } ? s : new CreativeTabAndStackList
            {
                Tabs = s.Tabs.Append(General).ToArray(),
                Stacks = s.Stacks,
            }).ToArray();
        }
        return true;
    }
}
