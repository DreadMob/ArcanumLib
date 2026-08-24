using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Helpers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Core;

/// <summary>
/// Central lifecycle ModSystem that registers the current API with <see cref="ArcanumServices"/>
/// so other ArcanumLib modules can resolve the active client or server API without static coupling.
/// </summary>
public class ArcanumLibModSystem : ModSystem
{
    /// <summary>
    /// Loads early so the API is available for later systems.
    /// </summary>
    public override double ExecuteOrder() => -1000;

    /// <summary>
    /// Starts on both client and server.
    /// </summary>
    public override bool ShouldLoad(EnumAppSide forSide) => true;

    /// <summary>
    /// Registers the client API and common API.
    /// </summary>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        ArcanumServices.Register<ICoreAPI>(capi);
        ArcanumServices.Register<ICoreClientAPI>(capi);
        ImageIconCache.Init(capi);
    }

    /// <summary>
    /// Registers the server API and common API.
    /// </summary>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        ArcanumServices.Register<ICoreAPI>(sapi);
        ArcanumServices.Register<ICoreServerAPI>(sapi);
    }

    /// <summary>
    /// Disposes icon surfaces, clears name caches and the service registry on world unload.
    /// </summary>
    public override void Dispose()
    {
        ImageIconCache.Dispose();
        CollectibleNameResolver.Clear();
        EventBus.ClearAll();
        EffectResistanceStore.ClearAll();
        ArcanumServices.Shutdown();
        base.Dispose();
    }
}
