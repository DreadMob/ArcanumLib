using ArcanumLib.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Consolidated lifecycle ModSystem for all performance/scheduling systems.
/// Starts and disposes <see cref="DeferredWorkService" />, <see cref="GameTimeScheduler" /> and
/// <see cref="StatCoalescingEngine" /> on the appropriate side.
/// </summary>
public class ArcanumPerformanceModSystem : ModSystem
{
    private StatCoalescingEngine? _statCoalescing;
    private GameTimeScheduler? _gameTimeScheduler;

    /// <summary>
    /// Returns the execution order relative to other systems.
    /// </summary>
    /// <returns>The execution order value.</returns>
    public override double ExecuteOrder() => 0.1;

    /// <summary>
    /// Determines whether this system should load on the given side.
    /// </summary>
    /// <param name="forSide">The application side.</param>
    /// <returns><c>true</c> for all sides.</returns>
    public override bool ShouldLoad(EnumAppSide forSide) => true;

    /// <summary>
    /// Starts the deferred work scheduler on the client.
    /// </summary>
    /// <param name="capi">The client API.</param>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        ArcanumServices.Get<DeferredWorkService>()?.Start(capi);
    }

    /// <summary>
    /// Starts the deferred work scheduler, game-time scheduler and stat coalescing engine on the server.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        ArcanumServices.Get<DeferredWorkService>()?.Start(sapi);

        _gameTimeScheduler = new GameTimeScheduler();
        _gameTimeScheduler.Start(sapi);
        ArcanumServices.Register(_gameTimeScheduler, ArcanumServiceScope.Server);

        _statCoalescing = new StatCoalescingEngine();
        _statCoalescing.Start(sapi);
        ArcanumServices.Register(_statCoalescing, ArcanumServiceScope.Server);
    }

    /// <summary>
    /// Stops all performance and scheduling systems.
    /// </summary>
    public override void Dispose()
    {
        _statCoalescing?.Dispose();
        ArcanumServices.Unregister<StatCoalescingEngine>();

        _gameTimeScheduler?.Dispose();
        ArcanumServices.Unregister<GameTimeScheduler>();

        ArcanumServices.Get<DeferredWorkService>()?.Stop();
        base.Dispose();
    }
}
