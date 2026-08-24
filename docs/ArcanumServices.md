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
- Keeping client, server, and world-scoped services separate so singleplayer does not accidentally overwrite one side with the other.

## Scopes

| Scope | Meaning |
|-------|---------|
| `Global` | Shared across client and server. |
| `Client` | Belongs to the client side. |
| `Server` | Belongs to the server side. |
| `World` | Tied to the currently loaded world. |

`ArcanumLibModSystem` automatically registers `ICoreClientAPI` and `ICoreAPI` under `Client`, and `ICoreServerAPI` and `ICoreAPI` under `Server`.

## Quick example

### Register a service

```csharp
using ArcanumLib.Core;
using ArcanumLib.Persistence;

var tracker = new PityTracker(sapi, "old:pity");
ArcanumServices.Register(tracker, ArcanumServiceScope.Server);
```

### Consume a service

```csharp
var tracker = ArcanumServices.Get<PityTracker>(); // searches all scopes
// or explicitly server-side:
var tracker = ArcanumServices.Get<PityTracker>(ArcanumServiceScope.Server);

tracker?.RecordOpen(playerUid, "my:milestone", rolledTier);
```

### Resolve the current API by side

```csharp
var sapi = ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server);
var capi = ArcanumServices.Get<ICoreClientAPI>(ArcanumServiceScope.Client);
```

### Ensure a service is initialized

```csharp
var tracker = ArcanumServices.EnsureInitialized(() => new PityTracker(sapi), ArcanumServiceScope.Server);
```

### Shut down

```csharp
// ArcanumLibModSystem.Dispose already calls this on world unload.
ArcanumServices.Shutdown();
```

## API

| Method | Description |
|--------|-------------|
| `Register<T>(T service, ArcanumServiceScope scope = Global)` | Registers or replaces a service of type `T` in the given scope. |
| `Unregister<T>(ArcanumServiceScope scope = Global)` | Removes the registered `T` from the given scope. |
| `Get<T>(ArcanumServiceScope? scope = null)` | Returns the registered `T` or null. If `scope` is null, searches all scopes. |
| `TryGet<T>(out T? service, ArcanumServiceScope? scope = null)` | Returns true and the service if one is registered. |
| `EnsureInitialized<T>(Func<T> factory, ArcanumServiceScope scope = Global)` | Returns the existing `T` or creates and registers one. |
| `Get(Type type, ArcanumServiceScope? scope = null)` | Non-generic get by `Type`. |
| `Shutdown(ArcanumServiceScope? scope = null)` | Clears all registered services, or only the given scope. |

## Notes

- Registration is thread-safe.
- Use `T` as the service contract. For example, register `PityTracker` and resolve it via `ArcanumServices.Get<PityTracker>()`.
- Services that implement `IDisposable` are disposed by `Shutdown` and `Unregister`.
- Prefer explicit `Client` / `Server` scopes for side-specific services to avoid singleplayer conflicts.
