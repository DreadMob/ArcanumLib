using System;
using System.Collections.Generic;
using ArcanumLib.Common;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Spatial;

/// <summary>
/// Tracks which land claim each online player is currently standing in and
/// raises enter/exit/change events. Useful for consumers that need to react
/// to claim boundaries without polling the claim API every tick.
/// </summary>
public class PlayerInLandTracker : ModSystem
{
    private static PlayerInLandTracker? _instance;
    /// <summary>Active tracker instance, or null when not loaded.</summary>
    public static PlayerInLandTracker? Instance => _instance;

    private ICoreServerAPI? _sapi;
    private long _tickListenerId;

    private readonly Dictionary<string, string?> _currentClaims = new(StringComparer.Ordinal);

    /// <summary>
    /// Interval between internal claim checks in milliseconds. Default is 1000ms.
    /// </summary>
    public static int TickIntervalMs { get; set; } = 1000;

    /// <summary>Raised when a player enters a land claim from outside.</summary>
    public static event Action<string, string>? PlayerClaimEntered;

    /// <summary>Raised when a player leaves a land claim for outside.</summary>
    public static event Action<string, string>? PlayerClaimExited;

    /// <summary>Raised whenever the player changes from one claim to another, including entering null.</summary>
    public static event Action<string, string?, string?>? PlayerClaimChanged;

    /// <summary>Loads only on the server side, where land claims are authoritative.</summary>
    /// <param name="forSide">The application side being tested.</param>
    /// <returns><c>true</c> if the side is server; otherwise <c>false</c>.</returns>
    /// <inheritdoc />
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    /// <summary>Initializes the tracker, registers the tick listener, and subscribes to player join/leave events.</summary>
    /// <param name="api">The server API.</param>
    /// <inheritdoc />
    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        _instance = this;
        _tickListenerId = api.Event.RegisterGameTickListener(OnTick, TickIntervalMs);
        api.Event.PlayerJoin += OnPlayerJoin;
        api.Event.PlayerLeave += OnPlayerLeave;
    }

    /// <summary>Releases all resources used by the current object.</summary>
    /// <inheritdoc />
    public override void Dispose()
    {
        if (_sapi != null && _tickListenerId != 0)
        {
            _sapi.Event.UnregisterGameTickListener(_tickListenerId);
            _sapi.Event.PlayerJoin -= OnPlayerJoin;
            _sapi.Event.PlayerLeave -= OnPlayerLeave;
            _tickListenerId = 0;
        }

        _currentClaims.Clear();
        _instance = null;
        _sapi = null;
        base.Dispose();
    }

    /// <summary>
    /// Returns the current claim name for the given player, or null if the
    /// player is not inside any claim or is offline. The value is cached and
    /// refreshed by the tracker tick.
    /// </summary>
    /// <param name="playerUid">The unique player identifier.</param>
    /// <returns>The player claim, or null if none is found.</returns>
    public static string? GetPlayerClaim(string playerUid)
    {
        if (_instance == null) return null;
        if (_instance._currentClaims.TryGetValue(playerUid, out var claim)) return claim;

        var player = _instance._sapi?.World.PlayerByUid(playerUid) as IServerPlayer;
        if (player == null) return null;

        return GetClaimName(player);
    }

    /// <summary>
    /// Returns true if the player is currently inside the named claim.
    /// </summary>
    /// <param name="playerUid">The unique player identifier.</param>
    /// <param name="claimName">The claim name value.</param>
    /// <returns>true if player in claim; otherwise, false.</returns>
    public static bool IsPlayerInClaim(string playerUid, string? claimName)
    {
        if (_instance == null || string.IsNullOrWhiteSpace(claimName)) return false;
        var current = GetPlayerClaim(playerUid);
        return !string.IsNullOrWhiteSpace(current) &&
               string.Equals(current, claimName, StringComparison.OrdinalIgnoreCase);
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        _currentClaims[player.PlayerUID] = GetClaimName(player);
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        _currentClaims.Remove(player.PlayerUID);
    }

    private void OnTick(float dt)
    {
        if (_sapi == null) return;

        var cache = ArcanumServices.Get<IOnlinePlayerCache>();
        IEnumerable<IPlayer> players = cache?.IsLoaded == true
            ? (IEnumerable<IPlayer>)cache.All
            : _sapi.World.AllOnlinePlayers;

        foreach (var p in players)
        {
            var sp = p as IServerPlayer;
            if (sp?.Entity?.Pos == null) continue;

            string uid = sp.PlayerUID;
            string? newClaim = GetClaimName(sp);

            bool hasLast = _currentClaims.TryGetValue(uid, out var lastClaim);
            _currentClaims[uid] = newClaim;

            if (!hasLast)
                continue;

            if (string.Equals(newClaim, lastClaim, StringComparison.Ordinal))
                continue;

            if (!string.IsNullOrWhiteSpace(lastClaim))
            {
                PlayerClaimExited?.Invoke(uid, lastClaim);
            }

            if (!string.IsNullOrWhiteSpace(newClaim))
            {
                PlayerClaimEntered?.Invoke(uid, newClaim);
            }

            PlayerClaimChanged?.Invoke(uid, lastClaim, newClaim);
        }
    }

    private static string? GetClaimName(IServerPlayer player)
    {
        if (player?.Entity?.World?.Claims == null) return null;

        var pos = player.Entity.Pos.AsBlockPos;
        var claims = player.Entity.World.Claims.Get(pos);
        if (claims == null || claims.Length == 0) return null;

        for (int i = 0; i < claims.Length; i++)
        {
            var c = claims[i];
            if (c == null) continue;

            if (!string.IsNullOrWhiteSpace(c.Description))
                return c.Description;

            if (!string.IsNullOrWhiteSpace(c.LastKnownOwnerName))
                return c.LastKnownOwnerName;
        }

        return null;
    }
}
