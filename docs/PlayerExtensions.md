---
layout: default
title: PlayerExtensions
parent: "ApiExtensions"
nav_order: 5
---

# PlayerExtensions

## What is it for?

`ArcanumLib.Common.PlayerExtensions` adds helpers for filtering online players to those with living, positioned entities. It removes the need for repeated null, alive, and position checks.

## When to use it

- Iterate over all online players and only process those currently alive in the world.
- Apply effects, buffs, or logic to living player entities.
- Get the `IPlayer` / `Entity` pair without manual null checking.

## Quick example

```csharp
using ArcanumLib.Common;

if (player.HasValidPosition())
{
    // player has a spawned entity with a valid position
}

foreach (var (player, entity) in sapi.World.AllOnlinePlayers.GetAliveEntities())
{
    // player and entity are guaranteed non-null and alive
}
```

## API overview

| Method | Returns | Description |
|---|---|---|
| `HasValidPosition(this IPlayer)` | `bool` | `true` if the player has a spawned entity with a valid position. |
| `GetAliveEntities(this IEnumerable<IPlayer>)` | `IEnumerable<(IPlayer Player, Entity Entity)>` | Yields only players with a living, positioned entity. |
| `GetAliveServerEntities(this IEnumerable<IPlayer>)` | `IEnumerable<(IServerPlayer Player, Entity Entity)>` | Yields only `IServerPlayer` instances with a living, positioned entity. |

## Notes

- The returned enumerables are evaluated lazily.
- Players are skipped when their entity is `null`, not alive, or has no position.