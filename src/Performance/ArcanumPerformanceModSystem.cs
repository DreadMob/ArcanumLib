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
    private IStatCoalescingEngine? _statCoalescing;
    private IGameTimeScheduler? _gameTimeScheduler;

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
    /// <see cref="IDeferredWorkService"/> is owned and registered by <see cref="ArcanumLibModSystem"/>.
    /// </summary>
    /// <param name="capi">The client API.</param>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        ArcanumServices.Get<IDeferredWorkService>()?.Start(capi);
    }

    /// <summary>
    /// Starts the deferred work scheduler, game-time scheduler and stat coalescing engine on the server.
    /// <see cref="IDeferredWorkService"/> is owned and registered by <see cref="ArcanumLibModSystem"/>.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        ArcanumServices.Get<IDeferredWorkService>()?.Start(sapi);

        var gameTime = new GameTimeScheduler();
        _gameTimeScheduler = gameTime;
        gameTime.Start(sapi);
        ArcanumServices.Register(gameTime, ArcanumServiceScope.Server);
        ArcanumServices.Register<IGameTimeScheduler>(gameTime, ArcanumServiceScope.Server);

        var statCoalescing = new StatCoalescingEngine();
        _statCoalescing = statCoalescing;
        statCoalescing.Start(sapi);
        ArcanumServices.Register(statCoalescing, ArcanumServiceScope.Server);
        ArcanumServices.Register<IStatCoalescingEngine>(statCoalescing, ArcanumServiceScope.Server);
    }

    /// <summary>
    /// Stops all performance and scheduling systems.
    /// <see cref="IDeferredWorkService"/> is owned by <see cref="ArcanumLibModSystem"/> and is only stopped here.
    /// </summary>
    public override void Dispose()
    {
        _statCoalescing?.Dispose();
        ArcanumServices.Unregister<StatCoalescingEngine>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<IStatCoalescingEngine>(ArcanumServiceScope.Server);

        _gameTimeScheduler?.Dispose();
        ArcanumServices.Unregister<GameTimeScheduler>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<IGameTimeScheduler>(ArcanumServiceScope.Server);

        ArcanumServices.Get<IDeferredWorkService>()?.Stop();
        base.Dispose();
    }
}
