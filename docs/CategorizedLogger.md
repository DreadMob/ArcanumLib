# CategorizedLogger

Categorized file logger for Vintage Story mods. Writes structured logs to
categorized files in a configurable subfolder of the game's Logs directory.

## Features

- **Categorized file output**: each category gets its own `.log` file (e.g. `combat.log`, `economy/trades.log`).
- **Consolidated important.log**: warnings, errors, and explicitly important events are copied to `important.log`.
- **Four verbosity modes**: `Silent`, `Production` (default), `Debug`, `Verbose`.
- **Debug throttling**: identical debug messages within 1 second are deduplicated.
- **Periodic auto-flush**: writers are flushed every 5 seconds.
- **Thread-safe**: per-category locks and concurrent dictionaries.
- **Configurable console prefix**: e.g. `[MyMod/combat] ...`.
- **Configurable log folder**: e.g. `Logs/mymod/`.
- **Zero-poll**: no tick listener; flushing is driven by log activity.

## Quick start

```csharp
using ArcanumLib.Logging;

public class MyModSystem : ModSystem
{
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        CategorizedLogger.Init(sapi, new LogConfig
        {
            Mode = LogMode.Production,
            EnableFileLog = true
        }, "mymod", "MyMod");
    }

    public override void Dispose()
    {
        CategorizedLogger.Instance?.Dispose();
    }
}
```

## Logging

```csharp
CategorizedLogger.Instance?.Error("combat", "Boss {0} defeated by {1}", bossName, playerName);
CategorizedLogger.Instance?.Warning("economy/trades", "Trade failed: insufficient funds");
CategorizedLogger.Instance?.Info("system", "Server started");
CategorizedLogger.Instance?.Debug("combat", "Tick processing took {0}ms", elapsedMs);
CategorizedLogger.Instance?.Important("system", "Mod version upgraded from {0} to {1}", oldVer, newVer);
CategorizedLogger.Instance?.Structured("combat", "BossHit", ("boss", name), ("damage", dmg.ToString()));
```

## Subclassing with custom categories

Consumers can subclass `CategorizedLogger` to provide named category constants:

```csharp
public class MyModLogger : CategorizedLogger
{
    public new static MyModLogger? Instance => CategorizedLogger.Instance as MyModLogger;

    public static void Init(ICoreAPI api, LogConfig? config = null)
    {
        CategorizedLogger.Instance?.Dispose();
        CategorizedLogger.Instance = new MyModLogger(api, config);
    }

    public MyModLogger(ICoreAPI api, LogConfig? config = null)
        : base(api, config, "mymod", "MyMod") { }

    public static class Categories
    {
        public const string Combat = "combat";
        public const string Economy = "economy/trades";
        public const string System = "system";
    }
}
```

## LogMode reference

| Mode | Files | Console |
|------|-------|---------|
| `Silent` | disabled | errors only |
| `Production` | everything | errors only |
| `Debug` | everything | warnings + errors + debug |
| `Verbose` | everything | almost everything |

## LogConfig

| Property | Default | Description |
|----------|---------|-------------|
| `Mode` | `Production` | Verbosity mode. |
| `EnableFileLog` | `true` | If false, only console output is produced. |

## File layout

```
VintagestoryData/
  Logs/
    mymod/
      important.log
      combat.log
      economy/
        trades.log
      system.log
```

Log files are truncated on each server restart (`FileMode.Create`).
