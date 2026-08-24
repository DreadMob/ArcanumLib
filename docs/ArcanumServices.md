---
layout: default
title: ArcanumServices
nav_order: 5
---

# ArcanumServices

World-scoped service registry for ArcanumLib. ModSystems register their services during `StartServerSide` / `StartClientSide`; `ArcanumLibModSystem` clears the registry on world unload, preventing static state from leaking between saves.

## What is it for?

- Avoiding static `Current` or `Instance` fields that survive world reloads.
- Letting static public facades resolve an instance that was created for the current world.
- Enabling cross-mod access to shared services (`PityTracker`, `CategorizedLogger`, `ActionExecutorService`, `StatusEffectService`) without hardcoded references.

## Quick example

### Register a service

```csharp
using ArcanumLib.Core;
using ArcanumLib.Persistence;

var tracker = new PityTracker(sapi, "old:pity");
ArcanumServices.Register(tracker);
```

### Consume a service

```csharp
var tracker = ArcanumServices.Get<PityTracker>();
tracker?.RecordOpen(playerUid, "my:milestone", rolledTier);
```

### Shut down

```csharp
// ArcanumLibModSystem.Dispose already calls this on world unload.
ArcanumServices.Shutdown();
```

## API

| Method | Description |
|--------|-------------|
| `Register<T>(T service)` | Registers or replaces a service of type `T`. |
| `Unregister<T>()` | Removes the registered `T`. |
| `Get<T>()` | Returns the registered `T` or null. |
| `Shutdown()` | Clears all registered services. |

## Notes

- Registration is thread-safe.
- Use `T` as the service contract. For example, register `PityTracker` and resolve it via `ArcanumServices.Get<PityTracker>()`.
- Services that implement `IDisposable` should be disposed before `Shutdown` if they hold unmanaged resources.
