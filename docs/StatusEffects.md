---
layout: default
title: Status Effects
---

# Status Effects

`ArcanumLib.Effects.StatusEffectManager` is a static manager for applying, ticking, and removing status effects on `Vintagestory.API.Common.Entities.Entity` instances. It supports refresh, stack, override, and independent modes.

## When to use it

Use `StatusEffectManager` when your mod has timed buffs, debuffs, or any temporary state that:

- Needs a duration and a per-tick callback.
- Stacks or refreshes when re-applied.
- Should be removed on death (or persist through it).
- Needs safe apply/remove hooks even if the entity is invalid.

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

public interface IStatusEffectInstance
{
    long InstanceId { get; }
    IStatusEffect Effect { get; }
    float RemainingMs { get; }
    float MaxDurationMs { get; }
    int StackCount { get; }
    object? Data { get; }
    bool IsExpired { get; }
}
```

## Stack modes

| Mode | Behavior |
|------|----------|
| `Independent` | A new, separate instance is created every time. |
| `Refresh` | The existing instance's duration is reset. |
| `Stack` | The stack count increases up to `MaxStacks`; duration is reset. |
| `Override` | The new instance replaces the old one. |

## Applying an effect

```csharp
var instance = StatusEffectManager.Apply(entity, myEffect, durationMs: 10000, data: null);
```

The returned `IStatusEffectInstance` is the active or updated instance. Events are raised for `New`, `Refreshed`, `Stacked`, and so on.

## Ticking

Call `Update` from a server/client tick handler:

```csharp
StatusEffectManager.Update(dt);
```

`dt` is in seconds. Effects whose duration reaches zero are removed and `OnRemove` is called.

## Removing effects

Remove all effects on an entity:

```csharp
StatusEffectManager.RemoveAll(entity);
```

Remove all effects from dead entities:

```csharp
StatusEffectManager.CleanupDead();
```

## Example effect

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

    public void OnTick(Entity entity, IStatusEffectInstance instance, float dt)
    {
        // optional per-tick logic
    }
}
```

## Stat modifier effects

`StatModifierEffect` is a reusable effect that adds or removes a flat value from an `EntityStats` category. Useful for simple buffs/debuffs.

```csharp
public class MySpeedBuff : StatModifierEffect
{
    public override string Code => "mymod:speedbuff";
    public override string Category => "walkspeed";
    public override string StatKey => "mymodSpeedBuff";
    public override float Value => 0.2f;
    public override bool PersistThroughDeath => false;
    public override EnumStackMode StackMode => EnumStackMode.Refresh;
}
```

## Events

`StatusEffectManager` exposes events for monitoring:

- `OnEffectApplied`
- `OnEffectRefreshed`
- `OnEffectStacked`
- `OnEffectExpired`
- `OnEffectRemoved`

Use these to drive UI, logging, or side effects without modifying the effect classes.
