using System;
using System.Collections.Generic;
using ArcanumLib.Common;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Spatial;

/// <summary>
/// Defines a 3D zone shape in a specific dimension for use with <see cref="PlayerZoneTracker" />.
/// </summary>
public interface IZoneShape
{
    /// <summary>The dimension in which the zone exists.</summary>
    int Dimension { get; }

    /// <summary>Returns true if the point lies inside the zone.</summary>
    /// <param name="point">The three-dimensional vector.</param>
    /// <returns>true if the specified point is contained; otherwise, false.</returns>
    bool Contains(Vec3d? point);

    /// <summary>
    /// Distance from the point to the zone boundary. Negative or zero if inside.
    /// Positive values can be used for LOD or culling.
    /// </summary>
    /// <param name="point">The three-dimensional vector.</param>
    /// <returns>The distance to.</returns>
    double DistanceTo(Vec3d? point);

    /// <summary>
    /// Enumerates chunk coordinates that the zone's bounding box overlaps.
    /// </summary>
    /// <param name="chunkSize">Chunk edge length, usually <see cref="GlobalConstants.ChunkSize" />.</param>
    /// <returns>A collection of chunk keys values.</returns>
    IEnumerable<BlockPos> GetChunkKeys(int chunkSize);
}

/// <summary>
/// Spherical zone shape centered at a world position.
/// </summary>
public readonly record struct SphereZoneShape : IZoneShape
{
    /// <summary>World-space center of the sphere.</summary>
    public Vec3d Center { get; init; }

    /// <summary>Gets or sets the dimension.</summary>
    /// <inheritdoc />
    public int Dimension { get; init; }

    /// <summary>Radius in blocks.</summary>
    public double Radius { get; init; }

    /// <summary>Returns whether <paramref name="point" /> lies inside the zone.</summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c> if the point is inside the zone; otherwise <c>false</c>.</returns>
    /// <inheritdoc />
    public bool Contains(Vec3d? point)
    {
        if (point == null) return false;
        double dx = point.X - Center.X;
        double dy = point.Y - Center.Y;
        double dz = point.Z - Center.Z;
        return dx * dx + dy * dy + dz * dz <= Radius * Radius;
    }

    /// <summary>Returns the Euclidean distance from the sphere surface to <paramref name="point" />, or <see cref="double.MaxValue" /> when null.</summary>
    /// <param name="point">The point to measure.</param>
    /// <returns>The signed distance from the sphere surface, or <see cref="double.MaxValue" /> when <paramref name="point" /> is null.</returns>
    /// <inheritdoc />
    public double DistanceTo(Vec3d? point)
    {
        if (point == null) return double.MaxValue;
        double dx = point.X - Center.X;
        double dy = point.Y - Center.Y;
        double dz = point.Z - Center.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz) - Radius;
    }

    /// <summary>Gets chunk keys.</summary>
    /// <param name="chunkSize">The chunk edge length.</param>
    /// <returns>A collection of chunk keys values.</returns>
    /// <inheritdoc />
    public IEnumerable<BlockPos> GetChunkKeys(int chunkSize)
    {
        int minCx = Div(Center.X - Radius, chunkSize);
        int maxCx = Div(Center.X + Radius, chunkSize);
        int minCy = Div(Center.Y - Radius, chunkSize);
        int maxCy = Div(Center.Y + Radius, chunkSize);
        int minCz = Div(Center.Z - Radius, chunkSize);
        int maxCz = Div(Center.Z + Radius, chunkSize);

        for (int x = minCx; x <= maxCx; x++)
            for (int y = minCy; y <= maxCy; y++)
                for (int z = minCz; z <= maxCz; z++)
                    yield return new BlockPos(x, y, z, Dimension);

        static int Div(double value, int size) => (int)Math.Floor(value / size);
    }
}

