---
layout: default
title: ModConfig
---

# ModConfig

Typed configuration wrapper with load, save, validation, and defaults.

## What is it for?

`ModConfig<T>` wraps the Vintage Story mod config system with a typed configuration object. It handles loading, saving, validation, and fallback defaults so your mod does not need to repeat boilerplate JSON parsing and error handling.

## When to use it

- Your mod has a `config.json` that should be editable by server operators.
- You want typed access to config values instead of raw `JsonObject` calls.
- You need validation with structured error reporting.
- You want safe defaults when the config file is missing or corrupt.

## Quick example

```csharp
using ArcanumLib.Persistence;

public class MyConfig
{
    public int MaxItems { get; set; } = 100;
    public bool DebugMode { get; set; } = false;
}

var config = new ModConfig<MyConfig>(
    api,
    filename: "MyMod",
    defaults: () => new MyConfig());

config.Load();
int max = config.Current.MaxItems;
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `Load()` | `ConfigResult` | Loads, validates, and applies defaults. |
| `Save()` | `ConfigResult` | Serializes and stores the current config. |
| `Reload()` | `ConfigResult` | Re-loads from disk. |
| `TryApplyJson(string)` | `ConfigResult` | Applies a JSON string with validation. |
| `Current` | `T` | The currently loaded config instance. |

### ConfigResult

```csharp
public sealed class ConfigResult
{
    public ConfigStatus Status { get; }   // Success, DefaultsUsed, ParseFailed, ValidationError, IOError
    public string? Message { get; }
    public bool IsSuccess { get; }
}
```

### Validation

Pass a `validate` predicate to the constructor:

```csharp
var config = new ModConfig<MyConfig>(
    api,
    "MyMod",
    () => new MyConfig(),
    validate: c => c.MaxItems > 0);
```

## Notes

- `Load()` falls back to defaults when the file is missing or cannot be parsed.
- `Save()` serializes using Newtonsoft.Json with indented formatting.
- The config type `T` must have a parameterless constructor.
