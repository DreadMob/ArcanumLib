---
layout: default
title: WatchedAttributes
---

# WatchedAttributes

## What is it for?

`ArcanumLib.Data.WatchedAttributesExtensions` adds helpers for `ITreeAttribute` and `Entity.WatchedAttributes`. It removes the common boilerplate of `HasAttribute` checks before getting or setting defaults.

## When to use it

- Initialize a default value for a mod attribute without overwriting an existing one.
- Read a value from `WatchedAttributes` and fall back to a default.
- Safely create nested tree attributes.

## Quick example

```csharp
using ArcanumLib.Data;

// Get an existing value or initialize it to 0 and store it.
int kills = player.Entity.WatchedAttributes.GetOrCreateInt("mod:kills");

// Only set the value if the player does not already have it.
player.Entity.WatchedAttributes.SetBoolIfMissing("mod:tutorial_seen", true);
```

## API overview

The helpers extend `ITreeAttribute`, so call them on `Entity.WatchedAttributes` or any `ITreeAttribute` instance.

| Method | Returns | Description |
|---|---|---|
| `GetOrCreateTreeAttribute(key)` | `ITreeAttribute` | Returns an existing tree or creates and attaches a new one. |
| `GetOrCreateInt/Long/Float/Double/Bool/String(key, defaultValue)` | type | Gets the existing value or writes and returns `defaultValue`. |
| `SetIntIfMissing/LongIfMissing/FloatIfMissing/DoubleIfMissing/BoolIfMissing/StringIfMissing(key, value)` | `void` | Sets the value only when the key does not already exist. |

The `Entity` overload for `GetOrCreateTreeAttribute` is also available:

| Method | Returns | Description |
|---|---|---|
| `GetOrCreateTreeAttribute(this Entity? entity, key)` | `ITreeAttribute?` | Forwards to `entity.WatchedAttributes.GetOrCreateTreeAttribute(key)`. |

## Notes

- Helpers are null-safe: the `Entity?` overload returns `null` when the entity or its `WatchedAttributes` is `null`.
- The `Set*IfMissing` methods preserve existing data; use `GetOrCreate*` when you need the value back.
- Remember to call `MarkPathDirty(key)` after manual changes if the attribute is watched.
