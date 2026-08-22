---
layout: default
title: Status Effects
---

# Status Effects

Apply, tick, and remove timed status effects on entities.

## What is it for?

Use `StatusEffectManager` when your mod has temporary buffs, debuffs, or states:

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

    void OnApply(Entity entity, IStatusEffectInstance instance);
    void OnRemove(Entity entity, IStatusEffectInstance instance);
    void OnTick(Entity entity, IStatusEffectInstance instance, float dt);
}
```

## Stack modes

| Mode | Behaviour |
|------|-----------|
| `Independent` | New, separate instance every time. |
| `Refresh` | Resets the existing instance's duration. |
| `Stack` | Increases stack count up to `MaxStacks`. |
| `Override` | Replaces the old instance. |

## Quick example

```csharp
using ArcanumLib.Effects;

var instance = StatusEffectManager.Apply(entity, new SlowEffect(), durationMs: 10000);
```

## Usage

### Apply

```csharp
var instance = StatusEffectManager.Apply(entity, myEffect, durationMs: 10000, data: null);
```

### Tick

Call this from a client/server tick handler:

```csharp
StatusEffectManager.Tick(dt); // dt in seconds
```

### Remove

```csharp
StatusEffectManager.RemoveAll(entity);
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
StatusEffectManager.OnEffectApplied += (entity, instance) => { /* ... */ };
StatusEffectManager.OnEffectExpired += (entity, instance) => { /* ... */ };
```

Use events to drive UI, logging, or side effects without touching effect classes.
