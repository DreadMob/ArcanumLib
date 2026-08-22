---
layout: default
title: PlayerExtensions
---

# PlayerExtensions

`ArcanumLib.Common.PlayerExtensions` adds helpers for iterating over alive player entities.

## Quick example

```csharp
using ArcanumLib.Common;

foreach (var (player, entity) in sapi.World.AllOnlinePlayers.GetAliveEntities())
{
    // player and entity are guaranteed non-null and alive
}
```
