---
layout: default
title: CleanupScope
---

# CleanupScope

## What is it for?

`ArcanumLib.Common.CleanupScope` groups disposable resources, `DeferredWork` keys, and game tick listener IDs so they can all be cancelled or released with a single `Dispose()` call.

## When to use it

- A mod system starts several listeners, disposables, or deferred work items.
- You want cleanup in one place, typically in `Dispose()` or teardown.
- You need cleanup to run in reverse registration order.
- You want cleanup exceptions to be logged and swallowed so earlier cleanup is not blocked.

## Quick example

```csharp
using ArcanumLib.Common;
using ArcanumLib.Performance;

private CleanupScope _cleanup;

public override void StartServerSide(ICoreServerAPI sapi)
{
    _cleanup = sapi.CreateCleanupScope()
        .AddListener(sapi.Event.RegisterGameTickListener(OnTick, 100))
        .AddDeferred(_deferredWorkKey);
}

public override void Dispose()
{
    _cleanup?.Dispose();
}
```

## Usage

```csharp
public sealed class CleanupScope : IDisposable
{
    public CleanupScope(ICoreAPI? api = null);

    public CleanupScope AddDeferred(string key);
    public CleanupScope AddListener(long listenerId);
    public CleanupScope Add(IDisposable disposable);
    public CleanupScope Use(IDisposable disposable); // alias for Add
    public void Dispose();
}

public static class CleanupScopeExtensions
{
    public static CleanupScope CreateCleanupScope(this ICoreAPI api);
}
```

| Method | Description |
| --- | --- |
| `AddDeferred` | Cancels a `DeferredWork` key on dispose. |
| `AddListener` | Unregisters a game tick listener on dispose. |
| `Add` / `Use` | Adds a nested `IDisposable` to dispose on cleanup. |
| `CreateCleanupScope` | Extension on `ICoreAPI` that creates a new scope tied to the API. |

## Notes

- Cleanup runs in reverse registration order.
- Exceptions during cleanup are logged and swallowed.
- Adding to a disposed scope throws `ObjectDisposedException`.
