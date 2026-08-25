---
layout: default
title: Diagnostics
nav_order: 95
---

# Diagnostics

Runtime validation and monitoring for ArcanumLib modules, services, EventBusService health, dependency chains, and server performance.

## What is it for?

When a mod depends on `arcanumlib`, it relies on several services and ModSystems being registered at startup. If something is missing — a service not registered, a dependency version mismatch, an EventBusService subscription leaking — the failure is often silent and hard to trace. `DiagnosticsModSystem` runs automatically at server start and exposes commands for on-demand checks.

## When to use it

- **At server start**: the diagnostics pass runs automatically and logs results to the server console and a dedicated log file.
- **After installing or updating mods**: run `/arcanum diagnose` to verify all dependencies resolve and services are registered.
- **During development**: use `/arcanum eventbus` to inspect subscription counts, invocation timing, and dangling handlers.
- **For performance investigation**: use `/arcanum monitor` to see tick overhead, memory usage, and entity counts over time.

## Automatic checks

On `StartServerSide` (with `ExecuteOrder = 1000`, after all other systems), the diagnostics pass checks:

### Services

Verifies that expected services are registered in `ArcanumServices`:

| Service | Required | Registered by |
|---------|----------|---------------|
| `ICoreAPI` | yes | `ArcanumLibModSystem` |
| `ICoreServerAPI` | yes | `ArcanumLibModSystem` |
| `ActionRegistryService` | yes | `ActionRegistryModSystem` |
| `ActionExecutorService` | yes | `ActionRegistryModSystem` |
| `StatusEffectService` | no | `StatusEffectModSystem` |
| `CategorizedLogger` | no | Consumer via `CategorizedLogger.Init` |

### ModSystems

Verifies that expected ModSystems are loaded:

| System | Required |
|--------|----------|
| `ArcanumLibModSystem` | yes |
| `ActionRegistryModSystem` | yes |
| `StatusEffectModSystem` | no |
| `PityTrackerModSystem` | no |
| `ModDataStoreModSystem` | no |
| `StatCoalescingEngine` | no |
| `GameTimeScheduler` | no |
| `DeferredWork` | no |

### Singletons

Checks `PityTracker.Current` is initialized.

### EventBusService health

| Check | Description |
|-------|-------------|
| Active subscriptions | Total count of live subscriptions. |
| Disposed-but-tracked | Subscriptions that were disposed but still referenced — possible leak. |
| Slow handlers | Handlers with average invocation time > 10 ms. |
| Handler errors | Handlers that threw exceptions (last error message recorded). |
| Dangling subscriptions | Subscriptions on tags that were never published — possible typo in event name. |

### Dependency analysis

| Check | Description |
|-------|-------------|
| Version conflicts | Each mod depending on `arcanumlib` is checked against the loaded version, with semver pre-release awareness (`1.0.0-rc1` < `1.0.0`). |
| Missing dependencies | Mods requiring a dependency that is not loaded (excluding `game`, `survival`, `creative`). |
| Load order | ArcanumLib ModSystems with `ExecuteOrder < -1000` are flagged — they may run before dependents are ready. |

### Runtime monitor

A game-tick listener samples every 5 seconds:

| Metric | Description |
|--------|-------------|
| Tick overhead | Time spent in the tick beyond the expected 5 s interval. |
| Memory | Current and peak `Process.PrivateMemorySize64`. |
| Active entities | `World.LoadedEntities.Count`. |
| Players online | `Server.Players.Length`. |

Up to 60 samples (5 minutes) are retained for trend display.

## Commands

All commands require the `controlserver` privilege.

### `/arcanum diagnose`

Runs a full diagnostics pass and outputs the summary to chat. The full report is logged to the server console and appended to `Logs/arcanumlib-diagnostics.log`.

### `/arcanum monitor`

Shows runtime monitor data: tick overhead, memory, entity counts, and an ASCII trend chart of the last 10 samples.

```
=== ArcanumLib Runtime Monitor ===
Samples: 42/60
Total monitored ticks: 42
Avg tick overhead: 1.23 ms
Max tick overhead: 15.40 ms
Current memory: 3120 MB
Peak memory: 3180 MB

-- Latest Sample --
  Time: 14:22:09
  Tick overhead: 0.85 ms
  Memory: 3120 MB
  Active entities: 103
  Players online: 3

-- Recent Trend (last 10) --
  14:21:24 |    0.5 ms | #
  14:21:29 |    1.2 ms | #
  14:21:34 |   15.4 ms | #######
  14:21:39 |    0.3 ms |
  ...
=== End Monitor ===
```

### `/arcanum eventbus`

Lists all tracked EventBusService Subscriptions with status, invocation count, average time, and last error. Also shows dangling subscriptions (tags with subscribers but no publishes).

```
=== EventBusService Subscriptions ===
Total tracked: 24
Active: 22

  [ACTIVE] EncounterCompletedEvent[encounter.completed] calls=142 avg=0.32ms
  [ACTIVE] MyModEvent[custom.category] calls=89 avg=0.15ms
  [DISPOSED] PlayerEvent[player.join] calls=3 avg=0.08ms
  ...

Dangling (never published): 2
  - MyModEvent[custom.abandond]
  - PlayerDeathEvent[player.deaht]
=== End EventBusService ===
```

## Log file

Each diagnostics pass is appended to:

```
<game data>/Logs/arcanumlib-diagnostics.log
```

The file is UTF-8 encoded and accumulates across server restarts. Each entry is timestamped and includes the full report with all sections.

## EventBusService diagnostic API

The following methods on `EventBusService` are available for programmatic access. Resolve the service via `ArcanumServices.Get<EventBusService>()`:

### `GetDiagnostics()`

Returns `List<EventBusSubscriptionInfo>` with details about every tracked subscription:

| Field | Description |
|-------|-------------|
| `EventType` | The event type the handler is subscribed to. |
| `Tag` | The string tag, or empty for type-only subscriptions. |
| `CreatedAt` | UTC timestamp when the subscription was created. |
| `IsDisposed` | `true` if the subscription has been disposed. |
| `InvocationCount` | Number of times the handler has been invoked. |
| `TotalInvocationMs` | Total time spent in the handler, in milliseconds. |
| `AverageInvocationMs` | `TotalInvocationMs / InvocationCount`, or 0 if never invoked. |
| `LastError` | Last exception message thrown by the handler, if any. |

### `GetDanglingSubscriptions()`

Returns `List<string>` of subscription keys (`EventType[Tag]`) that have active subscribers but were never published. Useful for detecting typo'd event names.

### `ActiveSubscriptionCount()`

Returns the number of currently active (non-disposed) subscriptions across all event types and tags.

## Notes

- The diagnostics system is server-side only.
- Runtime monitoring has minimal overhead: one `Stopwatch` per handler invocation and one process memory query every 5 seconds.
- The semver comparison in dependency analysis handles pre-release suffixes: `1.0.0-rc1` is considered lower than `1.0.0`, so a mod requiring `arcanumlib@1.0.0` will fail against `1.0.0-rc1`.
- Weak references are used for subscription tracking, so disposed and GC'd entries are cleaned up automatically.
