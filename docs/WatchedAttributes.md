---
layout: default
title: WatchedAttributes
---

# WatchedAttributes

`ArcanumLib.Data.WatchedAttributesExtensions` adds helpers for `ITreeAttribute` and `Entity.WatchedAttributes`. It removes the common boilerplate of `HasAttribute` checks before getting or setting defaults.

## Quick example

```csharp
using ArcanumLib.Data;

// Get an existing value or initialize it to 0 and store it.
int kills = player.Entity.WatchedAttributes.GetOrCreateInt("mod:kills");

// Only set the value if the player does not already have it.
player.Entity.WatchedAttributes.SetBoolIfMissing("mod:tutorial_seen", true);
```

## Available helpers

- `GetOrCreateTreeAttribute(key)`
- `GetOrCreateInt/Long/Float/Double/Bool/String(key, defaultValue)`
- `SetIntIfMissing/LongIfMissing/FloatIfMissing/DoubleIfMissing/BoolIfMissing/StringIfMissing`
- `SetAndMarkDirty(key, value)` for `bool/int/long/float/double/string`

All helpers are also available directly on `Entity` (they forward to `Entity.WatchedAttributes`).