/// <summary>
/// Cuboid zone defined by minimum and maximum world corners.
/// </summary>
public readonly record struct BoxZoneShape : IZoneShape
{
    /// <summary>Minimum corner of the box.</summary>
    public Vec3d Min { get; init; }

    /// <summary>Maximum corner of the box.</summary>
    public Vec3d Max { get; init; }

    /// <summary>Gets or sets the dimension.</summary>
    /// <inheritdoc />
    public int Dimension { get; init; }

    /// <summary>Returns whether <paramref name="point" /> lies inside the zone.</summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c> if the point is inside the zone; otherwise <c>false</c>.</returns>
    /// <inheritdoc />
    public bool Contains(Vec3d? point)
    {
        if (point == null) return false;
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }

    /// <summary>Returns the Euclidean distance from the box surface to <paramref name="point" />, or <see cref="double.MaxValue" /> when null.</summary>
    /// <param name="point">The point to measure.</param>
    /// <returns>The distance to the nearest box face, or <see cref="double.MaxValue" /> when <paramref name="point" /> is null.</returns>
    /// <inheritdoc />
    public double DistanceTo(Vec3d? point)
    {
        if (point == null) return double.MaxValue;
        double dx = point.X < Min.X ? Min.X - point.X : (point.X > Max.X ? point.X - Max.X : 0);
        double dy = point.Y < Min.Y ? Min.Y - point.Y : (point.Y > Max.Y ? point.Y - Max.Y : 0);
        double dz = point.Z < Min.Z ? Min.Z - point.Z : (point.Z > Max.Z ? point.Z - Max.Z : 0);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Gets chunk keys.</summary>
    /// <param name="chunkSize">The chunk edge length.</param>
    /// <returns>A collection of chunk keys values.</returns>
    /// <inheritdoc />
    public IEnumerable<BlockPos> GetChunkKeys(int chunkSize)
    {
        int minCx = Div(Min.X, chunkSize);
        int maxCx = Div(Max.X, chunkSize);
        int minCy = Div(Min.Y, chunkSize);
        int maxCy = Div(Max.Y, chunkSize);
        int minCz = Div(Min.Z, chunkSize);
        int maxCz = Div(Max.Z, chunkSize);

        for (int x = minCx; x <= maxCx; x++)
            for (int y = minCy; y <= maxCy; y++)
                for (int z = minCz; z <= maxCz; z++)
                    yield return new BlockPos(x, y, z, Dimension);

        static int Div(double value, int size) => (int)Math.Floor(value / size);
    }
}

/// <summary>
/// Server-side tracker for player presence in 3D zones. Other mods can register
/// zones and receive enter/exit callbacks without per-mod tick listeners.
/// </summary>
public class PlayerZoneTracker : ModSystem
{
    private static PlayerZoneTracker? _instance;
    /// <summary>Active tracker instance, or null when not loaded.</summary>
    public static PlayerZoneTracker? Instance => _instance;

    private ICoreServerAPI? _sapi;
    private long _tickListenerId;

    private readonly Dictionary<string, TrackedZone> _zones = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<BlockPos, HashSet<string>> _chunkIndex = new();
    private readonly Dictionary<string, HashSet<string>> _playersInZone = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _lastZonesByPlayer = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Interval between internal updates in milliseconds. Lower values are more
    /// responsive but more expensive. Default is 1000ms.
    /// </summary>
    public static int TickIntervalMs { get; set; } = 1000;

    /// <summary>Raised when a player enters any tracked zone.</summary>
    public static event Action<string, IServerPlayer>? PlayerEntered;

    /// <summary>Raised when a player leaves any tracked zone.</summary>
    public static event Action<string, IServerPlayer>? PlayerExited;

    /// <summary>Loads only on the server side, where player positions are authoritative.</summary>
    /// <param name="forSide">The application side being tested.</param>
    /// <returns><c>true</c> if the side is server; otherwise <c>false</c>.</returns>
    /// <inheritdoc />
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    /// <summary>Initializes the tracker, registers the tick listener, and subscribes to player leave events.</summary>
    /// <param name="api">The server API.</param>
    /// <inheritdoc />
    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        _instance = this;
        _tickListenerId = api.Event.RegisterGameTickListener(OnTick, TickIntervalMs);
        api.Event.PlayerLeave += OnPlayerLeave;
    }

    /// <summary>Releases all resources used by the current object.</summary>
    /// <inheritdoc />
    public override void Dispose()
    {
        if (_sapi != null && _tickListenerId != 0)
        {
            _sapi.Event.UnregisterGameTickListener(_tickListenerId);
            _sapi.Event.PlayerLeave -= OnPlayerLeave;
            _tickListenerId = 0;
        }

        var keys = new List<string>(_zones.Keys);
        foreach (var zoneId in keys)
            _Unregister(zoneId);

        _playersInZone.Clear();
        _lastZonesByPlayer.Clear();
        _instance = null;
        _sapi = null;
        base.Dispose();
    }

    /// <summary>
    /// Registers a new zone. If <paramref name="zoneId" /> already exists, it is replaced.
    /// </summary>
    /// <param name="zoneId">Unique zone identifier.</param>
    /// <param name="shape">Zone geometry and dimension.</param>
    /// <param name="data">Optional arbitrary data for the consumer.</param>
    /// <param name="onEnter">Optional callback when a player enters.</param>
    /// <param name="onStayed">Optional callback called each tick while a player remains inside.</param>
    /// <param name="onExit">Optional callback when a player leaves.</param>
    public static void Register(string zoneId, IZoneShape shape, object? data = null, Action<IServerPlayer>? onEnter = null, Action<IServerPlayer>? onStayed = null, Action<IServerPlayer>? onExit = null)
    {
        if (_instance == null) return;
        _instance.RegisterZone(zoneId, shape, data, onEnter, onStayed, onExit);
    }

    /// <summary>
    /// Removes a zone and clears all tracked players for it.
    /// </summary>
    /// <param name="zoneId">The unique zone identifier.</param>
    public static void Unregister(string zoneId)
    {
        _instance?._Unregister(zoneId);
    }

    /// <summary>
    /// Returns true if the player is currently inside the given zone.
    /// </summary>
    /// <param name="playerUid">The unique player identifier.</param>
    /// <param name="zoneId">The unique zone identifier.</param>
    /// <returns>true if player in zone; otherwise, false.</returns>
    public static bool IsPlayerInZone(string playerUid, string zoneId)
    {
        if (_instance == null) return false;
        return _instance._playersInZone.TryGetValue(zoneId, out var uids) && uids.Contains(playerUid);
    }

    /// <summary>
    /// Returns the players currently inside a zone.
    /// </summary>
    /// <param name="zoneId">The unique zone identifier.</param>
    /// <returns>A collection of players in zone values.</returns>
    public static IReadOnlyList<IServerPlayer> GetPlayersInZone(string zoneId)
    {
        var result = new List<IServerPlayer>();
        if (_instance == null) return result;

        if (!_instance._playersInZone.TryGetValue(zoneId, out var uids) || uids.Count == 0)
            return result;

        foreach (var uid in uids)
        {
            var player = _instance._sapi?.World.PlayerByUid(uid) as IServerPlayer;
            if (player != null) result.Add(player);
        }

        return result;
    }

    /// <summary>
    /// Returns the zone IDs the player is currently inside.
    /// </summary>
    /// <param name="playerUid">The unique player identifier.</param>
    /// <returns>A collection of zones for player values.</returns>
    public static IReadOnlyList<string> GetZonesForPlayer(string playerUid)
    {
        var result = new List<string>();
        if (_instance == null) return result;
        if (_instance._lastZonesByPlayer.TryGetValue(playerUid, out var zones))
            result.AddRange(zones);
        return result;
    }

    /// <summary>
    /// Manually rechecks all players immediately. Useful for consumers that need
    /// up-to-date state between scheduled ticks.
    /// </summary>
    public static void ForceUpdate()
    {
        _instance?.OnTick(0);
    }

    private void RegisterZone(string zoneId, IZoneShape shape, object? data, Action<IServerPlayer>? onEnter, Action<IServerPlayer>? onStayed, Action<IServerPlayer>? onExit)
    {
        _Unregister(zoneId);

        var zone = new TrackedZone
        {
            Shape = shape,
            Data = data,
            OnEnter = onEnter,
            OnStayed = onStayed,
            OnExit = onExit
        };

        _zones[zoneId] = zone;

        foreach (var chunk in shape.GetChunkKeys(GlobalConstants.ChunkSize))
        {
            if (!_chunkIndex.TryGetValue(chunk, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _chunkIndex[chunk] = set;
            }
            set.Add(zoneId);
        }
    }

    private void _Unregister(string zoneId)
    {
        if (!_zones.Remove(zoneId, out var zone)) return;

        var toRemove = new List<BlockPos>();
        foreach (var kvp in _chunkIndex)
        {
            kvp.Value.Remove(zoneId);
            if (kvp.Value.Count == 0) toRemove.Add(kvp.Key);
        }
        foreach (var chunk in toRemove)
            _chunkIndex.Remove(chunk);

        if (_playersInZone.TryGetValue(zoneId, out var uids))
        {
            foreach (var uid in uids)
            {
                if (_lastZonesByPlayer.TryGetValue(uid, out var playerZones))
                    playerZones.Remove(zoneId);

                var player = _sapi?.World.PlayerByUid(uid) as IServerPlayer;
                if (player != null)
                {
                    zone.OnExit?.Invoke(player);
                    PlayerExited?.Invoke(zoneId, player);
                }
            }
            _playersInZone.Remove(zoneId);
        }
    }

    private void OnTick(float dt)
    {
        if (_sapi == null) return;

        var cache = ArcanumServices.Get<OnlinePlayerCache>();
        IEnumerable<IPlayer> players = cache?.IsLoaded == true
            ? (IEnumerable<IPlayer>)cache.All
            : _sapi.World.AllOnlinePlayers;

        foreach (var p in players)
        {
            var sp = p as IServerPlayer;
            if (sp?.Entity?.Pos == null) continue;

            var pos = sp.Entity.Pos.XYZ;
            int dim = sp.Entity.Pos.Dimension;
            int cs = GlobalConstants.ChunkSize;
            var chunk = new BlockPos(
                (int)Math.Floor(pos.X / cs),
                (int)Math.Floor(pos.Y / cs),
                (int)Math.Floor(pos.Z / cs),
                dim);

            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_chunkIndex.TryGetValue(chunk, out var candidates))
            {
                foreach (var zoneId in candidates)
                {
                    if (!_zones.TryGetValue(zoneId, out var zone)) continue;
                    if (zone.Shape.Contains(pos))
                        current.Add(zoneId);
                }
            }

            UpdatePlayer(sp, current);
        }
    }

    private void UpdatePlayer(IServerPlayer player, HashSet<string> current)
    {
        string uid = player.PlayerUID;
        _lastZonesByPlayer.TryGetValue(uid, out var last);
        last ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var zoneId in current)
        {
            if (!_zones.TryGetValue(zoneId, out var zone)) continue;

            if (last.Contains(zoneId))
            {
                zone.OnStayed?.Invoke(player);
                continue;
            }

            if (!_playersInZone.TryGetValue(zoneId, out var uids))
            {
                uids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _playersInZone[zoneId] = uids;
            }
            uids.Add(uid);

            zone.OnEnter?.Invoke(player);
            PlayerEntered?.Invoke(zoneId, player);
        }

        foreach (var zoneId in last)
        {
            if (current.Contains(zoneId)) continue;
            if (!_zones.TryGetValue(zoneId, out var zone)) continue;

            if (_playersInZone.TryGetValue(zoneId, out var uids))
                uids.Remove(uid);

            zone.OnExit?.Invoke(player);
            PlayerExited?.Invoke(zoneId, player);
        }

        foreach (var zoneId in current)
        {
            if (!last.Contains(zoneId)) continue;
            if (!_zones.TryGetValue(zoneId, out var zone)) continue;
            zone.OnStayed?.Invoke(player);
        }

        if (current.Count > 0)
            _lastZonesByPlayer[uid] = current;
        else
            _lastZonesByPlayer.Remove(uid);
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        if (!_lastZonesByPlayer.Remove(player.PlayerUID, out var last)) return;

        foreach (var zoneId in last)
        {
            if (_playersInZone.TryGetValue(zoneId, out var uids))
                uids.Remove(player.PlayerUID);

            if (_zones.TryGetValue(zoneId, out var zone))
            {
                zone.OnExit?.Invoke(player);
                PlayerExited?.Invoke(zoneId, player);
            }
        }
    }

    private sealed class TrackedZone
    {
        public IZoneShape Shape = null!;
        public object? Data;
        public Action<IServerPlayer>? OnEnter;
        public Action<IServerPlayer>? OnStayed;
        public Action<IServerPlayer>? OnExit;
    }
}
