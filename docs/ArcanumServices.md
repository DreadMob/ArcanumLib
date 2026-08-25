---
layout: default
title: ArcanumServices
nav_order: 5
---

# ArcanumServices

World-scoped service registry for ArcanumLib. Each world load activates an `ArcanumRuntime` that owns an `ArcanumServiceRegistry` instance. ModSystems register their services during `StartServerSide` / `StartClientSide`; the runtime is disposed on world unload, preventing static state from leaking between saves.

## Architecture

`ArcanumRuntime` is the instance-based root. It owns:
- `ArcanumServiceRegistry Services` — the per-world service registry.
- Lifecycle coordination via `ArcanumLifecycle`.

`ArcanumServices` (static) and `ArcanumLifecycle` (static) are thin facades that delegate to `ArcanumRuntime.Current`. This preserves backward compatibility while enabling instance-based access for new code and tests.

## What is it for?

- Avoiding static `Current` or `Instance` fields that survive world reloads.
- Letting static public facades resolve an instance that was created for the current world.
- Enabling cross-mod access to shared services (`PityTracker`, `CategorizedLogger`, `ActionExecutorService`, `StatusEffectService`) without hardcoded references.
- Keeping client, server, and world-scoped services separate so singleplayer does not accidentally overwrite one side with the other.
- Allowing tests to create and dispose runtime instances for isolation.

## Scopes

| Scope | Meaning |
|-------|---------|
| `Global` | Shared across client and server. |
| `Client` | Belongs to the client side. |
| `Server` | Belongs to the server side. |
| `World` | Tied to the currently loaded world. |

`ArcanumLibModSystem` automatically registers `ICoreClientAPI` and `ICoreAPI` under `Client`, and `ICoreServerAPI` and `ICoreAPI` under `Server`.

## Quick example

### Instance-based access (preferred for new code)

```csharp
using ArcanumLib.Core;

var registry = ArcanumRuntime.Current?.Services;
var sapi = registry?.Get<ICoreServerAPI>(ArcanumServiceScope.Server);
```

### Static facade (backward compatible)

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
// ArcanumLibModSystem.Dispose already disposes the runtime on world unload.
// For manual cleanup:
ArcanumRuntime.Current?.Dispose();
```

## API

### ArcanumRuntime

| Member | Description |
|--------|-------------|
| `Current` | The active runtime, or null if no world is loaded. |
| `Activate()` | Creates a new runtime, sets it as `Current`, and returns it. Disposes any prior runtime. |
| `Services` | The `ArcanumServiceRegistry` for this runtime. |
| `Api` | The core API for the current side. |
| `Side` | The application side (Client or Server). |
| `Initialize()` | Marks the runtime as initialized and runs lifecycle init handlers. |
| `Dispose()` | Runs lifecycle disposal, shuts down all services, clears `Current`. |

### ArcanumServices (static facade)

| Method | Description |
|--------|-------------|
| `Register<T>(T service, ArcanumServiceScope scope = Global)` | Registers or replaces a service of type `T` in the given scope. Throws if no runtime is active. |
| `Unregister<T>(ArcanumServiceScope scope = Global)` | Removes the registered `T` from the given scope. No-op if no runtime is active. |
| `Get<T>(ArcanumServiceScope? scope = null)` | Returns the registered `T` or null. Returns null if no runtime is active. |
| `TryGet<T>(out T? service, ArcanumServiceScope? scope = null)` | Returns true and the service if one is registered. |
| `EnsureInitialized<T>(Func<T> factory, ArcanumServiceScope scope = Global)` | Returns the existing `T` or creates and registers one. Throws if no runtime is active. |
| `Get(Type type, ArcanumServiceScope? scope = null)` | Non-generic get by `Type`. Returns null if no runtime is active. |
| `Shutdown(ArcanumServiceScope? scope = null)` | Clears all registered services, or only the given scope. No-op if no runtime is active. |

## Notes

- Registration is thread-safe.
- Use `T` as the service contract. For example, register `PityTracker` and resolve it via `ArcanumServices.Get<PityTracker>()`.
- Services that implement `IDisposable` are disposed by `Shutdown` and `Unregister`.
- Prefer explicit `Client` / `Server` scopes for side-specific services to avoid singleplayer conflicts.
- `Get<T>` and `Get(Type)` return null when no runtime is active, making them safe for logging paths.
- `Register` and `EnsureInitialized` throw when no runtime is active, since registration requires an active world.
