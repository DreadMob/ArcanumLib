---
layout: default
title: Particle Effects
nav_order: 20
---

# Particle Effects

## What is it for?

`ArcanumLib.Particles` provides a fluent builder for `SimpleParticleProperties` and a library of preset particle effects (explosions, auras, pillars, lines, spirals, impacts, shockwaves, ambient effects). All presets use the builder and named color constants for consistent visual style.

## When to use it

- You need to spawn particle effects without manually constructing `SimpleParticleProperties` each time.
- You want named color presets (Fire, Poison, Ice, Shadow, Holy, Arcane, etc.) instead of raw RGBA values.
- You want preset effects (explosion, aura ring, aura sphere, pillar, line, spiral, impact, shockwave, falling, embers, rising) that work out of the box.
- You need a fluent builder for custom particle configurations.

## Quick example

### Using the builder

```csharp
using ArcanumLib.Particles;

sapi.World.SpawnParticles(new ParticleEffectBuilder()
    .Count(20, 30)
    .Color(ParticleUtils.Colors.Fire)
    .Position(center, spread: 2f)
    .VelocityUp(0.2f, 0.8f)
    .Life(1.5f)
    .Gravity(-0.05f)
    .Size(0.3f, 0.2f)
    .Quad()
    .Build());
```

### Using presets

```csharp
// Fire explosion
ParticleUtils.SpawnFireExplosion(sapi, center, radius: 3f, intensity: 2);

// Colored aura ring
ParticleUtils.SpawnAuraRing(sapi, center, radius: 2f, ParticleUtils.Colors.Holy);

// Impact at entity position
ParticleUtils.SpawnImpact(sapi, entity, ParticleUtils.Colors.Blood);

// Ground shockwave
ParticleUtils.SpawnShockwave(sapi, center, radius: 4f, ParticleUtils.Colors.Shadow);

// Rising embers
ParticleUtils.SpawnEmbers(sapi, center, radius: 1.5f);
```

### Client-side aura sphere

```csharp
ParticleUtils.SpawnAuraSphereClient(capi, center, radius: 1.5f, ParticleUtils.Colors.Arcane);
```

## API overview

### ParticleEffectBuilder

Fluent builder for `SimpleParticleProperties`.

| Method | Description |
|--------|-------------|
| `Count(min, max)` | Set particle count range. |
| `Color(rgba)` | Set RGBA color. |
| `Position(center, spread)` | Set spawn area as a cube around center. |
| `Position(min, max)` | Set spawn area as explicit bounds. |
| `AtEntity(entity, spread)` | Set spawn area around an entity's midpoint. |
| `Velocity(min, max)` | Set velocity range. |
| `VelocityUp(min, max)` | Set upward velocity with slight horizontal jitter. |
| `VelocityOutward(speed)` | Set outward velocity. |
| `Life(seconds)` | Set particle lifetime. |
| `Gravity(gravity)` | Set gravity effect. |
| `Size(min, max)` | Set particle size range. |
| `Size(size)` | Set uniform particle size. |
| `Model(model)` | Set particle model. |
| `Cube()` | Use cube particle model. |
| `Quad()` | Use quad particle model. |
| `Build()` | Build the `SimpleParticleProperties`. |
| `Spawn(sapi)` | Build and spawn on server. |
| `Spawn(world)` | Build and spawn on any world. |

### ParticleUtils.Colors

Named RGBA color presets:

| Category | Colors |
|----------|--------|
| Elements | `Fire`, `FireDark`, `Poison`, `PoisonGreen`, `PoisonBright`, `Ice`, `IceBright`, `Lightning`, `LightningBlue` |
| Water | `Nile`, `NileBright`, `NileFoam` |
| Dark | `Shadow`, `ShadowDeep`, `Void` |
| Holy | `Holy`, `HolyGold` |
| Combat | `Blood`, `BloodDark`, `Chain`, `Shield`, `ShieldGold` |
| Magic | `Arcane`, `ArcaneBright`, `Nature`, `NatureBright` |
| Smoke | `Smoke`, `SmokeDark` |
| Basic | `White`, `Black` |
| Mechanical | `MechaSpark`, `MechaOrange`, `MechaSmoke`, `MechaCore` |
| Bone | `BoneWhite`, `BoneMarrow`, `BoneRage`, `BoneDust` |
| Stone | `StoneGrey`, `CryptPurple`, `CryptDeep`, `AncientDust` |
| Toxic | `ToxicGreen`, `Miasma`, `MiasmaBright`, `Corruption` |

### ParticleUtils preset methods

| Method | Description |
|--------|-------------|
| `Create()` | Returns a new `ParticleEffectBuilder`. |
| `SpawnFireExplosion` | Fire explosion with smoke and flash. |
| `SpawnPoisonExplosion` | Poison explosion with green mist. |
| `SpawnExplosion` | Generic colored explosion. |
| `SpawnShadowExplosion` | Shadow/void explosion with dark particles. |
| `SpawnAuraRing` | Ring of particles around a position. |
| `SpawnAuraSphere` | Sphere of particles (server-side). |
| `SpawnAuraSphereClient` | Sphere of particles (client-side). |
| `SpawnPillar` | Column of particles rising from a position. |
| `SpawnLine` | Particles along a line between two points. |
| `SpawnSpiral` | Spiral of particles around a position. |
| `SpawnEntityAura` | Particles around an entity (body glow). |
| `SpawnImpact` | Impact particles at entity or world position. |
| `SpawnShockwave` | Ground-level particles spreading outward. |
| `SpawnFalling` | Falling particles (rain, ash, embers). |
| `SpawnEmbers` | Rising embers/sparks. |
| `SpawnRising` | Slow rising ambient particles. |

## Notes

- All preset methods are null-safe: they return early if the API parameter is null.
- Server-side methods take `ICoreServerAPI`; `SpawnAuraSphereClient` takes `ICoreClientAPI`.
- The builder is mutable and not thread-safe; create a new instance per effect.
