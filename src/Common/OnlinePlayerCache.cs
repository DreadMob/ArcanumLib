using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Common;

/// <summary>
/// Interface for a fast, read-only snapshot of all currently connected server players.
/// </summary>
public interface IOnlinePlayerCache
{
    /// <summary>Returns true once the cache has been initialized server-side.</summary>
    bool IsLoaded { get; }

    /// <summary>All currently online server players.</summary>
    IReadOnlyList<IServerPlayer> All { get; }

    /// <summary>Online server players indexed by UID.</summary>
    IReadOnlyDictionary<string, IServerPlayer> ByUid { get; }

    /// <summary>Returns the online server player for the given UID, or null.</summary>
    IServerPlayer? GetByUid(string playerUid);

    /// <summary>Current number of online server players.</summary>
    int Count { get; }
}

/// <summary>
/// Maintains a fast, read-only snapshot of all currently connected server players.
/// Updated via PlayerJoin/PlayerLeave events, with a periodic safety rebuild.
/// Useful for consumers that would otherwise call sapi.World.AllOnlinePlayers repeatedly
/// and cast every entry to IServerPlayer.
/// </summary>
/// <remarks>
/// The active instance is registered in <see cref="ArcanumServices" /> during
/// <see cref="StartServerSide" />. Resolve via
/// <c>ArcanumRuntime.Current.Services.Get&lt;IOnlinePlayerCache&gt;()</c>.
/// </remarks>
public class OnlinePlayerCache : ModSystem, IOnlinePlayerCache
{
    private readonly object _syncLock = new();
    private readonly List<IServerPlayer> _all = new();
    private readonly Dictionary<string, IServerPlayer> _byUid = new(StringComparer.Ordinal);

    // Immutable snapshot references. Replaced, never modified, so reads are lock-free.
    private IServerPlayer[] _allSnapshot = Array.Empty<IServerPlayer>();
    private Dictionary<string, IServerPlayer> _byUidSnapshot = new(StringComparer.Ordinal);

    private ICoreServerAPI? _sapi;
    private long _tickId;

    /// <summary>Returns true once the cache has been initialized server-side.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>All currently online server players.</summary>
    public IReadOnlyList<IServerPlayer> All => _allSnapshot;

    /// <summary>Online server players indexed by UID.</summary>
    public IReadOnlyDictionary<string, IServerPlayer> ByUid => _byUidSnapshot;

    /// <summary>Returns the online server player for the given UID, or null.</summary>
    /// <param name="playerUid">The unique player identifier.</param>
    /// <returns>The player, or null if none is found.</returns>
    public IServerPlayer? GetByUid(string playerUid)
    {
        if (string.IsNullOrWhiteSpace(playerUid)) return null;
        return _byUidSnapshot.TryGetValue(playerUid, out var player) ? player : null;
    }

    /// <summary>Current number of online server players.</summary>
    public int Count => _allSnapshot.Length;

    /// <summary>Loads only on the server side, where online players are authoritative.</summary>
    /// <param name="forSide">The application side being tested.</param>
    /// <returns><c>true</c> if the side is server; otherwise <c>false</c>.</returns>
    /// <inheritdoc />
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    /// <summary>Initializes the cache, subscribes to join/leave events, and starts the periodic rebuild listener.</summary>
    /// <param name="api">The server API.</param>
    /// <inheritdoc />
    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        IsLoaded = true;

        ArcanumServices.Register(this, ArcanumServiceScope.Server);
        ArcanumServices.Register<IOnlinePlayerCache>(this, ArcanumServiceScope.Server);

        Rebuild();

        api.Event.PlayerJoin += OnPlayerJoin;
        api.Event.PlayerLeave += OnPlayerLeave;
        api.Event.PlayerDisconnect += OnPlayerDisconnect;

        _tickId = api.Event.RegisterGameTickListener(OnTick, 5000);
    }

    /// <summary>Releases all resources used by the current object.</summary>
    /// <inheritdoc />
    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerJoin -= OnPlayerJoin;
            _sapi.Event.PlayerLeave -= OnPlayerLeave;
            _sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;

            if (_tickId != 0)
            {
                _sapi.Event.UnregisterGameTickListener(_tickId);
                _tickId = 0;
            }
        }

        _sapi = null;
        IsLoaded = false;

        lock (_syncLock)
        {
            _all.Clear();
            _byUid.Clear();
            _allSnapshot = Array.Empty<IServerPlayer>();
            _byUidSnapshot = new Dictionary<string, IServerPlayer>(StringComparer.Ordinal);
        }

        ArcanumServices.Unregister<OnlinePlayerCache>(ArcanumServiceScope.Server);
        ArcanumServices.Unregister<IOnlinePlayerCache>(ArcanumServiceScope.Server);

        base.Dispose();
    }

    private void OnTick(float dt)
    {
        Rebuild();
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        Add(player);
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        Remove(player);
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        Remove(player);
    }

    private void Add(IServerPlayer? player)
    {
        if (player == null || string.IsNullOrWhiteSpace(player.PlayerUID)) return;

        lock (_syncLock)
        {
            AddCore(player);
            PublishSnapshot();
        }
    }

    private void AddCore(IServerPlayer player)
    {
        if (_byUid.ContainsKey(player.PlayerUID))
        {
            _byUid[player.PlayerUID] = player;
            return;
        }

        _all.Add(player);
        _byUid[player.PlayerUID] = player;
    }

    private void Remove(IServerPlayer? player)
    {
        if (player == null) return;

        lock (_syncLock)
        {
            RemoveCore(player);
            PublishSnapshot();
        }
    }

    private void RemoveCore(IServerPlayer player)
    {
        if (!_byUid.Remove(player.PlayerUID)) return;

        for (int i = _all.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_all[i]?.PlayerUID, player.PlayerUID, StringComparison.Ordinal))
            {
                _all.RemoveAt(i);
                return;
            }
        }
    }

    private void Rebuild()
    {
        lock (_syncLock)
        {
            _all.Clear();
            _byUid.Clear();

            if (_sapi == null) return;

            foreach (var p in _sapi.World.AllOnlinePlayers)
            {
                if (p is IServerPlayer sp) AddCore(sp);
            }

            PublishSnapshot();
        }
    }

    private void PublishSnapshot()
    {
        _allSnapshot = _all.ToArray();
        _byUidSnapshot = new Dictionary<string, IServerPlayer>(_byUid, StringComparer.Ordinal);
    }
}
