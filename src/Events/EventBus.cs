using System;
using System.Collections.Generic;
using ArcanumLib.Core;

namespace ArcanumLib.Events;

/// <summary>
/// Marker interface for events published through <see cref="EventBus"/>.
/// Implement on a plain class or record carrying event data.
/// </summary>
public interface IEvent;

/// <summary>
/// Subscription token returned by <see cref="EventBus.Subscribe{T}"/>.
/// Dispose it to unsubscribe. Also works with <see cref="Common.CleanupScope"/>.
/// </summary>
public sealed class EventBusSubscription : IDisposable
{
    private Action? _unsubscribe;
    private bool _disposed;

    internal EventBusSubscription(Action unsubscribe)
    {
        _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
    }

    /// <summary>
    /// Unsubscribes the handler from the bus. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _unsubscribe?.Invoke(); } catch { /* swallow */ }
        _unsubscribe = null;
    }
}

/// <summary>
/// Handler priority. Higher priority handlers run first.
/// </summary>
public enum EventBusPriority
{
    Low = 0,
    Normal = 100,
    High = 200,
    Highest = 300
}

/// <summary>
/// Typed publish/subscribe event bus for cross-mod communication.
/// Mods can publish events without knowing who subscribes, and subscribe
/// to event types without a hard reference to the publisher.
/// Supports both type-only and string-tagged subscriptions for flexibility.
/// </summary>
public static class EventBus
{
    private sealed class HandlerEntry
    {
        public Action<object?> Handler = _ => { };
        public EventBusPriority Priority;
        public int RegistrationOrder;
    }

    private readonly struct EventKey : IEquatable<EventKey>
    {
        public readonly Type EventType;
        public readonly string Tag;

        public EventKey(Type eventType, string tag)
        {
            EventType = eventType;
            Tag = tag ?? "";
        }

        public bool Equals(EventKey other)
            => EventType == other.EventType && string.Equals(Tag, other.Tag, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
            => System.HashCode.Combine(EventType, Tag?.ToLowerInvariant() ?? "");
    }

    private static readonly Dictionary<EventKey, List<HandlerEntry>> _handlers = new();
    private static readonly object _syncLock = new();
    private static int _registrationCounter;

    // ── Type-only subscriptions (tag = "") — IEvent constrained ──

    /// <summary>
    /// Subscribes a handler to events of type <typeparamref name="T"/>.
    /// The returned <see cref="EventBusSubscription"/> unsubscribes on dispose.
    /// </summary>
    public static EventBusSubscription Subscribe<T>(Action<T> handler, EventBusPriority priority = EventBusPriority.Normal) where T : IEvent
        => SubscribeTyped("", handler, priority);

    /// <summary>
    /// Publishes an event to all subscribers of type <typeparamref name="T"/>.
    /// Handlers run synchronously in priority order. Exceptions in one handler
    /// do not block subsequent handlers.
    /// </summary>
    /// <returns>The number of handlers invoked.</returns>
    public static int Publish<T>(T evt) where T : IEvent
        => PublishTyped("", evt);

    /// <summary>
    /// Publishes an event on the next server or client game tick.
    /// </summary>
    public static int PublishAsync<T>(T evt) where T : IEvent
        => PublishTypedAsync("", evt);

    // ── String-tagged typed subscriptions — IEvent constrained ──

    /// <summary>
    /// Subscribes a handler to events of type <typeparamref name="T"/> with the given tag.
    /// Allows multiple distinct events sharing the same payload type.
    /// </summary>
    public static EventBusSubscription Subscribe<T>(string tag, Action<T> handler, EventBusPriority priority = EventBusPriority.Normal) where T : IEvent
        => SubscribeTyped(tag, handler, priority);

    /// <summary>
    /// Publishes a tagged event to all subscribers of type <typeparamref name="T"/> with matching tag.
    /// </summary>
    public static int Publish<T>(string tag, T evt) where T : IEvent
        => PublishTyped(tag, evt);

    /// <summary>
    /// Publishes a tagged event on the next game tick, marshalled to the main thread.
    /// </summary>
    public static int PublishAsync<T>(string tag, T evt) where T : IEvent
        => PublishTypedAsync(tag, evt);

    private static EventBusSubscription SubscribeTyped<T>(string tag, Action<T> handler, EventBusPriority priority) where T : IEvent
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var entry = new HandlerEntry
        {
            Handler = o => handler((T)o!),
            Priority = priority
        };

        lock (_syncLock)
        {
            entry.RegistrationOrder = _registrationCounter++;
            var key = new EventKey(typeof(T), tag);
            if (!_handlers.TryGetValue(key, out var list))
            {
                list = new List<HandlerEntry>();
                _handlers[key] = list;
            }
            list.Add(entry);
            list.Sort((a, b) =>
            {
                int p = b.Priority.CompareTo(a.Priority);
                return p != 0 ? p : a.RegistrationOrder.CompareTo(b.RegistrationOrder);
            });
        }

        var capturedKey = new EventKey(typeof(T), tag);
        return new EventBusSubscription(() =>
        {
            lock (_syncLock)
            {
                if (_handlers.TryGetValue(capturedKey, out var list))
                {
                    list.RemoveAll(h => ReferenceEquals(h, entry));
                    if (list.Count == 0) _handlers.Remove(capturedKey);
                }
            }
        });
    }

    private static int PublishTyped<T>(string tag, T evt) where T : IEvent
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        List<HandlerEntry>? snapshot;
        var key = new EventKey(typeof(T), tag);
        lock (_syncLock)
        {
            if (!_handlers.TryGetValue(key, out var list) || list.Count == 0)
                return 0;
            snapshot = new List<HandlerEntry>(list);
        }

