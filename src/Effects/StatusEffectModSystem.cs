using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Effects;

/// <summary>
/// ModSystem that ticks the <see cref="StatusEffectService"/> each game tick.
/// </summary>
public class StatusEffectModSystem : ModSystem
{
    private long _clientListenerId;
    private long _serverListenerId;
    private ICoreClientAPI? _capi;
    private ICoreServerAPI? _sapi;
    private readonly List<IEventAPI> _despawnEvents = new();
    private readonly EntityDespawnDelegate _despawnHandler;

    /// <summary>
    /// Initializes the entity despawn handler used to remove active status effects.
    /// </summary>
    public StatusEffectModSystem()
    {
        _despawnHandler = (entity, _) => StatusEffectManager.RemoveAll(entity);
    }

    private static void EnsureService()
    {
        if (ArcanumServices.Get<StatusEffectService>() == null)
        {
            ArcanumServices.Register(new StatusEffectService());
        }
    }

    /// <summary>
    /// Registers the tick listener on the client.
    /// </summary>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        _capi = capi;
        EnsureService();
        _clientListenerId = capi.Event.RegisterGameTickListener(dt => StatusEffectManager.Tick(dt), 1000);
        capi.Event.OnEntityDespawn += _despawnHandler;
        _despawnEvents.Add(capi.Event);
    }

    /// <summary>
    /// Registers the tick listener on the server.
    /// </summary>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        EnsureService();
        _serverListenerId = sapi.Event.RegisterGameTickListener(dt => StatusEffectManager.Tick(dt), 1000);
        sapi.Event.OnEntityDespawn += _despawnHandler;
        _despawnEvents.Add(sapi.Event);
    }

    /// <summary>
    /// Unregisters the tick listeners and despawn handlers.
    /// </summary>
    public override void Dispose()
    {
        if (_clientListenerId != 0 && _capi != null)
        {
            try { _capi.Event.UnregisterGameTickListener(_clientListenerId); }
            catch (Exception ex) { _capi?.Logger?.Warning("[ArcanumLib] Failed to unregister client status effect tick listener: {0}", ex.Message); }
            _clientListenerId = 0;
        }

        if (_serverListenerId != 0 && _sapi != null)
        {
            try { _sapi.Event.UnregisterGameTickListener(_serverListenerId); }
            catch (Exception ex) { _sapi?.Logger?.Warning("[ArcanumLib] Failed to unregister server status effect tick listener: {0}", ex.Message); }
            _serverListenerId = 0;
        }

        ICoreAPI? api = (ICoreAPI?)_capi ?? _sapi;
        foreach (var events in _despawnEvents)
        {
            try { events.OnEntityDespawn -= _despawnHandler; }
            catch (Exception ex) { api?.Logger?.Warning("[ArcanumLib] Failed to unregister despawn handler: {0}", ex.Message); }
        }
        _despawnEvents.Clear();

        // The listener is already tied to the API lifecycle, but we clear state for tests.
        StatusEffectManager.Clear();
    }
}
