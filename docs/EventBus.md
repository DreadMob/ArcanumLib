---
layout: default
title: EventBus
nav_order: 50
---

# EventBus

Typed publish/subscribe event bus for cross-mod communication.

## What is it for?

Vintage Story mods communicate through direct `ModSystem` references or watched attributes. `EventBus` provides a typed pub/sub channel: mods can publish events without knowing who subscribes, and subscribe to event types without a hard reference to the publisher.

## When to use it

- Notify other mods when something happens in your mod (player death, combat, objective completion).
- React to events from another mod without adding a project reference.
- Decouple systems that would otherwise call each other directly.

## Quick example

### Define an event

```csharp
using ArcanumLib.Events;

public record PlayerKilledEvent : IEvent
{
    public string VictimUid { get; init; } = "";
    public string KillerUid { get; init; } = "";
    public string Cause { get; init; } = "";
}
```

### Subscribe

```csharp
using ArcanumLib.Events;

var sub = EventBus.Subscribe<PlayerKilledEvent>(e =>
{
    Logger.Notification("Player {0} was killed by {1}", e.VictimUid, e.KillerUid);
});

// Later: sub.Dispose() unsubscribes.
// Or add to a CleanupScope / EventScope.
```

### Publish

```csharp
EventBus.Publish(new PlayerKilledEvent
{
    VictimUid = victim.PlayerUID,
    KillerUid = killer?.PlayerUID ?? "environment",
    Cause = "falling"
});
```

## API overview

### `EventBus.Subscribe<T>(Action<T> handler, EventBusPriority priority)`

Subscribes a handler to events of type `T`. Returns an `EventBusSubscription` that unsubscribes on dispose.

| Parameter | Type | Description |
|-----------|------|-------------|
| `handler` | `Action<T>` | Called when an event of type `T` is published. |
| `priority` | `EventBusPriority` | Higher priority handlers run first. Default is `Normal`. |

### `EventBus.Publish<T>(T evt)`

Publishes an event to all subscribers of type `T`. Handlers run synchronously in priority order. Exceptions in one handler do not block subsequent handlers.

Returns the number of handlers invoked.

### `EventBus.PublishAsync<T>(T evt)`

Publishes an event on the next game tick, marshalled to the main thread. Use this when handlers may touch entities or world state that must be accessed on the main thread. If no API/world is available, falls back to synchronous publish.

Returns the number of handlers that will be invoked.

### `EventBus.Clear<T>()`

Removes all subscriptions for event type `T`.

### `EventBus.ClearAll()`

Removes all subscriptions for all event types. Intended for world shutdown.

### `EventBus.SubscriberCount<T>()`

Returns the number of active subscriptions for event type `T`.

### `EventBus.GetDiagnostics()`

Returns a list of `EventBusSubscriptionInfo` records for every tracked subscription, including invocation count, average time, and last error. See [Diagnostics]({{ site.baseurl }}{% link Diagnostics.md %}) for details.

### `EventBus.GetDanglingSubscriptions()`

Returns a list of subscription keys (`EventType[Tag]`) that have active subscribers but were never published. Useful for detecting typo'd event names.

### `EventBus.ActiveSubscriptionCount()`

Returns the total number of active (non-disposed) subscriptions across all event types and tags.

### `EventBusPriority`

| Value | Description |
|-------|-------------|
| `Low` | Runs after normal-priority handlers. |
| `Normal` | Default. |
| `High` | Runs before normal-priority handlers. |
| `Highest` | Runs first. |

## Notes

- Thread-safe: all operations are lock-guarded.
- `EventBusSubscription` implements `IDisposable` — add it to a `CleanupScope` for automatic cleanup.
- Exceptions in handlers are logged and swallowed so one bad handler doesn't break the bus.
- Events are plain classes or records implementing `IEvent` (a marker interface).
