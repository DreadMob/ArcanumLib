---
layout: default
title: ApiExtensions
nav_order: 80
has_children: true
---

# ApiExtensions

## What is it for?

`ArcanumLib.Common.ApiExtensions` makes it easy to check whether the current `ICoreAPI`, `ICoreClientAPI`, `ICoreServerAPI`, or `IWorldAccessor` is running on the client or server side without manually comparing `EnumAppSide`.

## When to use it

- Branch logic in code shared between client and server.
- Guard client-only or server-only paths with a clear, readable check.
- Determine the side from an `IWorldAccessor` reference.

## Quick example

```csharp
using ArcanumLib.Common;

if (api.IsServer())
{
    // server-only logic
}

if (world.IsClient())
{
    // client-only logic
}
```

## API overview

| Method | Returns | Description |
|---|---|---|
| `IsClient(this ICoreAPI)` | `bool` | `true` if the API is on the client side. |
| `IsServer(this ICoreAPI)` | `bool` | `true` if the API is on the server side. |
| `IsClient(this ICoreClientAPI)` | `bool` | `true` if the client API is on the client side. |
| `IsServer(this ICoreClientAPI)` | `bool` | `true` if the client API is on the server side. |
| `IsClient(this ICoreServerAPI)` | `bool` | `true` if the server API is on the client side. |
| `IsServer(this ICoreServerAPI)` | `bool` | `true` if the server API is on the server side. |
| `IsClient(this IWorldAccessor?)` | `bool` | `true` if the world accessor is on the client side. |
| `IsServer(this IWorldAccessor?)` | `bool` | `true` if the world accessor is on the server side. |

## Notes

- All overloads use null-conditional checks and return `false` when the target is `null`.