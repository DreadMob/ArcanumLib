# EventScope

`ArcanumLib.Common.EventScope` collects event subscriptions and unsubscribes them in reverse order when disposed. Use it in `ModSystem.Dispose` or any `IDisposable` implementation to avoid leaking handlers when a mod unloads.

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

## Notes

- `Add` takes a subscribe and an unsubscribe action; it calls subscribe immediately.
- `On` is an alias for `Add`.
- Exceptions during unsubscribe are logged and swallowed so one failed cleanup does not block the rest.
