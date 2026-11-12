---
layout: default
title: Player trackers
nav_order: 44
---

# Player trackers

`ArcanumLib.Spatial` and `ArcanumLib.Common` — server-side `ModSystem`s that watch where online players are, so a mod reacts to enter/leave instead of scanning every player each tick. All of them load automatically on the server and tick once a second by default.

## OnlinePlayerCache

Immutable snapshots of the online players, updated on join, leave and disconnect (kicks and timeouts included). Lock-free reads.

```csharp
var online = ArcanumServices.Get<IOnlinePlayerCache>(ArcanumServiceScope.Server);
foreach (var p in online.All) { ... }
var player = online.GetByUid(uid);   // also ByUid, Count, IsLoaded
```

`TypedNetworkChannel` and `ServerBroadcaster` use it for their player lists.

## PlayerZoneTracker

Named zones (sphere or box, per dimension) with enter/stay/exit callbacks, indexed by chunk so only players near a zone are tested.

```csharp
using ArcanumLib.Spatial;

PlayerZoneTracker.Register("arena-1",
    new SphereZoneShape { Center = pos.ToVec3d(), Radius = 24, Dimension = 0 },
    data: arena,
    onEnter: p => ShowHud(p), onStayed: null, onExit: p => HideHud(p));

bool inside = PlayerZoneTracker.IsPlayerInZone(uid, "arena-1");
var players = PlayerZoneTracker.GetPlayersInZone("arena-1");
var zones   = PlayerZoneTracker.GetZonesForPlayer(uid);
PlayerZoneTracker.Unregister("arena-1");
```

- Shapes: `SphereZoneShape { Center, Radius, Dimension }`, `BoxZoneShape { Min, Max, Dimension }`, or your own `IZoneShape` (`Contains`, `DistanceTo`, `GetChunkKeys`).
- Global events `PlayerEntered` / `PlayerExited` (`zoneId`, player) fire for every zone.
- `TickIntervalMs` (default 1000) sets the update rate; `ForceUpdate()` runs one now.

## PlayerProximityTracker

The same index for objects that implement `IPlayerProximityListener` (`Position`, `Radius`, `OnPlayerEntered`, `OnPlayerStayed`, `OnPlayerExited`) — typically block entities:

```csharp
public override void Initialize(ICoreAPI api)
{
    base.Initialize(api);
    api.ModLoader.GetModSystem<PlayerProximityTracker>()?.Register(this);
}

public override void OnBlockRemoved()
{
    Api.ModLoader.GetModSystem<PlayerProximityTracker>()?.Unregister(this);
    base.OnBlockRemoved();
}
```

Registering twice is a no-op; the stay interval follows `PlayerZoneTracker.TickIntervalMs`.

## PlayerInLandTracker

Which land claim each player stands in, with events. A claim is named by its description, or the owner's name when it has none.

```csharp
PlayerInLandTracker.PlayerClaimEntered += (uid, claim) => { ... };
PlayerInLandTracker.PlayerClaimExited  += (uid, claim) => { ... };
PlayerInLandTracker.PlayerClaimChanged += (uid, from, to) => { ... };

string? claim = PlayerInLandTracker.GetPlayerClaim(uid);
bool there = PlayerInLandTracker.IsPlayerInClaim(uid, "Market square");
```

Values are cached and refreshed by the tick (`TickIntervalMs`, default 1000).
