---
layout: default
title: Vehicles & mounts
nav_order: 19
---

# Vehicles & mounts

`ArcanumLib.Entities` — driving an entity from its seat, keeping riders in their seat pose, standing them up next to a seat, and clips that follow a speed.

## EntityBehaviorDrivable

The passenger of a controllable seat drives the entity: each tick the behavior reads the seat's synced controls, integrates a signed forward speed and a yaw rate, turns `Pos.Yaw` and writes the horizontal motion along the facing (vanilla mount convention, forward = `(sin yaw, 0, cos yaw)`). The vertical axis is left to the physics behavior (`passivephysicsmultibox` or the like), which also does the terrain collision.

While a player drives, the driver's client is position-authoritative (as for vanilla mounts) and turns the vehicle every frame; with a non-player at the wheel the server integrates the motion itself. A blocked step up to `stepUpMax` is climbed with a short hop that keeps a minimum forward speed.

Register it yourself on both sides and list it in both behavior lists:

```csharp
api.RegisterEntityBehaviorClass("drivable", typeof(EntityBehaviorDrivable));
```

```json
{ "code": "drivable", "drive": { "maxSpeed": 5, "turnSpeed": 1.2, "steerK": 3, "waterOnly": true } }
```

### `drive` keys (`DrivableParams`)

| Key | Default | Meaning |
|-----|---------|---------|
| `maxSpeed` | 4 | Top forward speed, blocks/s. |
| `reverseFactor` | 0.5 | Share of `maxSpeed` reachable backwards. |
| `accelK`, `brakeK` | 0.5, 1 | Approach rates (1/s) when speeding up / slowing or reversing. |
| `coastDrag` | 0.98 | Per-second speed multiplier without input. |
| `turnSpeed`, `steerK` | 1, 2 | Peak yaw rate (rad/s) and its approach rate. |
| `understeer` | 0 | Share of the demanded yaw rate that is lost. |
| `steerFalloff` | 1 | Share of `turnSpeed` left at full speed (lower = numb wheel when fast). |
| `steerDragK` | 0 | Speed lost per second while steering hard at speed. |
| `stepUpMax` | 0.55 | Highest step (blocks) the assist climbs. |
| `turnAtStandstill` | `false` | Turn in place; the turn curve no longer depends on speed. |
| `waterOnly` | `false` | Drive input is ignored unless the feet are in a liquid. |

Bad values are clamped; non-finite ones fall back to the default.

### Runtime members

| Member | Meaning |
|--------|---------|
| `CurrentSpeed`, `YawRate` | Signed speed and yaw rate (synced to other clients at 10 Hz). |
| `ThrottleInput`, `SteerInput` | Raw input this tick (−1, 0, +1). |
| `MaxSpeedFactor` | External multiplier on the target speed (0…2.5): reduced power, boost. |
| `IsBlocked` | The entity tried to move and could not (stalled, or out of water with `waterOnly`). |
| `EngineOn`, `SetEngineOn(byEntity, on)` | Engine switch, persisted and synced; only the driver may flip it (server). |
| `IsDriver(entity)` | Whether that entity sits in a controlling seat. |

## RiderPoses

The game starts a seat's rider pose (`IMountableSeat.SuggestedAnimation`) once, on mounting. A re-tesselation, a wearable or model change or an animation reset later drops it, and nothing restarts it — the rider stands waist-deep in the seat. Call `RiderPoses.Keep` from a client tick of whatever owns the seats (a few times a second is enough):

```csharp
RiderPoses.Keep(entity);                 // every seat of a mountable entity
RiderPoses.Keep(seats);                  // a seat list
RiderPoses.Keep(rider, seat.SuggestedAnimation);  // one rider; returns whether it had to restart
```

Seats whose pose the caller changes on purpose (a carried rider, a pose synced to a clip) are left to the caller.

## WheelClip

A looping clip whose speed follows something on the client: wheels, paddles, treads, a drill head, a winch drum. Create one per clip and call `Run` every client tick with the signed target speed (negative runs the clip backwards, 0 comes to rest):

```csharp
_wheels ??= new WheelClip("roll");
_wheels.Run(entity.AnimManager, speed / maxSpeed * 2f, dt);
```

- Started once, never stopped: at speed 0 it holds the pose where it is. `StopAnimation` would fade the bones back to the rest angle (wheels roll backwards for a moment) and the next start would jump to frame 0 — never stop such a clip.
- Started with `ClientSide = true`. A client-started clip without it is dropped by the next server animation packet on every entity but the local player. If something still drops it, the next `Run` restarts it from the frame it was on (`Frame`).
- The speed is eased (~0.15 s), since observers get the speed from the server in steps.

`Speed` is the eased speed last applied; `Run` returns whether it (re)started the clip.

## SeatExit

Someone getting up from a seat or a bed stays where the seat was and stands inside the block's collision boxes; the physics does not push out of a box the entity is already in, so they sink through the furniture. `SeatExit.StandUp` moves them to the free floor nearest the seat, searching only the ring of cells right around the block's footprint (never past a wall), on the same level, one up, then one down; if walled in, on top of the block. Call it on the server when the rider unmounts:

```csharp
public override void DidUnmount(EntityAgent agent)
{
    base.DidUnmount(agent);
    if (DoTeleportOnUnmount) SeatExit.StandUp(agent.World, agent, block, pos, SeatPosition.XYZ);
}
```

`beside: true` (a bed) always stands the sleeper on the floor beside the block, even when they would fit on the mattress. `SeatExit.Spot` returns the place without moving anyone (null: free where they are, or nowhere better). Block seats only: the footprint is the block's collision boxes.

## Riders seen by other players

For a rider who is not the local player, vanilla `EntityPlayerShapeRenderer` turns the body by the **vehicle's** yaw (`MountedOn.Entity.Pos.Yaw`) and multiplies only `seat.RenderTransform` in before it; the seat attachment point's own Y rotation is never applied. A seat facing forward looks right, a sideways or rear seat on an entity shows the rider along the entity for everyone else, while the rider sees themselves correctly. Seats on such attachment points must add the turn to `RenderTransform` for non-local riders (AlegacySteamworks `EntityVehicleSeat` does). Block seats (`IMountableSeat.Entity == null`) are not affected: there the renderer uses the rider's own body yaw.
