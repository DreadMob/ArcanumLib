using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// ModSystem that clears the <see cref="ActionRegistry"/> and <see cref="ActionExecutor"/>
/// static state when the world is unloaded. Also hooks player disconnect to clear
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
    }

    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
            _sapi = null;
        }

        ActionRegistry.Clear();
        ActionExecutor.ClearAllCooldowns();
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        if (player?.Entity?.EntityId != null)
        {
            ActionExecutor.ClearCooldowns(player.Entity.EntityId);
        }
    }
}
