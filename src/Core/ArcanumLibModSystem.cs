using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.RadialMenu;
using ArcanumLib.Helpers;
using ArcanumLib.Logging;
using ArcanumLib.Persistence;
using ArcanumLib.Performance;
using ArcanumLib.Rendering;
using ArcanumLib.Rendering.Glass;
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
    private EnumAppSide _runtimeSide;
    private bool _lifecycleRegistered;
    private bool _groundPiles;
    private bool _clientSide;

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
        api.RegisterBlockBehaviorClass(ArcanumLib.Blocks.BlockBehaviorFacePlayer.ClassName, typeof(ArcanumLib.Blocks.BlockBehaviorFacePlayer));
        if (!_groundPiles) { ArcanumLib.Blocks.GroundPiles.Apply(); _groundPiles = true; }
        if (_runtime != null) return;

        api.RegisterCollectibleBehaviorClass("ArcanumCullFaces", typeof(CollectibleBehaviorCullFaces));
        _runtime = ArcanumRuntime.Join(api.Side);
        _runtimeSide = api.Side;
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
        _clientSide = true;

        _runtime!.Side = EnumAppSide.Client;
        _runtime.Api = capi;

        WireStaticLogSink(capi);
        RegisterCommonServices(ArcanumServiceScope.Client);

        _runtime.Services.Register<ICoreAPI>(capi, ArcanumServiceScope.Client);
        _runtime.Services.Register<ICoreAPI>(capi, ArcanumServiceScope.Global);
        _runtime.Services.Register<ICoreClientAPI>(capi, ArcanumServiceScope.Client);
        capi.RegisterEntityRendererClass(GlassEntityShapeRenderer.ClassName, typeof(GlassEntityShapeRenderer));
        ImageIconCache.Init(capi);
        CustomTabIconRenderer.RegisterGenericIcons();
        GroundStorageCullFacesPatch.Apply();
        DisplayCullFacesPatch.Apply();
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
        _runtime.Services.Register<ICoreAPI>(sapi, ArcanumServiceScope.Global);
        _runtime.Services.Register<ICoreServerAPI>(sapi, ArcanumServiceScope.Server);
        _runtime.Initialize();
    }

    private void EnsureRuntime(ICoreAPI api)
    {
        if (_runtime != null) return;

        _runtime = ArcanumRuntime.Join(api.Side);
        _runtimeSide = api.Side;
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
        RegisterWorldRegistries();
    }

    /// <summary>Static registries emptied with the world (<see cref="ArcanumLifecycle.DisposeAll" />).</summary>
    internal static void RegisterWorldRegistries()
    {
        ArcanumLifecycle.Register("ImageIconCache", () => { }, ImageIconCache.Dispose);
        ArcanumLifecycle.Register("CollectibleNameResolver", () => { }, CollectibleNameResolver.Clear);
        ArcanumLifecycle.Register("ModDataStore", () => { }, ModDataStore.Clear);
        ArcanumLifecycle.Register("CustomIconRegistry", () => { }, CustomIconRegistry.Clear);
        // outlines are keyed by collectible ids the server assigned; another world numbers items differently
        ArcanumLifecycle.Register("GuiItemFit", () => { }, Gui.Controls.GuiItemFit.Clear);
        // a dialog closed by world unload never removes its occluder, the next world's slots would stay hidden
        ArcanumLifecycle.Register("GuiOccluders", () => { }, Gui.Controls.GuiOccluders.Clear);
        // wrappers hold the old world's api and network channel; the next world must register its own
        ArcanumLifecycle.Register("ArcanumChannelHub", () => { }, Network.ArcanumChannelHub.Clear);
        // installed by a mod's StartServerSide on every start; must not route into an unloaded mod
        ArcanumLifecycle.Register("ChatAnnounce", () => { }, () => Text.ChatAnnounce.Handler = null);
        // stamps are World.ElapsedMilliseconds, which starts again at 0 in the next world (would mute denials)
        ArcanumLifecycle.Register("BlockEntityAccessGate", () => { }, Common.BlockEntityAccessGate.ClearDenyLog);
        // Not cleared: RadialMenuStyleRegistry (stateless style objects with no api or textures, any mod's
        // registration stays valid for every world). ArcanumTexturePool is GL-bound and disposed in Dispose()
        // of the client instance, on the main thread.
    }

    private void RegisterCommonServices(ArcanumServiceScope scope)
    {
        var resistance = new EffectResistanceService();
        _runtime!.Services.Register(resistance, scope);
        _runtime!.Services.Register<IEffectResistanceService>(resistance, scope);

        var eventBus = new EventBusService(scope);
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
        GroundStorageCullFacesPatch.Dispose();
        DisplayCullFacesPatch.Dispose();
        if (_groundPiles) { ArcanumLib.Blocks.GroundPiles.Dispose(); _groundPiles = false; }
        // GL textures: freed here, on the client's main thread (the lifecycle may run on the server thread)
        if (_clientSide) { Gui.ArcanumTexturePool.DisposeAll(); _clientSide = false; }
        _runtime?.Leave(_runtimeSide);
        _runtime = null;
        _lifecycleRegistered = false;
        StaticLogSink.SetLogger(null);
        base.Dispose();
    }
}
