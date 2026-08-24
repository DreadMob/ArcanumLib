using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Consolidated lifecycle ModSystem for all performance/scheduling systems.
/// Starts and disposes <see cref="DeferredWork"/>, <see cref="GameTimeScheduler"/> and
/// <see cref="StatCoalescingEngine"/> on the appropriate side.
/// </summary>
public class ArcanumPerformanceModSystem : ModSystem
{
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
        DeferredWork.Start(capi);
    }

    /// <summary>
    /// Starts the deferred work scheduler, game-time scheduler and stat coalescing engine on the server.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        DeferredWork.Start(sapi);
        GameTimeScheduler.Start(sapi);
        StatCoalescingEngine.Start(sapi);
    }

    /// <summary>
    /// Stops all performance and scheduling systems.
    /// </summary>
    public override void Dispose()
    {
        StatCoalescingEngine.Stop();
        GameTimeScheduler.Stop();
        DeferredWork.Stop();
        base.Dispose();
    }
}
