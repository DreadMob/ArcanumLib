---
layout: default
title: EventScope
parent: "ApiExtensions"
nav_order: 4
---

# EventScope

## What is it for?

`ArcanumLib.Common.EventScope` collects event subscriptions and unsubscribes them in reverse order when disposed. Use it in `ModSystem.Dispose` or any `IDisposable` implementation to avoid leaking handlers when a mod unloads.

## When to use it

- Subscribe to Vintage Story events in `StartServerSide` or `StartClientSide`.
- Ensure all handlers are removed when the mod system is disposed.
- Group many event subscriptions under a single disposable scope.

## Quick example

```csharp
using ArcanumLib.Common;

private EventScope _events;

public override void StartServerSide(ICoreServerAPI sapi)
{
    _events = sapi.CreateEventScope()
        .Add(
            () => sapi.Event.PlayerJoin += OnPlayerJoin,
            () => sapi.Event.PlayerJoin -= OnPlayerJoin)
        .Add(
            () => sapi.Event.PlayerDisconnect += OnPlayerDisconnect,
            () => sapi.Event.PlayerDisconnect -= OnPlayerDisconnect);
}

public override void Dispose()
{
    _events?.Dispose();
}
```

## API overview

| Method | Returns | Description |
|---|---|---|
| `Add(Action subscribe, Action unsubscribe)` | `EventScope` | Calls `subscribe` immediately and stores `unsubscribe` for later. |
| `On(Action subscribe, Action unsubscribe)` | `EventScope` | Alias for `Add`. |
| `Dispose()` | `void` | Unsubscribes all registered callbacks in reverse registration order. |

| Extension | Returns | Description |
|---|---|---|
| `CreateEventScope(this ICoreAPI api)` | `EventScope` | Creates a new `EventScope` tied to the given API for logging. |

## Notes

- `Add` takes a subscribe and an unsubscribe action; it calls `subscribe` immediately.
- `On` is an alias for `Add`.
- Exceptions during unsubscribe are logged and swallowed so one failed cleanup does not block the rest.