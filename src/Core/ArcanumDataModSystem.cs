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
/// and the action registry/executor services. <see cref="IPityTracker" /> is published
/// by the consuming mod rather than here, to avoid save-key collisions.
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

        if (ArcanumServices.Get<IPlaytimeTracker>(ArcanumServiceScope.Server) == null)
        {
            var playtime = new PlaytimeTracker(sapi);
            ArcanumServices.Register(playtime, ArcanumServiceScope.Server);
            ArcanumServices.Register<IPlaytimeTracker>(playtime, ArcanumServiceScope.Server);
        }

        var registry = new ActionRegistryService();
        ArcanumServices.Register(registry, ArcanumServiceScope.Server);
        ArcanumServices.Register<IActionRegistryService>(registry, ArcanumServiceScope.Server);

        var executor = new ActionExecutorService(sapi, registry);
        ArcanumServices.Register(executor, ArcanumServiceScope.Server);
        ArcanumServices.Register<IActionExecutorService>(executor, ArcanumServiceScope.Server);

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

        if (ArcanumServices.Get<IPlaytimeTracker>(ArcanumServiceScope.Server) is { } playtime)
            playtime.Dispose();
        ArcanumServices.Unregister<PlaytimeTracker>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<IPlaytimeTracker>(ArcanumServiceScope.Server);

        if (ArcanumServices.Get<IActionExecutorService>() is { } executor)
            executor.ClearAllCooldowns();

        if (ArcanumServices.Get<IActionRegistryService>() is { } registry)
            registry.Clear();

        ArcanumServices.Unregister<ActionExecutorService>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<IActionExecutorService>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<ActionRegistryService>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<IActionRegistryService>(ArcanumServiceScope.Server);
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
            ArcanumServices.Get<IActionExecutorService>()?.ClearCooldowns(player.Entity.EntityId);
        }
    }
}
