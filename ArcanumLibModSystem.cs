using ArcanumLib.Gui.Icons;
using ArcanumLib.Helpers;
using Vintagestory.API.Common;

namespace ArcanumLib;

/// <summary>
/// Entry point for the Arcanum Lib mod. The library exposes static helpers
/// (e.g. ImageIconCache, RGBA) that other mods can use without needing a
/// running ModSystem instance.
/// </summary>
public class ArcanumLibModSystem : ModSystem
{
    public override void StartPre(ICoreAPI api)
    {
        api.Logger.Notification("[Arcanum Lib] loaded.");
    }

    /// <summary>
    /// Disposes static caches that hold client-side resources or per-language
    /// data so they do not leak across world unload / reload cycles.
    /// </summary>
    public override void Dispose()
    {
        ImageIconCache.Dispose();
        CollectibleNameResolver.Clear();
    }
}
