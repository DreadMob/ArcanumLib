---
layout: default
title: Vehicles & mounts
nav_order: 19
---

# Vehicles & mounts

`ArcanumLib.Entities` — driving an entity from its seat, and keeping riders in their seat pose.

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
