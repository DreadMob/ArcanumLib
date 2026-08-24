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
/// </summary>
public static class EventBus
{
    private sealed class HandlerEntry
    {
        public Action<IEvent> Handler = _ => { };
        public EventBusPriority Priority;
        public int RegistrationOrder;
    }

    private static readonly Dictionary<Type, List<HandlerEntry>> _handlers = new();
    private static readonly object _syncLock = new();
    private static int _registrationCounter;

    /// <summary>
    /// Subscribes a handler to events of type <typeparamref name="T"/>.
    /// The returned <see cref="EventBusSubscription"/> unsubscribes on dispose.
    /// </summary>
    /// <param name="handler">Called when an event of type <typeparamref name="T"/> is published.</param>
    /// <param name="priority">Higher priority handlers run first. Default is <see cref="EventBusPriority.Normal"/>.</param>
    public static EventBusSubscription Subscribe<T>(Action<T> handler, EventBusPriority priority = EventBusPriority.Normal) where T : IEvent
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        int order;
        var entry = new HandlerEntry
        {
            Handler = e => handler((T)e),
            Priority = priority
        };

        lock (_syncLock)
        {
            order = _registrationCounter++;
            entry.RegistrationOrder = order;

            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<HandlerEntry>();
                _handlers[type] = list;
            }
            list.Add(entry);

            // Keep sorted by priority desc, then registration order asc.
            list.Sort((a, b) =>
            {
                int p = b.Priority.CompareTo(a.Priority);
                return p != 0 ? p : a.RegistrationOrder.CompareTo(b.RegistrationOrder);
            });
        }

        return new EventBusSubscription(() =>
        {
            lock (_syncLock)
            {
                if (_handlers.TryGetValue(typeof(T), out var list))
                {
                    list.RemoveAll(h => ReferenceEquals(h, entry));
                    if (list.Count == 0) _handlers.Remove(typeof(T));
                }
            }
        });
    }

    /// <summary>
    /// Publishes an event to all subscribers of type <typeparamref name="T"/>.
    /// Handlers run synchronously in priority order. Exceptions in one handler
    /// do not block subsequent handlers.
    /// </summary>
    /// <returns>The number of handlers invoked.</returns>
    public static int Publish<T>(T evt) where T : IEvent
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        List<HandlerEntry>? snapshot;
        lock (_syncLock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
                return 0;
            snapshot = new List<HandlerEntry>(list);
        }

        return InvokeHandlers(snapshot, evt, typeof(T).Name);
    }

    /// <summary>
    /// Publishes an event on the next server or client game tick.
    /// The event is marshalled to the main thread through <see cref="ArcanumServices"/>.
    /// Use this when handlers may touch entities or world state that must be accessed on the main thread.
    /// </summary>
    /// <returns>The number of handlers that will be invoked, or 0 if no subscribers or no API is available.</returns>
    public static int PublishAsync<T>(T evt) where T : IEvent
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        List<HandlerEntry>? snapshot;
        lock (_syncLock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
                return 0;
            snapshot = new List<HandlerEntry>(list);
        }

        var api = ArcanumServices.Get<Vintagestory.API.Common.ICoreAPI>();
        if (api?.World == null)
        {
            // No world available — invoke synchronously as fallback.
            return InvokeHandlers(snapshot, evt, typeof(T).Name);
        }

        var typeName = typeof(T).Name;
        api.Event.EnqueueMainThreadTask(() =>
        {
            InvokeHandlers(snapshot, evt, typeName);
        }, "arcanumlib-eventbus-publish");

        return snapshot.Count;
    }

    private static int InvokeHandlers<T>(List<HandlerEntry> snapshot, T evt, string typeName) where T : IEvent
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
                    "[ArcanumLib] EventBus handler for {0} threw: {1}", typeName, ex.Message);
            }
        }
        return invoked;
    }

    /// <summary>
    /// Removes all subscriptions for event type <typeparamref name="T"/>.
    /// </summary>
    public static void Clear<T>() where T : IEvent
    {
        lock (_syncLock)
        {
            _handlers.Remove(typeof(T));
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
    {
        lock (_syncLock)
        {
            return _handlers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
        }
    }
}
