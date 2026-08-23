using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// ModSystem that registers <see cref="ActionRegistryService"/> and <see cref="ActionExecutorService"/>
/// and unregisters/clears them on world unload. Also hooks player disconnect to clear
/// per-player cooldowns.
/// </summary>
public class ActionRegistryModSystem : ModSystem
{
    private ICoreServerAPI? _sapi;

    /// <summary>
    /// Server-side only: the registry and cooldowns are server-authoritative.
    /// </summary>
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        sapi.Event.PlayerDisconnect += OnPlayerDisconnect;

        ArcanumServices.Register(new ActionRegistryService());
        ArcanumServices.Register(new ActionExecutorService(sapi));
    }

    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
            _sapi = null;
        }

        if (ArcanumServices.Get<ActionExecutorService>() is { } executor)
            executor.ClearAllCooldowns();

        if (ArcanumServices.Get<ActionRegistryService>() is { } registry)
            registry.Clear();

        ArcanumServices.Unregister<ActionExecutorService>();
        ArcanumServices.Unregister<ActionRegistryService>();
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        if (player?.Entity?.EntityId != null)
        {
            ArcanumServices.Get<ActionExecutorService>()?.ClearCooldowns(player.Entity.EntityId);
        }
    }
}
