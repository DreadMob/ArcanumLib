---
layout: default
title: Clock
parent: Blocks
nav_order: 10
---

# Clock

`ArcanumLib.Blocks.Clock` — a clock block driven entirely by its blocktype: hands on one or more dials, pendulums and gears, the hour chime (per hour, set in a dialog), a new-year melody, a warning before temporal storms, client-side ticking and a winding sound. A mod ships shapes, sounds and JSON only.

## Setup

Register the classes under your own names (ArcanumLib does not register them):

```csharp
api.RegisterBlockClass("mymod.BlockClock", typeof(BlockClock));
api.RegisterBlockEntityClass("mymod.BEClock", typeof(BlockEntityClock));
```

```json
"class": "mymod.BlockClock",
"entityClass": "mymod.BEClock",
"attributes": { "clock": {
  "hourHand": "mymod:shapes/block/clock/hourhand.json",
  "minuteHand": "mymod:shapes/block/clock/minutehand.json",
  "dials": [ { "center": [8, 8, 7.4] } ],
  "parts": [ { "shape": "mymod:shapes/block/clock/pendulum.json", "center": [8, 6, 9], "pivot": [8, 16, 8], "swingAmplitude": 8, "swingPeriod": 2 } ],
  "chimeSound": "mymod:sounds/clock-chime",
  "tickSound": "mymod:sounds/clock-tick",
  "windSound": "mymod:sounds/clock-wind"
} }
```

Positions are shape pixels (0…16 per block) of the block's own shape before its `rotateY`; the renderer applies the placed variant's rotation, so one config serves all four facings. Hand shapes are tesselated with the block's textures — list their texture codes in the blocktype.

## Config (`ClockConfig`)

| Key | Default | Meaning |
|-----|---------|---------|
| `hourHand`, `minuteHand` | — | Hand shapes. |
| `handPivot` | `[8, 8, 8]` | Point of the hand shapes that sits on the dial axis. |
| `minuteHandOffset` | 0.3 | Minute hand distance from the hour hand along the dial normal (px); negative = behind. |
| `dials` | one dial | `center`, `yaw` (extra turn), `facing` (`north`/`south`), `handScale`. Several dials share the hands (a street clock's four faces). |
| `parts` | none | `shape`, `center`, `pivot`, `scale`, `yaw`, `spin` (rad/s), `swingAmplitude` (deg), `swingPeriod` (s). Angle = spin·t + amplitude·sin(2πt/period). |
| `stormSpin` | `true` | Hands whirl during a temporal storm. |
| `chimeSound`, `chimeRange`, `chimeVolume`, `chimeIntervalMs` | —, 300, 1, 1500 | The hour strike. |
| `melodySound`, `newYearRange`, `warningRange` | chime, 600, 300 | New-year melody and storm warning. |
| `tickSound`, `tickRange`, `tickVolume`, `tickIntervalMs` | —, 10, 0.35, 1000 | Ticking, played locally to players in range. |
| `windSound`, `windRange` | —, 16 | Played when a player opens the chime settings. |
| `langPrefix` | `arcanumlib:` | Domain of the dialog's lang keys. |

## Notes

- Right click opens the chime-hour dialog (`GuiDialogClock`); the server stores 24 hour bits per clock.
- Name and description lang keys may omit the facing variant: `block-mymod:clock` serves `clock-north` and the rest.
