using System;
using System.Collections.Generic;
using ArcanumLib.Common;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Spatial;

/// <summary>
/// Registers <see cref="IPlayerProximityListener" /> instances and forwards
/// enter/stay/exit events using the shared <see cref="PlayerZoneTracker" /> spatial index.
/// This avoids a full listeners × players scan by using chunk-based culling.
/// </summary>
public class PlayerProximityTracker : ModSystem
{
    /// <summary>
    /// Interval between stay checks in milliseconds. Matches the underlying
    /// <see cref="PlayerZoneTracker.TickIntervalMs" /> value by default.
    /// </summary>
    public static int TickIntervalMs => PlayerZoneTracker.TickIntervalMs;

    private readonly Dictionary<IPlayerProximityListener, string> _listenerToZoneId = new();
    private readonly object _syncLock = new();
    private ICoreServerAPI? _sapi;

    /// <inheritdoc />
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    /// <inheritdoc />
    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lock (_syncLock)
        {
            foreach (var zoneId in _listenerToZoneId.Values)
                PlayerZoneTracker.Unregister(zoneId);

            _listenerToZoneId.Clear();
        }

        _sapi = null;
        base.Dispose();
    }

    /// <summary>
    /// Registers a listener. Safe to call from block entity <c>Initialize</c>.
    /// If the listener was already registered, this is a no-op.
    /// </summary>
    /// <param name="listener">The listener to register.</param>
    public void Register(IPlayerProximityListener? listener)
    {
        if (listener == null) return;

        lock (_syncLock)
        {
            if (_listenerToZoneId.ContainsKey(listener)) return;

            string zoneId = Guid.NewGuid().ToString("N");
            _listenerToZoneId[listener] = zoneId;

            var center = listener.Position?.ToVec3d()?.Add(0.5, 0.5, 0.5);
            if (center == null) return;

            var shape = new SphereZoneShape
            {
                Center = center,
                Radius = listener.Radius,
                Dimension = listener.Position?.dimension ?? 0
            };

            PlayerZoneTracker.Register(
                zoneId,
                shape,
                null,
                player => SafeInvoke(listener, player, listener.OnPlayerEntered),
                player => SafeInvoke(listener, player, listener.OnPlayerStayed),
                player => SafeInvoke(listener, player, listener.OnPlayerExited));
        }
    }

    /// <summary>
    /// Unregisters a listener. Safe to call from block entity <c>OnBlockRemoved</c>.
    /// </summary>
    /// <param name="listener">The listener to unregister.</param>
    public void Unregister(IPlayerProximityListener? listener)
    {
        if (listener == null) return;

        lock (_syncLock)
        {
            if (!_listenerToZoneId.Remove(listener, out var zoneId)) return;
            PlayerZoneTracker.Unregister(zoneId);
        }
    }

    private void SafeInvoke(IPlayerProximityListener listener, IServerPlayer player, Action<IServerPlayer>? callback)
    {
        if (callback == null) return;
        try
        {
            callback(player);
        }
        catch (Exception ex)
        {
            _sapi?.Logger?.Warning("[PlayerProximityTracker] Listener at {0} failed: {1}", listener.Position, ex.Message);
        }
    }
}
