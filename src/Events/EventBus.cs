using System;
using System.Collections.Generic;
using System.Diagnostics;
using ArcanumLib.Core;

namespace ArcanumLib.Events;

/// <summary>
/// Marker interface for events published through <see cref="EventBus" />.
/// Implement on a plain class or record carrying event data.
/// </summary>
public interface IEvent;

/// <summary>
/// Subscription token returned by <see cref="EventBus.Subscribe{T}(Action{T}, EventBusPriority)" />.
/// Dispose it to unsubscribe. Also works with <see cref="Common.CleanupScope" />.
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
        try { _unsubscribe?.Invoke(); }
        catch (Exception ex)
        {
            ArcanumServices.Get<Vintagestory.API.Common.ICoreAPI>()?.Logger?.Warning(
                "[ArcanumLib] EventBus subscription unsubscribe failed: {0}", ex.Message);
        }
        _unsubscribe = null;
    }
}

/// <summary>
/// Handler priority. Higher priority handlers run first.
/// </summary>
public enum EventBusPriority
{
    /// <summary>Low.</summary>
    Low = 0,
    /// <summary>Normal.</summary>
    Normal = 100,
    /// <summary>High.</summary>
    High = 200,
    /// <summary>Highest.</summary>
    Highest = 300
}

/// <summary>
/// Diagnostic record for a single active EventBus subscription.
/// Used by <see cref="EventBus.GetDiagnostics" /> to report subscription health.
/// </summary>
public sealed class EventBusSubscriptionInfo
{
    /// <summary>Event type the handler is subscribed to.</summary>
    public Type EventType { get; init; } = typeof(object);

    /// <summary>Tag associated with the subscription, or empty for type-only subs.</summary>
    public string Tag { get; init; } = "";

    /// <summary>When the subscription was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Whether the subscription has been disposed.</summary>
    public bool IsDisposed { get; internal set; }

    /// <summary>Number of times the handler has been invoked.</summary>
    public long InvocationCount { get; internal set; }

    /// <summary>Total elapsed time spent in the handler, in milliseconds.</summary>
    public double TotalInvocationMs { get; internal set; }

    /// <summary>Last exception message thrown by the handler, if any.</summary>
    public string? LastError { get; internal set; }