        return InvokeHandlers(snapshot, evt, $"{typeof(T).Name}[{tag}]");
    }

    private static int PublishTypedAsync<T>(string tag, T evt) where T : IEvent
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        List<HandlerEntry>? snapshot;
        var key = new EventKey(typeof(T), tag);
        lock (_syncLock)
        {
            if (!_handlers.TryGetValue(key, out var list) || list.Count == 0)
                return 0;
            snapshot = new List<HandlerEntry>(list);
        }

        var api = ArcanumServices.Get<Vintagestory.API.Common.ICoreAPI>();
        if (api?.World == null)
            return InvokeHandlers(snapshot, evt, $"{typeof(T).Name}[{tag}]");

        var label = $"{typeof(T).Name}[{tag}]";
        api.Event.EnqueueMainThreadTask(() =>
        {
            InvokeHandlers(snapshot, evt, label);
        }, "arcanumlib-eventbus-publish");

        return snapshot.Count;
    }

    // ── Untyped string-only subscriptions (for non-IEvent payloads) ──

    /// <summary>
    /// Subscribes a handler to events by name only, without type constraints.
    /// The handler receives the payload as <see cref="object"/>.
    /// Use this for integrating with existing string-based event systems.
    /// </summary>
    public static EventBusSubscription Subscribe(string tag, Action<object?> handler, EventBusPriority priority = EventBusPriority.Normal)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var entry = new HandlerEntry
        {
            Handler = o => handler(o),
            Priority = priority
        };

        lock (_syncLock)
        {
            entry.RegistrationOrder = _registrationCounter++;
            var key = new EventKey(typeof(object), tag);
            if (!_handlers.TryGetValue(key, out var list))
            {
                list = new List<HandlerEntry>();
                _handlers[key] = list;
            }
            list.Add(entry);
            list.Sort((a, b) =>
            {
                int p = b.Priority.CompareTo(a.Priority);
                return p != 0 ? p : a.RegistrationOrder.CompareTo(b.RegistrationOrder);
            });
        }

        var capturedKey = new EventKey(typeof(object), tag);
        return new EventBusSubscription(() =>
        {
            lock (_syncLock)
            {
                if (_handlers.TryGetValue(capturedKey, out var list))
                {
                    list.RemoveAll(h => ReferenceEquals(h, entry));
                    if (list.Count == 0) _handlers.Remove(capturedKey);
                }
            }
        });
    }

    /// <summary>
    /// Publishes a payload to all subscribers of the given name, regardless of type.
    /// Also dispatches to typed subscribers whose <typeparamref name="T"/> matches
    /// the runtime type of <paramref name="payload"/>, so external mods using
    /// <see cref="Subscribe{T}(string, Action{T})"/> receive events published through
    /// this untyped overload.
    /// </summary>
    public static int Publish(string tag, object? payload)
    {
        if (string.IsNullOrEmpty(tag)) return 0;

        List<HandlerEntry>? untypedSnapshot;
        List<HandlerEntry>? typedSnapshot = null;
        Type? payloadType = payload?.GetType();

        lock (_syncLock)
        {
            var untypedKey = new EventKey(typeof(object), tag);
            if (_handlers.TryGetValue(untypedKey, out var list) && list.Count > 0)
                untypedSnapshot = new List<HandlerEntry>(list);
            else
                untypedSnapshot = null;

            if (payloadType != null)
            {
                var typedKey = new EventKey(payloadType, tag);
                if (_handlers.TryGetValue(typedKey, out var typedList) && typedList.Count > 0)
                    typedSnapshot = new List<HandlerEntry>(typedList);
            }
        }

        int invoked = 0;
        if (typedSnapshot != null)
            invoked += InvokeHandlers(typedSnapshot, payload, $"{payloadType?.Name}[{tag}]");
        if (untypedSnapshot != null)
            invoked += InvokeHandlers(untypedSnapshot, payload, tag);

        return invoked;
    }

    // ── Cleanup ──

    private static int InvokeHandlers<T>(List<HandlerEntry> snapshot, T evt, string label)
    {
        int invoked = 0;
        foreach (var entry in snapshot)
        {
            try
            {
                entry.Handler(evt);
                invoked++;
            }
            catch (Exception ex)
            {
                ArcanumServices.Get<Vintagestory.API.Common.ICoreAPI>()?.Logger?.Warning(
                    "[ArcanumLib] EventBus handler for {0} threw: {1}", label, ex.Message);
            }
        }
        return invoked;
    }

    /// <summary>
    /// Removes all subscriptions for event type <typeparamref name="T"/>.
    /// </summary>
    public static void Clear<T>() where T : IEvent
        => Clear<T>("");

    /// <summary>
    /// Removes all subscriptions for event type <typeparamref name="T"/> with the given tag.
    /// </summary>
    public static void Clear<T>(string tag) where T : IEvent
    {
        lock (_syncLock)
        {
            _handlers.Remove(new EventKey(typeof(T), tag));
        }
    }

    /// <summary>
    /// Removes all subscriptions for all event types. Intended for world shutdown.
    /// </summary>
    public static void ClearAll()
    {
        lock (_syncLock)
        {
            _handlers.Clear();
            _registrationCounter = 0;
        }
    }

    /// <summary>
    /// Returns the number of active subscriptions for event type <typeparamref name="T"/>.
    /// </summary>
    public static int SubscriberCount<T>() where T : IEvent
        => SubscriberCount<T>("");

    /// <summary>
    /// Returns the number of active subscriptions for event type <typeparamref name="T"/> with the given tag.
    /// </summary>
    public static int SubscriberCount<T>(string tag) where T : IEvent
    {
        lock (_syncLock)
        {
            return _handlers.TryGetValue(new EventKey(typeof(T), tag), out var list) ? list.Count : 0;
        }
    }
}
