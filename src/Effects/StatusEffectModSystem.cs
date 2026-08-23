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
    private long _listenerId;
    private ICoreAPI? _api;
    private readonly List<IEventAPI> _despawnEvents = new();
    private readonly EntityDespawnDelegate _despawnHandler;

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
        _api = capi;
        EnsureService();
        _listenerId = capi.Event.RegisterGameTickListener(dt => StatusEffectManager.Tick(dt), 1000);
        capi.Event.OnEntityDespawn += _despawnHandler;
        _despawnEvents.Add(capi.Event);
    }

    /// <summary>
    /// Registers the tick listener on the server.
    /// </summary>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _api = sapi;
        EnsureService();
        _listenerId = sapi.Event.RegisterGameTickListener(dt => StatusEffectManager.Tick(dt), 1000);
        sapi.Event.OnEntityDespawn += _despawnHandler;
        _despawnEvents.Add(sapi.Event);
    }

    /// <summary>
    /// Unregisters the tick listener and despawn handler.
    /// </summary>
    public override void Dispose()
    {
        foreach (var events in _despawnEvents)
        {
            try { events.OnEntityDespawn -= _despawnHandler; }
            catch (Exception ex) { _api?.Logger?.Warning("[ArcanumLib] Failed to unregister despawn handler: {0}", ex.Message); }
        }
        _despawnEvents.Clear();

        // The listener is already tied to the API lifecycle, but we clear state for tests.
        StatusEffectManager.Clear();
    }
}
