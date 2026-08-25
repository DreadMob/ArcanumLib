---
layout: default
title: Status Effects
nav_order: 30
---

# StatusEffectService

Apply, tick, and remove timed status effects on entities.

## What is it for?

Use `StatusEffectService` when your mod has temporary buffs, debuffs, or states:

- A speed potion that lasts 30 seconds.
- A poison effect that ticks damage over time.
- A stacking armor buff.
- A status that should be removed on death.

It handles duration, refresh, stacking, overriding, and per-tick callbacks.

## Core interfaces

```csharp
public interface IStatusEffect
{
    string Code { get; }
    EnumStackMode StackMode { get; }
    int MaxStacks { get; }
    bool PersistThroughDeath { get; }
    EffectCategory Category { get; }   // Buff, Debuff, or None
    IReadOnlyCollection<string> Tags { get; }  // "fire", "slow", "poison", ...

    void OnApply(Entity entity, IStatusEffectInstance instance);
    void OnRemove(Entity entity, IStatusEffectInstance instance);
    void OnTick(Entity entity, IStatusEffectInstance instance, float dt);
}
```

## Categories

Effects can be classified as `Buff`, `Debuff`, or `None`. This enables dispel-by-category:

```csharp
using ArcanumLib.Core;
using ArcanumLib.Effects;

var svc = ArcanumServices.Get<StatusEffectService>()!;
svc.RemoveByCategory(entity, EffectCategory.Debuff);
```

## Immunities and resistances

Effects can be tagged (e.g. `"fire"`, `"slow"`). Entities can be made immune to or resistant to specific tags:

```csharp
// Full immunity — effects with "fire" tag are rejected entirely
svc.AddImmunity(entity, "fire");

// 50% resistance — effects with "slow" tag last half as long
svc.AddResistance(entity, "slow", 0.5f);

// Check
bool isImmune = svc.IsImmune(entity, "fire");

// Remove
svc.RemoveImmunity(entity, "fire");
svc.RemoveResistance(entity, "slow");
```

Resistance reduces the effective duration: `actualDuration = baseDuration * (1 - resistance)`. A resistance of 1.0 is equivalent to full immunity.

## Stack modes

| Mode | Behaviour |
|------|-----------|
| `Independent` | New, separate instance every time. |
| `Refresh` | Resets the existing instance's duration. |
| `Stack` | Increases stack count up to `MaxStacks`. |
| `Override` | Replaces the old instance. |

## Quick example

```csharp
using ArcanumLib.Core;
using ArcanumLib.Effects;

var svc = ArcanumServices.Get<StatusEffectService>()!;
var instance = svc.Apply(entity, new SlowEffect(), durationMs: 10000);
```

## Usage

### Apply

```csharp
var instance = svc.Apply(entity, myEffect, durationMs: 10000, data: null);
```

### Tick

Call this from a client/server tick handler:

```csharp
svc.Tick(dt); // dt in seconds
```

### Remove

```csharp
svc.RemoveAll(entity);
```

### Example effect

```csharp
public class SlowEffect : IStatusEffect
{
    public string Code => "mymod:slow";
    public EnumStackMode StackMode => EnumStackMode.Stack;
    public int MaxStacks => 3;
    public bool PersistThroughDeath => false;

    public void OnApply(Entity entity, IStatusEffectInstance instance)
    {
        entity.Stats.Set("walkspeed", "mymodSlow", -0.1f * instance.StackCount, true);
    }

    public void OnRemove(Entity entity, IStatusEffectInstance instance)
    {
        entity.Stats.Remove("walkspeed", "mymodSlow");
    }

    public void OnTick(Entity entity, IStatusEffectInstance instance, float dt) { }
}
```

### Stat modifier effect

For simple stat changes, inherit from `StatModifierEffect`:

```csharp
var speedBuff = new StatModifierEffect(
    code: "mymod:speedbuff",
    statCategory: "walkspeed",
    value: 0.2f)
{
    StackMode = EnumStackMode.Refresh,
    PersistThroughDeath = false
};
```

### Events

```csharp
svc.OnEffectApplied += (entity, instance) => { /* ... */ };
svc.OnEffectExpired += (entity, instance) => { /* ... */ };
```

Use events to drive UI, logging, or side effects without touching effect classes.

## Lifecycle

- `StatusEffectService` is registered in `ArcanumServices` by `StatusEffectModSystem` during `StartClientSide` / `StartServerSide`.
- `StatusEffectModSystem.Dispose()` unregisters the service and clears state on world unload.
- Methods are safe to call before the service is ready; they will return no-ops / empty results when the service is not registered.
