---
layout: default
title: CleanupScope
---

# CleanupScope

`ArcanumLib.Common.CleanupScope` groups disposable resources, `DeferredWork` keys, and game tick listener IDs so they can all be cancelled or released with a single `Dispose()` call.

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

## Notes

- Cleanup runs in reverse registration order.
- Exceptions during cleanup are logged and swallowed.
- Nested `IDisposable` objects can be added with `Add` or `Use`.
