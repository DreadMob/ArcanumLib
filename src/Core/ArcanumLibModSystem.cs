using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.RadialMenu;
using ArcanumLib.Helpers;
using ArcanumLib.Logging;
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
    private bool _lifecycleRegistered;

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
    /// Activates the runtime early (during the Pre phase) so that mods whose
    /// <c>StartPre</c> runs after ours can call <see cref="ArcanumServices" />
    /// immediately. Side-specific API and services are registered later in
    /// <see cref="StartClientSide" /> / <see cref="StartServerSide" />.
    /// </summary>
    /// <param name="api">The core API.</param>
    public override void StartPre(ICoreAPI api)
    {
        if (_runtime != null) return;

        _runtime = ArcanumRuntime.Activate();
        _runtime.Api = api;

        WireStaticLogSink(api);
        RegisterLifecycleHandlers();

        _runtime.Services.Register<ICoreAPI>(api, ArcanumServiceScope.Global);
        _runtime.Initialize();
    }

    /// <summary>
    /// Registers the client API, common services, and initializes client-side caches.
    /// The runtime is expected to have been activated in <see cref="StartPre" />; if not,
    /// it is activated here as a fallback.
    /// </summary>
    /// <param name="capi">The client API.</param>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        EnsureRuntime(capi);

        _runtime!.Side = EnumAppSide.Client;
        _runtime.Api = capi;

        WireStaticLogSink(capi);
        RegisterCommonServices(ArcanumServiceScope.Client);

        _runtime.Services.Register<ICoreAPI>(capi, ArcanumServiceScope.Client);
        _runtime.Services.Register<ICoreClientAPI>(capi, ArcanumServiceScope.Client);
        ImageIconCache.Init(capi);
        CustomTabIconRenderer.RegisterGenericIcons();
        _runtime.Initialize();
    }

    /// <summary>
    /// Registers the server API and common services.
    /// The runtime is expected to have been activated in <see cref="StartPre" />; if not,
    /// it is activated here as a fallback.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        EnsureRuntime(sapi);

        _runtime!.Side = EnumAppSide.Server;
        _runtime.Api = sapi;

        WireStaticLogSink(sapi);
        RegisterCommonServices(ArcanumServiceScope.Server);

        _runtime.Services.Register<ICoreAPI>(sapi, ArcanumServiceScope.Server);
        _runtime.Services.Register<ICoreServerAPI>(sapi, ArcanumServiceScope.Server);
        _runtime.Initialize();
    }

    private void EnsureRuntime(ICoreAPI api)
    {
        if (_runtime != null) return;

        _runtime = ArcanumRuntime.Activate();
        _runtime.Api = api;
        WireStaticLogSink(api);
        RegisterLifecycleHandlers();
        _runtime.Services.Register<ICoreAPI>(api, ArcanumServiceScope.Global);
        _runtime.Initialize();
    }

    private void RegisterLifecycleHandlers()
    {
        if (_lifecycleRegistered) return;
        _lifecycleRegistered = true;
        ArcanumLifecycle.Register("ImageIconCache", () => { }, ImageIconCache.Dispose);
        ArcanumLifecycle.Register("CollectibleNameResolver", () => { }, CollectibleNameResolver.Clear);
        ArcanumLifecycle.Register("ModDataStore", () => { }, ModDataStore.Clear);
        ArcanumLifecycle.Register("CustomIconRegistry", () => { }, CustomIconRegistry.Clear);
    }

    private void RegisterCommonServices(ArcanumServiceScope scope)
    {
        var resistance = new EffectResistanceService();
        _runtime!.Services.Register(resistance, scope);
        _runtime!.Services.Register<IEffectResistanceService>(resistance, scope);

        var eventBus = new EventBusService();
        _runtime!.Services.Register(eventBus, scope);
        _runtime!.Services.Register<IEventBusService>(eventBus, scope);

        var deferred = new DeferredWorkService();
        _runtime!.Services.Register(deferred, scope);
        _runtime!.Services.Register<IDeferredWorkService>(deferred, scope);
    }

    private static void WireStaticLogSink(ICoreAPI api)
    {
        StaticLogSink.SetLogger(msg => api?.Logger?.Warning(msg));
    }

    /// <summary>
    /// Disposes the runtime, which runs lifecycle disposal and shuts down all services.
    /// </summary>
    public override void Dispose()
    {
        _runtime?.Dispose();
        _runtime = null;
        _lifecycleRegistered = false;
        StaticLogSink.SetLogger(null);
        base.Dispose();
    }
}
