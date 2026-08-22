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
}
