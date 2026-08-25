using System;
using ArcanumLib.Actions;
using ArcanumLib.Common;
using ArcanumLib.Persistence;
using ArcanumLib.Progression;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Core;

/// <summary>
/// Consolidated server-side data lifecycle ModSystem.
/// Initializes and disposes <see cref="ModDataStore" />, <see cref="PlaytimeTracker" />,
/// <see cref="PityTracker" /> and the action registry/executor services.
/// </summary>
public class ArcanumDataModSystem : ModSystem
{
    private ICoreServerAPI? _sapi;

    /// <summary>
    /// Determines whether this system should load on the given side.
    /// </summary>
    /// <param name="forSide">The application side.</param>
    /// <returns><c>true</c> if the side is server; otherwise <c>false</c>.</returns>
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    /// <summary>
    /// Starts all data-related subsystems on the server.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        if (sapi == null) throw new ArgumentNullException(nameof(sapi));

        _sapi = sapi;

        ArcanumServices.Register<ICoreServerAPI>(sapi, ArcanumServiceScope.Server);
        sapi.Event.SaveGameLoaded += OnSaveGameLoaded;
        sapi.Event.SaveGameCreated += OnSaveGameCreated;
        sapi.Event.GameWorldSave += OnGameWorldSave;

        PlaytimeTracker.Current ??= new PlaytimeTracker(sapi);
        PityTracker.Current ??= new PityTracker(sapi);

        ArcanumServices.Register(new ActionRegistryService(), ArcanumServiceScope.Server);
        ArcanumServices.Register(new ActionExecutorService(sapi), ArcanumServiceScope.Server);

        sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
    }

    /// <summary>
    /// Disposes all data-related subsystems and detaches savegame event handlers.
    /// </summary>
    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
            _sapi.Event.SaveGameLoaded -= OnSaveGameLoaded;
            _sapi.Event.SaveGameCreated -= OnSaveGameCreated;
            _sapi.Event.GameWorldSave -= OnGameWorldSave;
        }

        PlaytimeTracker.Current?.Dispose();
        PlaytimeTracker.Current = null;

        PityTracker.Current?.Save();
        PityTracker.Current = null;

        if (ArcanumServices.Get<ActionExecutorService>() is { } executor)
            executor.ClearAllCooldowns();

        if (ArcanumServices.Get<ActionRegistryService>() is { } registry)
            registry.Clear();

        ArcanumServices.Unregister<ActionExecutorService>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<ActionRegistryService>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<ICoreServerAPI>(ArcanumServiceScope.Server);

        ModDataStore.Clear();

        _sapi = null;
        base.Dispose();
    }

    private void OnSaveGameLoaded()
    {
        ModDataStore.LoadAll();
    }

    private void OnSaveGameCreated()
    {
        ModDataStore.LoadAll();
    }

    private void OnGameWorldSave()
    {
        ModDataStore.SaveAll();
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        if (player?.Entity?.EntityId != null)
        {
            ArcanumServices.Get<ActionExecutorService>()?.ClearCooldowns(player.Entity.EntityId);
        }
    }
}
