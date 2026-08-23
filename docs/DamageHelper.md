---
layout: default
title: DamageHelper
nav_order: 40
parent: Common & Utility
---

# DamageHelper

## What is it for?

`DamageHelper` is a factory for `DamageSource` instances with the most common field combinations used across combat abilities, effects, and projectiles. Instead of writing 6-8 lines of object initializer each time, you call a single factory method.

## When to use it

- You need to apply damage from an entity, player, weather, or internal source.
- You want concise one-liners for `entity.ReceiveDamage(...)`.
- You need consistent defaults for `IgnoreInvFrames` across your combat code.

## Quick example

```csharp
using ArcanumLib.Common;

// Entity-sourced damage
target.ReceiveDamage(DamageHelper.Create(boss, EnumDamageType.BluntAttack, 2), 50f);

// Player-sourced damage with tier
entity.ReceiveDamage(DamageHelper.CreatePlayer(player.Entity, EnumDamageType.SlashAttack, 3), 40f);

// Projectile damage (source entity + cause entity)
target.ReceiveDamage(DamageHelper.Create(boss, projectile, EnumDamageType.PiercingAttack, 3), 80f);

// Weather damage (lightning)
entity.ReceiveDamage(DamageHelper.CreateWeather(pos, EnumDamageType.Lightning, 0.5f), 100f);

// Healing
entity.ReceiveDamage(DamageHelper.CreateHeal(), -30f);
```

## API overview

| Method | Description |
|--------|-------------|
| `Create(source, type, [ignoreInvFrames])` | Entity-sourced damage. |
| `Create(source, cause, type, [ignoreInvFrames])` | Entity-sourced with a cause entity (projectiles). |
| `Create(source, type, damageTier, [ignoreInvFrames])` | Entity-sourced with explicit tier. |
| `Create(source, type, knockbackStrength, [ignoreInvFrames])` | Entity-sourced with knockback. |
| `Create(source, cause, type, damageTier, [ignoreInvFrames])` | Entity-sourced with cause and tier. |
| `Create(source, cause, type, knockbackStrength, [ignoreInvFrames])` | Entity-sourced with cause and knockback. |
| `Create(source, cause, type, damageTier, knockbackStrength, [ignoreInvFrames])` | Entity-sourced with all fields. |
| `Create(source, cause, type, damageTier, sourcePos, hitPos, [ignoreInvFrames])` | Projectile-style with positions. |
| `CreatePlayer(source, type, [ignoreInvFrames])` | Player-sourced damage. |
| `CreatePlayer(source, type, damageTier, [ignoreInvFrames])` | Player-sourced with tier. |
| `CreatePlayer(source, type, knockbackStrength, [ignoreInvFrames])` | Player-sourced with knockback. |
| `CreatePlayer(source, type, damageTier, knockbackStrength, [ignoreInvFrames])` | Player-sourced with tier and knockback. |
| `CreatePlayer(source, cause, type, damageTier, knockbackStrength, [ignoreInvFrames])` | Player-sourced projectile/area damage. |
| `CreateWeather(sourcePos, type, knockbackStrength, [ignoreInvFrames])` | Weather-sourced damage. |
| `CreateInternal(type, [ignoreInvFrames])` | Internal damage (self-damage, costs). |
| `CreateInternal(type, damageTier, knockbackStrength, [ignoreInvFrames])` | Internal with tier and knockback. |
| `CreateHeal([ignoreInvFrames])` | Healing damage source. |
