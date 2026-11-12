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
/// The 1 s tick listener runs only while some entity has an effect: the first <c>Apply</c> starts it and the
/// tick that leaves the service empty stops it. A replacement <see cref="IStatusEffectService" /> registered by
/// a mod (not a <see cref="StatusEffectService" />) cannot report that, so it is ticked all the time.
/// </summary>
public class StatusEffectModSystem : ModSystem
{
    private long _listenerId;
    private IEventAPI? _events;
    private readonly object _tickGate = new();
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
        StartTicking(capi.Event);
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
        StartTicking(sapi.Event);
        sapi.Event.OnEntityDespawn += _despawnHandler;
        _despawnEvents.Add(sapi.Event);
    }

    /// <summary>True while the tick listener is registered (tests).</summary>
    internal bool IsTicking { get { lock (_tickGate) return _listenerId != 0; } }

    private void StartTicking(IEventAPI events)
    {
        _events = events;
        if (_service is StatusEffectService lazy)
        {
            lazy.EffectApplied += OnEffectApplied;
            if (lazy.HasActive) StartTick();
        }
        else StartTick();
    }

    private void OnEffectApplied() => StartTick();

    private void StartTick()
    {
        lock (_tickGate)
        {
            if (_listenerId != 0 || _events == null) return;
            _listenerId = _events.RegisterGameTickListener(OnTick, 1000);
        }
    }

    private void OnTick(float dt)
    {
        _service?.Tick(dt);
        if (_service is StatusEffectService { HasActive: false }) StopTick();
    }

    private void StopTick()
    {
        lock (_tickGate)
        {
            if (_listenerId == 0 || _events == null) return;
            try { _events.UnregisterGameTickListener(_listenerId); }
            catch (Exception ex) { (((ICoreAPI?)_capi) ?? _sapi)?.Logger?.Warning("[ArcanumLib] Failed to unregister status effect tick listener: {0}", ex.Message); }
            _listenerId = 0;
        }
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
        if (_service is StatusEffectService lazy) lazy.EffectApplied -= OnEffectApplied;
        StopTick();
        _events = null;

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
