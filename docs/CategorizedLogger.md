---
layout: default
title: CategorizedLogger
nav_order: 40
---

# CategorizedLogger

File and console logger with per-category files, structured events, and throttled debug output.

## What is it for?

- Writing `INFO`/`WARN`/`DEBUG` logs into per-category files inside a mod subfolder.
- Mirroring important messages to the in-game console with a configurable prefix.
- Throttling repetitive debug messages to avoid per-tick log bloat.

## Quick example

```csharp
using ArcanumLib.Logging;

CategorizedLogger.Init(api, new LogConfig { Mode = LogMode.Verbose }, "myMod", "MyMod");
CategorizedLogger.Instance?.Info("combat", "Player hit target for {0}", damage);
CategorizedLogger.Instance?.Warning("combat", "Target enraged", ex);
```

## Notes

- `CategorizedLogger.Instance` is a facade for the instance registered in `ArcanumServices`.
- `Dispose` flushes and closes all file writers; call it on world unload.
- Empty `catch` blocks are not used; all failures are logged.
