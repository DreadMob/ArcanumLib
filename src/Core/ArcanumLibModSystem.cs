using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Helpers;
using ArcanumLib.Persistence;
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
    static ArcanumLibModSystem()
    {
        // Client-only init for ImageIconCache is done in StartClientSide because it needs ICoreClientAPI.
        ArcanumLifecycle.Register("ImageIconCache", () => { }, ImageIconCache.Dispose);
        ArcanumLifecycle.Register("CollectibleNameResolver", () => { }, CollectibleNameResolver.Clear);
        ArcanumLifecycle.Register("EventBus", () => { }, EventBus.ClearAll);
        ArcanumLifecycle.Register("EffectResistanceStore", () => { }, EffectResistanceStore.ClearAll);
        ArcanumLifecycle.Register("ModDataStore", () => { }, ModDataStore.Clear);
    }

    /// <summary>
    /// Returns the execution order relative to other systems.
    /// </summary>
    /// <returns>The execution order value.</returns>
    public override double ExecuteOrder() => -1000;

    /// <summary>
    /// Determines whether this system should load on the given side.
    /// </summary>
    /// <param name="forSide">The application side.</param>
    /// <returns><c>true</c> for all sides.</returns>
    public override bool ShouldLoad(EnumAppSide forSide) => true;

    /// <summary>
    /// Registers the client API and common API, and initializes client-side caches.
    /// </summary>
    /// <param name="capi">The client API.</param>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        ArcanumServices.Register<ICoreAPI>(capi);
        ArcanumServices.Register<ICoreClientAPI>(capi);
        ImageIconCache.Init(capi);
        CustomTabIconRenderer.RegisterGenericIcons();
    }

    /// <summary>
    /// Registers the server API and common API.
    /// </summary>
    /// <param name="sapi">The server API.</param>
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
        ArcanumLifecycle.DisposeAll();
        ArcanumServices.Shutdown();
        base.Dispose();
    }
}
