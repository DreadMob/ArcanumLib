---
layout: default
title: Player trackers
nav_order: 44
---

# Player trackers

`ArcanumLib.Spatial` and `ArcanumLib.Common` — server-side `ModSystem`s that watch where online players are, so a mod reacts to enter/leave instead of scanning every player each tick. All of them load automatically on the server; a tracker ticks (once a second by default) only while it has something to watch, so with no users it costs nothing.

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
- `TickIntervalMs` (default 1000) sets the update rate (read when the first zone is registered); `ForceUpdate()` runs one now.
- The tick listener runs only while at least one zone is registered.

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

Registering twice is a no-op; the stay interval follows `PlayerZoneTracker.TickIntervalMs`. Each listener is a zone of `PlayerZoneTracker`, so there is no tick of its own and none at all without listeners.

## PlayerInLandTracker

Which land claim each player stands in, with events. A claim is named by its description, or the owner's name when it has none.

```csharp
PlayerInLandTracker.PlayerClaimEntered += (uid, claim) => { ... };
PlayerInLandTracker.PlayerClaimExited  += (uid, claim) => { ... };
PlayerInLandTracker.PlayerClaimChanged += (uid, from, to) => { ... };

string? claim = PlayerInLandTracker.GetPlayerClaim(uid);
bool there = PlayerInLandTracker.IsPlayerInClaim(uid, "Market square");
```

Values are cached and refreshed by the tick (`TickIntervalMs`, default 1000). The tick runs only while one of the three events has a subscriber; without one `GetPlayerClaim` looks the claim up directly. Static events outlive the world: unsubscribe in your `Dispose`, or the next world keeps polling for a dead handler.
