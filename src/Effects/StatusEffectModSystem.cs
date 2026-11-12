using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Effects;

/// <summary>
/// ModSystem that owns and ticks one <see cref="StatusEffectService" /> per side, registered in that side's
/// <see cref="ArcanumServiceScope" /> (in singleplayer the client and the server keep their own effects).
/// </summary>
public class StatusEffectModSystem : ModSystem
{
    private long _clientListenerId;
    private long _serverListenerId;
    private ICoreClientAPI? _capi;
    private ICoreServerAPI? _sapi;
    private readonly List<IEventAPI> _despawnEvents = new();
    private EntityDespawnDelegate? _despawnHandler;
    private IStatusEffectService? _service;
    private ArcanumServiceScope _scope = ArcanumServiceScope.Global;

    /// <summary>
    /// Registers the tick listener on the client.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    public override void StartClientSide(ICoreClientAPI capi)
    {
        _capi = capi;
        _service = EnsureService(_scope = ArcanumServiceScope.Client);
        _despawnHandler = (entity, _) => _service?.RemoveAll(entity);
        _clientListenerId = capi.Event.RegisterGameTickListener(dt => _service?.Tick(dt), 1000);
        capi.Event.OnEntityDespawn += _despawnHandler;
        _despawnEvents.Add(capi.Event);
    }

    /// <summary>
    /// Registers the tick listener on the server.
    /// </summary>
    /// <param name="sapi">The server API instance.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        _service = EnsureService(_scope = ArcanumServiceScope.Server);
        _despawnHandler = (entity, _) => _service?.RemoveAll(entity);
        _serverListenerId = sapi.Event.RegisterGameTickListener(dt => _service?.Tick(dt), 1000);
        sapi.Event.OnEntityDespawn += _despawnHandler;
        _despawnEvents.Add(sapi.Event);
    }

    private static IStatusEffectService EnsureService(ArcanumServiceScope scope)
    {
        var service = ArcanumServices.Get<IStatusEffectService>(scope);
        if (service == null)
        {
            var concrete = new StatusEffectService();
            ArcanumServices.Register(concrete, scope);
            ArcanumServices.Register<IStatusEffectService>(concrete, scope);
            service = concrete;
        }
        return service;
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
            try { if (_despawnHandler != null) events.OnEntityDespawn -= _despawnHandler; }
            catch (Exception ex) { api?.Logger?.Warning("[ArcanumLib] Failed to unregister despawn handler: {0}", ex.Message); }
        }
        _despawnEvents.Clear();

        _service?.Clear();
        ArcanumServices.Unregister<StatusEffectService>(_scope);
        ArcanumServices.Unregister<IStatusEffectService>(_scope);
    }
}
