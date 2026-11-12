using System.Threading.Tasks;
using ArcanumLib.Actions;
using ArcanumLib.Common;
using ArcanumLib.Core;
using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Logging;
using ArcanumLib.Performance;
using ArcanumLib.Progression;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.AtlasTests;

/// <summary>
/// End-to-end smoke tests that verify the core ArcanumLib services are registered and resolvable
/// when the mod starts on a real (headless) Atlas server world.
/// </summary>
[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class ArcanumLibSmokeAtlasTests : AtlasScenarioBase
{
    private ICoreServerAPI Sapi => (ICoreServerAPI)World.Api;

    [AtlasScenario]
    public async Task AllCoreServices_Resolve_AfterServerStart()
    {
        await World.Ticks(5);

        Assert.NotNull(ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IOnlinePlayerCache>());
        Assert.NotNull(ArcanumServices.Get<IEventBusService>());
        Assert.NotNull(ArcanumServices.Get<IStatusEffectService>());
        Assert.NotNull(ArcanumServices.Get<IEffectResistanceService>());
        Assert.NotNull(ArcanumServices.Get<IActionRegistryService>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IActionExecutorService>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IDeferredWorkService>());
        Assert.NotNull(ArcanumServices.Get<IGameTimeScheduler>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IStatCoalescingEngine>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IPlaytimeTracker>(ArcanumServiceScope.Server));
    }

    [AtlasScenario]
    public async Task AllModSystems_AreLoaded_And_Owned_Services_Resolve()
    {
        await World.Ticks(5);

        var loader = Sapi.ModLoader;
        Assert.NotNull(loader.GetModSystem<ArcanumLib.Core.ArcanumLibModSystem>());
        Assert.NotNull(loader.GetModSystem<ArcanumLib.Core.ArcanumDataModSystem>());
        Assert.NotNull(loader.GetModSystem<ArcanumLib.Performance.ArcanumPerformanceModSystem>());
        Assert.NotNull(loader.GetModSystem<ArcanumLib.Effects.StatusEffectModSystem>());
        Assert.NotNull(loader.GetModSystem<ArcanumLib.Common.OnlinePlayerCache>());

        Assert.NotNull(ArcanumServices.Get<IOnlinePlayerCache>());
        Assert.NotNull(ArcanumServices.Get<IStatusEffectService>());
        Assert.NotNull(ArcanumServices.Get<IEffectResistanceService>());
        Assert.NotNull(ArcanumServices.Get<IActionRegistryService>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IActionExecutorService>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IPlaytimeTracker>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IDeferredWorkService>());
        Assert.NotNull(ArcanumServices.Get<IGameTimeScheduler>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<IStatCoalescingEngine>(ArcanumServiceScope.Server));
    }

    [AtlasScenario]
    public async Task CategorizedLogger_CanBeInitialized_AndResolved()
    {
        await World.Ticks(5);

        CategorizedLogger.Init(Sapi, logFolderName: "atlas-smoke");

        Assert.NotNull(CategorizedLogger.Instance);
        Assert.Same(CategorizedLogger.Instance, ArcanumServices.Get<ICategorizedLogger>());
        Assert.NotNull(ArcanumServices.Get<CategorizedLogger>());

        CategorizedLogger.Instance!.Info("atlas-smoke", "logger works");
        CategorizedLogger.Instance.Dispose();
    }

    [AtlasScenario]
    public async Task PityTracker_CanBePublished_ByConsumer()
    {
        await World.Ticks(5);

        Assert.Null(PityTracker.Current);

        var tracker = new PityTracker(Sapi, "atlas-smoke:pity");
        ArcanumServices.Register(tracker, ArcanumServiceScope.Server);
        ArcanumServices.Register<IPityTracker>(tracker, ArcanumServiceScope.Server);
        ArcanumServices.Register<IPityProvider>(tracker, ArcanumServiceScope.Server);

        Assert.Same(tracker, PityTracker.Current);
        Assert.Same(tracker, ArcanumServices.Get<IPityTracker>(ArcanumServiceScope.Server));

        tracker.Initialize();
    }

    /// <summary>Filler blocks reach five cells sideways and eight up, both corners of the range exist once.</summary>
    [AtlasScenario]
    public async Task Multiblock_Fillers_Reach_Five_Sideways_And_Eight_Up()
    {
        await World.Ticks(1);
        foreach (var code in new[] { "multiblock-monolithic-n5-p8-p5", "multiblock-monolithic-p5-n2-n5", "multiblock-monolithic-0-p8-0" })
            Assert.NotNull(Sapi.World.GetBlock(new AssetLocation("game", code)));
        int fillers = 0;
        foreach (var b in Sapi.World.Blocks) if (b?.Code?.Path.StartsWith("multiblock-monolithic-") == true) fillers++;
        Assert.Equal(11 * 11 * 11, fillers);
    }
}
