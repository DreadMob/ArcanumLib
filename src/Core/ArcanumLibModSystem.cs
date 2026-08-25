using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.RadialMenu;
using ArcanumLib.Helpers;
using ArcanumLib.Persistence;
using ArcanumLib.Performance;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Core;

/// <summary>
/// Central lifecycle ModSystem that activates the <see cref="ArcanumRuntime" />,
/// registers the current API with the service registry, and coordinates disposal on world unload.
/// </summary>
public class ArcanumLibModSystem : ModSystem
{
    private ArcanumRuntime? _runtime;

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
    /// Activates the runtime, registers the client API and common API, and initializes client-side caches.
    /// </summary>
    /// <param name="capi">The client API.</param>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        _runtime = ArcanumRuntime.Activate();
        _runtime.Side = EnumAppSide.Client;
        _runtime.Api = capi;

        RegisterLifecycleHandlers();
        RegisterCommonServices();

        _runtime.Services.Register<ICoreAPI>(capi, ArcanumServiceScope.Client);
        _runtime.Services.Register<ICoreClientAPI>(capi, ArcanumServiceScope.Client);
        ImageIconCache.Init(capi);
        CustomTabIconRenderer.RegisterGenericIcons();
        _runtime.Initialize();
    }

    /// <summary>
    /// Activates the runtime, registers the server API and common API.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _runtime = ArcanumRuntime.Activate();
        _runtime.Side = EnumAppSide.Server;
        _runtime.Api = sapi;

        RegisterLifecycleHandlers();
        RegisterCommonServices();

        _runtime.Services.Register<ICoreAPI>(sapi, ArcanumServiceScope.Server);
        _runtime.Services.Register<ICoreServerAPI>(sapi, ArcanumServiceScope.Server);
        _runtime.Initialize();
    }

    private void RegisterLifecycleHandlers()
    {
        ArcanumLifecycle.Register("ImageIconCache", () => { }, ImageIconCache.Dispose);
        ArcanumLifecycle.Register("CollectibleNameResolver", () => { }, CollectibleNameResolver.Clear);
        ArcanumLifecycle.Register("ModDataStore", () => { }, ModDataStore.Clear);
        ArcanumLifecycle.Register("CustomIconRegistry", () => { }, CustomIconRegistry.Clear);
    }

    private void RegisterCommonServices()
    {
        var resistance = new EffectResistanceService();
        _runtime!.Services.Register(resistance);
        _runtime!.Services.Register<IEffectResistanceService>(resistance);

        var eventBus = new EventBusService();
        _runtime!.Services.Register(eventBus);
        _runtime!.Services.Register<IEventBusService>(eventBus);

        var deferred = new DeferredWorkService();
        _runtime!.Services.Register(deferred);
        _runtime!.Services.Register<IDeferredWorkService>(deferred);
    }

    /// <summary>
    /// Disposes the runtime, which runs lifecycle disposal and shuts down all services.
    /// </summary>
    public override void Dispose()
    {
        _runtime?.Dispose();
        _runtime = null;
        base.Dispose();
    }
}