    /// <summary>Average time per invocation in milliseconds, or 0 if never invoked.</summary>
    public double AverageInvocationMs => InvocationCount > 0 ? TotalInvocationMs / InvocationCount : 0;
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
        public EventBusSubscriptionInfo? DiagInfo;
    }

    private readonly struct EventKey : IEquatable<EventKey>
    {
        /// <summary>The event type used as the primary key.</summary>
        public readonly Type EventType;
        /// <summary>Optional tag distinguishing subscriptions to the same event type.</summary>
        public readonly string Tag;

        /// <summary>Creates an event key from an event type and optional tag.</summary>
        /// <param name="eventType">The event type.</param>
        /// <param name="tag">Optional tag; treated as empty string when null.</param>
        public EventKey(Type eventType, string tag)
        {
            EventType = eventType;
            Tag = tag ?? "";
        }

        /// <summary>Determines whether the specified object is equal to the current object.</summary>
        /// <param name="other">The other value.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public bool Equals(EventKey other)
            => EventType == other.EventType && string.Equals(Tag, other.Tag, StringComparison.OrdinalIgnoreCase);

        /// <summary>Returns a hash code for the current object.</summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
            => System.HashCode.Combine(EventType, Tag?.ToLowerInvariant() ?? "");
    }

    private static readonly Dictionary<EventKey, List<HandlerEntry>> _handlers = new();
    private static readonly object _syncLock = new();
    private static int _registrationCounter;
    private static readonly List<WeakReference<HandlerEntry>> _allEntries = new();
    private static readonly List<string> _publishedTags = new();

    // ── Type-only subscriptions (tag = "") — IEvent constrained ──

    /// <summary>
    /// Subscribes a handler to events of type <typeparamref name="T" />.
    /// The returned <see cref="EventBusSubscription" /> unsubscribes on dispose.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="handler">The callback to invoke.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The subscribe.</returns>
    public static EventBusSubscription Subscribe<T>(Action<T> handler, EventBusPriority priority = EventBusPriority.Normal) where T : IEvent
        => SubscribeTyped("", handler, priority);

    /// <summary>
    /// Publishes an event to all subscribers of type <typeparamref name="T" />.
    /// Handlers run synchronously in priority order. Exceptions in one handler
    /// do not block subsequent handlers.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="evt">The evt value.</param>
    /// <returns>The number of handlers invoked.</returns>
    public static int Publish<T>(T evt) where T : IEvent
        => PublishTyped("", evt);

    /// <summary>
    /// Publishes an event on the next server or client game tick.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="evt">The evt value.</param>
    /// <returns>The publish async.</returns>
    public static int PublishAsync<T>(T evt) where T : IEvent
        => PublishTypedAsync("", evt);

    // ── String-tagged typed subscriptions — IEvent constrained ──

    /// <summary>
    /// Subscribes a handler to events of type <typeparamref name="T" /> with the given tag.
    /// Allows multiple distinct events sharing the same payload type.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="tag">The tag value.</param>
    /// <param name="handler">The callback to invoke.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The subscribe.</returns>
    public static EventBusSubscription Subscribe<T>(string tag, Action<T> handler, EventBusPriority priority = EventBusPriority.Normal) where T : IEvent
        => SubscribeTyped(tag, handler, priority);

    /// <summary>
    /// Publishes a tagged event to all subscribers of type <typeparamref name="T" /> with matching tag.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="tag">The tag value.</param>
    /// <param name="evt">The evt value.</param>
    /// <returns>The publish.</returns>
    public static int Publish<T>(string tag, T evt) where T : IEvent
        => PublishTyped(tag, evt);

    /// <summary>
    /// Publishes a tagged event on the next game tick, marshalled to the main thread.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="tag">The tag value.</param>
    /// <param name="evt">The evt value.</param>
    /// <returns>The publish async.</returns>
    public static int PublishAsync<T>(string tag, T evt) where T : IEvent
        => PublishTypedAsync(tag, evt);

    private static EventBusSubscription SubscribeTyped<T>(string tag, Action<T> handler, EventBusPriority priority) where T : IEvent
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var diagInfo = new EventBusSubscriptionInfo
        {
            EventType = typeof(T),
            Tag = tag ?? "",
            CreatedAt = DateTime.UtcNow
        };

        var entry = new HandlerEntry
        {
            Handler = o => handler((T)o!),
            Priority = priority,
            DiagInfo = diagInfo
        };

        lock (_syncLock)
        {
            entry.RegistrationOrder = _registrationCounter++;
            var key = new EventKey(typeof(T), tag ?? "");
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
            _allEntries.Add(new WeakReference<HandlerEntry>(entry));
        }

        var capturedKey = new EventKey(typeof(T), tag ?? "");
        return new EventBusSubscription(() =>
        {
            lock (_syncLock)
            {
                if (_handlers.TryGetValue(capturedKey, out var list))
                {
                    list.RemoveAll(h => ReferenceEquals(h, entry));
                    if (list.Count == 0) _handlers.Remove(capturedKey);
                }
                diagInfo.IsDisposed = true;
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
            RecordPublishedTag(tag);
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
    /// The handler receives the payload as <see cref="object" />.
    /// Use this for integrating with existing string-based event systems.
    /// </summary>
    /// <param name="tag">The tag value.</param>
    /// <param name="handler">The callback to invoke.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The subscribe.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler" /> is <see langword="null" />.</exception>
    public static EventBusSubscription Subscribe(string tag, Action<object?> handler, EventBusPriority priority = EventBusPriority.Normal)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var diagInfo = new EventBusSubscriptionInfo
        {
            EventType = typeof(object),
            Tag = tag ?? "",
            CreatedAt = DateTime.UtcNow
        };

        var entry = new HandlerEntry
        {
            Handler = o => handler(o),
            Priority = priority,
            DiagInfo = diagInfo
        };

        lock (_syncLock)
        {
            entry.RegistrationOrder = _registrationCounter++;
            var key = new EventKey(typeof(object), tag ?? "");
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
            _allEntries.Add(new WeakReference<HandlerEntry>(entry));
        }

        var capturedKey = new EventKey(typeof(object), tag ?? "");
        return new EventBusSubscription(() =>
        {
            lock (_syncLock)
            {
                if (_handlers.TryGetValue(capturedKey, out var list))
                {
                    list.RemoveAll(h => ReferenceEquals(h, entry));
                    if (list.Count == 0) _handlers.Remove(capturedKey);
                }
                diagInfo.IsDisposed = true;
            }
        });
    }

    /// <summary>
    /// Publishes a payload to all subscribers of the given name, regardless of type.
    /// Also dispatches to typed subscribers whose event type matches
    /// the runtime type of <paramref name="payload" />, so external mods using
    /// <see cref="Subscribe{T}(string, Action{T}, EventBusPriority)" /> receive events published through
    /// this untyped overload.
    /// </summary>
    /// <param name="tag">The tag value.</param>
    /// <param name="payload">The payload value.</param>
    /// <returns>The number of subscribers that received the event.</returns>
    public static int Publish(string tag, object? payload)
    {
        if (string.IsNullOrEmpty(tag)) return 0;

        List<HandlerEntry>? untypedSnapshot;
        List<HandlerEntry>? typedSnapshot = null;
        Type? payloadType = payload?.GetType();

        lock (_syncLock)
        {
            RecordPublishedTag(tag);
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
            var sw = entry.DiagInfo != null ? Stopwatch.StartNew() : null;
            try
            {
                entry.Handler(evt);
                invoked++;
            }
            catch (Exception ex)
            {
                ArcanumServices.Get<Vintagestory.API.Common.ICoreAPI>()?.Logger?.Warning(
                    "[ArcanumLib] EventBus handler for {0} threw: {1}", label, ex.Message);
                if (entry.DiagInfo != null)
                    entry.DiagInfo.LastError = ex.Message;
            }
            if (sw != null && entry.DiagInfo != null)
            {
                sw.Stop();
                entry.DiagInfo.InvocationCount++;
                entry.DiagInfo.TotalInvocationMs += sw.Elapsed.TotalMilliseconds;
            }
        }
        return invoked;
    }

    /// <summary>
    /// Removes all subscriptions for event type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    public static void Clear<T>() where T : IEvent
        => Clear<T>("");

    /// <summary>
    /// Removes all subscriptions for event type <typeparamref name="T" /> with the given tag.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="tag">The tag value.</param>
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
            _allEntries.Clear();
            _publishedTags.Clear();
            _registrationCounter = 0;
        }
    }

    /// <summary>
    /// Records a tag as having been published. Used for dangling-handler detection.
    /// </summary>
    /// <param name="tag">The tag value.</param>
    private static void RecordPublishedTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        if (!_publishedTags.Contains(tag))
            _publishedTags.Add(tag);
    }

    /// <summary>
    /// Returns a diagnostic snapshot of all known EventBus subscriptions,
    /// including invocation counts, timing, and errors.
    /// </summary>
    /// <returns>The diagnostics.</returns>
    public static List<EventBusSubscriptionInfo> GetDiagnostics()
    {
        var result = new List<EventBusSubscriptionInfo>();
        lock (_syncLock)
        {
            // Clean up dead weak references
            _allEntries.RemoveAll(wr => !wr.TryGetTarget(out _));

            foreach (var wr in _allEntries)
            {
                if (wr.TryGetTarget(out var entry) && entry.DiagInfo != null)
                    result.Add(entry.DiagInfo);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns tags that have active subscribers but were never published.
    /// Useful for detecting typo'd event names.
    /// </summary>
    /// <returns>The dangling subscriptions.</returns>
    public static List<string> GetDanglingSubscriptions()
    {
        var result = new List<string>();
        lock (_syncLock)
        {
            foreach (var kvp in _handlers)
            {
                if (kvp.Value.Count == 0) continue;
                string tag = kvp.Key.Tag;
                if (string.IsNullOrEmpty(tag)) continue;
                if (!_publishedTags.Contains(tag))
                    result.Add($"{kvp.Key.EventType.Name}[{tag}]");
            }
        }
        return result;
    }

    /// <summary>
    /// Returns the number of active (non-disposed) subscriptions.
    /// </summary>
    /// <returns>The active subscription count.</returns>
    public static int ActiveSubscriptionCount()
    {
        lock (_syncLock)
        {
            int count = 0;
            foreach (var kvp in _handlers)
                count += kvp.Value.Count;
            return count;
        }
    }

    /// <summary>
    /// Returns the number of active subscriptions for event type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <returns>The subscriber count.</returns>
    public static int SubscriberCount<T>() where T : IEvent
        => SubscriberCount<T>("");

    /// <summary>
    /// Returns the number of active subscriptions for event type <typeparamref name="T" /> with the given tag.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="tag">The tag value.</param>
    /// <returns>The subscriber count.</returns>
    public static int SubscriberCount<T>(string tag) where T : IEvent
    {
        lock (_syncLock)
        {
            return _handlers.TryGetValue(new EventKey(typeof(T), tag), out var list) ? list.Count : 0;
        }
    }
}
