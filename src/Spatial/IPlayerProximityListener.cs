using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Spatial;

/// <summary>
/// Receives proximity events for a single tracked position and radius.
/// Implement this on block entities, effects, or other consumers that need to
/// react when players enter, remain inside, or leave a spherical zone.
/// </summary>
public interface IPlayerProximityListener
{
    /// <summary>Center position of the proximity zone.</summary>
    BlockPos Position { get; }

    /// <summary>Radius of the proximity zone in blocks.</summary>
    float Radius { get; }

    /// <summary>Called when a player enters the zone.</summary>
    /// <param name="player">The server player that entered.</param>
    void OnPlayerEntered(IServerPlayer player);

    /// <summary>
    /// Called each tick while a player remains inside the zone.
    /// The interval is determined by <see cref="PlayerProximityTracker.TickIntervalMs" />.
    /// </summary>
    /// <param name="player">The server player that stayed.</param>
    void OnPlayerStayed(IServerPlayer player);

    /// <summary>Called when a player leaves the zone.</summary>
    /// <param name="player">The server player that left.</param>
    void OnPlayerExited(IServerPlayer player);
}
