using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Interface for the deferred work service, providing both the active-side scheduler API
/// and explicit client/server facades.
/// </summary>
public interface IDeferredWorkService : IDeferredWork
{
    /// <summary>
    /// Enables or disables the scheduler. When disabled, pending work is executed immediately.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Client-side scheduler. Use this from client-side code or for client-only effects.
    /// </summary>
    IDeferredWork Client { get; }

    /// <summary>
    /// Server-side scheduler. Use this from server-side code or for server-authoritative work.
    /// </summary>
    IDeferredWork Server { get; }

    /// <summary>
    /// Starts the deferred work scheduler for the given API side.
    /// </summary>
    void Start(ICoreAPI api);

    /// <summary>
    /// Stops the deferred work scheduler, cancels pending callbacks and clears the task queue.
    /// </summary>
    void Stop();
}

/// <summary>
/// Instance-based scheduler for deferred and coalesced work on the game tick loop.
/// Use this to debounce repeated events, batch mark-dirty calls, or delay
/// expensive work without storing callback IDs in every caller.
/// Registered in <see cref="Core.ArcanumServices" /> and disposed with the <see cref="Core.ArcanumRuntime" />.
/// </summary>
public sealed class DeferredWorkService : IDeferredWorkService, IDisposable
{
    private sealed class Scheduler
    {
        public ICoreAPI? Api;
        public long TickListenerId;
        public readonly Dictionary<string, ScheduledTask> Tasks = new(StringComparer.Ordinal);
        public readonly Dictionary<string, long> Callbacks = new(StringComparer.Ordinal);
        public readonly Queue<Action> EndOfTickQueue = new();
        public Thread? OwnerThread;
        public bool IsRunning => Api != null;
    }

    private sealed class ScheduledTask
    {
        public string Key;
        public Action Action;
        public long FirstScheduledMs;
        public long DueTimeMs;
        public long? MaxDelayMs;

        public ScheduledTask(string key, Action action)
        {
            Key = key;
            Action = action;
        }
    }

    private sealed class DeferredWorkFacade : IDeferredWork
    {
        private readonly Scheduler _scheduler;
        private readonly DeferredWorkService _owner;

        public DeferredWorkFacade(DeferredWorkService owner, Scheduler scheduler)
        {
            _owner = owner;
            _scheduler = scheduler;
        }

        public void Schedule(string key, Action action, int delayMs)
            => _owner.ScheduleCore(_scheduler, key, action, delayMs);

        public void ScheduleCallback(string key, Action action, int delayMs)
            => _owner.ScheduleCallbackCore(_scheduler, key, action, delayMs);

        public void CancelCallback(string key)
            => _owner.CancelCallbackCore(_scheduler, key);

        public bool IsCallbackPending(string key)
            => _owner.IsCallbackPendingCore(_scheduler, key);

        public void CancelCallbacksByPrefix(string prefix)
            => _owner.CancelCallbacksByPrefixCore(_scheduler, prefix);

        public void Coalesce(string key, Action action, int windowMs, int maxDelayMs = -1)
            => _owner.CoalesceCore(_scheduler, key, action, windowMs, maxDelayMs);

        public void AtEndOfTick(Action action)
            => _owner.AtEndOfTickCore(_scheduler, action);

        public void Cancel(string key)
            => _owner.CancelCore(_scheduler, key);

        public bool IsPending(string key)
            => _owner.IsPendingCore(_scheduler, key);
    }

    private readonly Scheduler _client = new();
    private readonly Scheduler _server = new();
    private readonly object _syncLock = new();
    private bool _disposed;

    /// <summary>
    /// Enables or disables the scheduler. When disabled, pending work is executed immediately.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Client-side scheduler. Use this from client-side code or for client-only effects.
    /// </summary>
    public IDeferredWork Client { get; }

    /// <summary>
    /// Server-side scheduler. Use this from server-side code or for server-authoritative work.
    /// </summary>
    public IDeferredWork Server { get; }

    /// <summary>
    /// Creates a new deferred work service.
    /// </summary>
    public DeferredWorkService()
    {
        Client = new DeferredWorkFacade(this, _client);
        Server = new DeferredWorkFacade(this, _server);
    }

    /// <summary>
    /// Starts the deferred work scheduler for the given API side.
    /// </summary>
    /// <param name="api">The core API instance.</param>
    public void Start(ICoreAPI api)
    {
        if (api is ICoreServerAPI sapi)
            StartScheduler(_server, sapi);
        else if (api is ICoreClientAPI capi)
            StartScheduler(_client, capi);
    }

    /// <summary>
    /// Stops the deferred work scheduler, cancels pending callbacks and clears the task queue.
    /// </summary>
    public void Stop()
    {
        StopScheduler(_client);
        StopScheduler(_server);
    }

    /// <summary>
    /// Schedules a one-shot action to run after <paramref name="delayMs" />.
    /// Calling <see cref="Schedule" /> again with the same <paramref name="key" />
    /// reschedules and replaces the action, so the work runs only once.
    /// </summary>
    public void Schedule(string key, Action action, int delayMs)
        => Active.Schedule(key, action, delayMs);

    /// <summary>
    /// Schedules a one-shot callback via the API's <c>RegisterCallback</c> mechanism.
    /// </summary>
    public void ScheduleCallback(string key, Action action, int delayMs)
        => Active.ScheduleCallback(key, action, delayMs);

    /// <summary>
    /// Cancels a pending <see cref="ScheduleCallback" /> by key.
    /// </summary>
    public void CancelCallback(string key)
        => Active.CancelCallback(key);

    /// <summary>
    /// Returns true when a callback with the given key is pending.
    /// </summary>
    public bool IsCallbackPending(string key)
        => Active.IsCallbackPending(key);

    /// <summary>
    /// Cancels all callbacks whose key starts with <paramref name="prefix" />.
    /// </summary>
    public void CancelCallbacksByPrefix(string prefix)
        => Active.CancelCallbacksByPrefix(prefix);

    /// <summary>
    /// Coalesces repeated calls for the same <paramref name="key" /> into a single execution.
    /// </summary>
    public void Coalesce(string key, Action action, int windowMs, int maxDelayMs = -1)
        => Active.Coalesce(key, action, windowMs, maxDelayMs);

    /// <summary>
    /// Queues an action to run at the end of the current game tick.
    /// </summary>
    public void AtEndOfTick(Action action)
        => Active.AtEndOfTick(action);

    /// <summary>
    /// Cancels a pending task.
    /// </summary>
    public void Cancel(string key)
        => Active.Cancel(key);

    /// <summary>
    /// Returns true when a task with the given key is pending.
    /// </summary>
    public bool IsPending(string key)
        => Active.IsPending(key);

    private IDeferredWork Active
    {
        get
        {
            var current = Thread.CurrentThread;
            lock (_syncLock)
            {
                if (_client.OwnerThread == current && _client.IsRunning)
                    return Client;
                if (_server.OwnerThread == current && _server.IsRunning)
                    return Server;

                if (_server.IsRunning) return Server;
                if (_client.IsRunning) return Client;

                return Server;
            }
        }
    }

    private void StartScheduler(Scheduler scheduler, ICoreAPI api)
    {
        lock (_syncLock)
        {
            if (scheduler.IsRunning)
                StopScheduler(scheduler);

            scheduler.Api = api;
            scheduler.OwnerThread = Thread.CurrentThread;
            scheduler.TickListenerId = api.Event.RegisterGameTickListener(dt => OnGameTick(scheduler, dt), 0);
        }
    }

    private void StopScheduler(Scheduler scheduler)
    {
        lock (_syncLock)
        {
            if (scheduler.Api != null && scheduler.TickListenerId != 0)
            {
                try { scheduler.Api.Event.UnregisterGameTickListener(scheduler.TickListenerId); }
                catch (Exception ex) { scheduler.Api.Logger?.Warning("[ArcanumLib] Failed to unregister DeferredWork tick listener: {0}", ex.Message); }
                scheduler.TickListenerId = 0;
            }

            if (scheduler.Api != null)
            {
                foreach (var callbackId in scheduler.Callbacks.Values)
                {
                    try { scheduler.Api.Event.UnregisterCallback(callbackId); }
                    catch (Exception ex) { scheduler.Api.Logger?.Warning("[ArcanumLib] Failed to unregister DeferredWork callback: {0}", ex.Message); }
                }
            }

            scheduler.Callbacks.Clear();
            scheduler.Tasks.Clear();
            scheduler.EndOfTickQueue.Clear();
            scheduler.Api = null;
            scheduler.OwnerThread = null;
        }
    }

    private void OnGameTick(Scheduler scheduler, float dt)
    {
        var api = scheduler.Api;
        if (api?.World is null) return;

        long now = api.World.ElapsedMilliseconds;
        var toRun = new List<ScheduledTask>(scheduler.Tasks.Count);
        var toRemove = new List<string>(scheduler.Tasks.Count);

        lock (_syncLock)
        {
            foreach (var kvp in scheduler.Tasks)
            {
                var task = kvp.Value;
                if (now >= task.DueTimeMs ||
                    (task.MaxDelayMs.HasValue && now >= task.MaxDelayMs.Value))
                {
                    toRun.Add(task);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
                scheduler.Tasks.Remove(key);
        }

        foreach (var task in toRun)
        {
            try { task.Action(); }
            catch (Exception ex) { api.Logger?.Warning("[ArcanumLib] Deferred task '{0}' failed: {1}", task.Key, ex.Message); }
        }

        var endOfTickBatch = new List<Action>();
        lock (_syncLock)
        {
            int safety = 0;
            while (scheduler.EndOfTickQueue.Count > 0 && safety < 100)
            {
                safety++;
                endOfTickBatch.Add(scheduler.EndOfTickQueue.Dequeue());
            }
        }

        foreach (var current in endOfTickBatch)
        {
            try { current(); }
            catch (Exception ex) { api.Logger?.Warning("[ArcanumLib] End-of-tick task failed: {0}", ex.Message); }
        }
    }

    private void ScheduleCore(Scheduler scheduler, string key, Action action, int delayMs)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (delayMs < 0) delayMs = 0;

        var api = scheduler.Api;
        if (!IsEnabled || api?.World is null)
        {
            action();
            return;
        }

        long now = api.World.ElapsedMilliseconds;
        lock (_syncLock)
        {
            scheduler.Tasks[key] = new ScheduledTask(key, action)
            {
                FirstScheduledMs = now,
                DueTimeMs = now + delayMs,
            };
        }
    }

    private void ScheduleCallbackCore(Scheduler scheduler, string key, Action action, int delayMs)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (delayMs < 0) delayMs = 0;

        var api = scheduler.Api;
        if (!IsEnabled || api?.World is null)
        {
            action();
            return;
        }

        lock (_syncLock)
        {
            if (scheduler.Callbacks.TryGetValue(key, out var existingId))
            {
                api.Event.UnregisterCallback(existingId);
                scheduler.Callbacks.Remove(key);
            }

            long callbackId = api.Event.RegisterCallback(_ =>
            {
                lock (_syncLock)
                {
                    scheduler.Callbacks.Remove(key);
                }
                try { action(); }
                catch (Exception ex) { api.Logger?.Warning("[ArcanumLib] Deferred callback '{0}' failed: {1}", key, ex.Message); }
            }, delayMs);

            scheduler.Callbacks[key] = callbackId;
        }
    }

    private void CancelCallbackCore(Scheduler scheduler, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_syncLock)
        {
            if (scheduler.Callbacks.TryGetValue(key, out var callbackId))
            {
                var api = scheduler.Api;
                api?.Event.UnregisterCallback(callbackId);
                scheduler.Callbacks.Remove(key);
            }
        }
    }

    private bool IsCallbackPendingCore(Scheduler scheduler, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_syncLock)
        {
            return scheduler.Callbacks.ContainsKey(key);
        }
    }

    private void CancelCallbacksByPrefixCore(Scheduler scheduler, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return;
        lock (_syncLock)
        {
            var api = scheduler.Api;
            var toRemove = new List<string>();
            foreach (var kvp in scheduler.Callbacks)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    api?.Event.UnregisterCallback(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
                scheduler.Callbacks.Remove(key);
        }
    }

    private void CoalesceCore(Scheduler scheduler, string key, Action action, int windowMs, int maxDelayMs = -1)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (windowMs < 0) windowMs = 0;

        var api = scheduler.Api;
        if (!IsEnabled || api?.World is null)
        {
            action();
            return;
        }

        long now = api.World.ElapsedMilliseconds;
        lock (_syncLock)
        {
            if (scheduler.Tasks.TryGetValue(key, out var existing))
            {
                existing.Action = action;
                existing.DueTimeMs = now + windowMs;
                return;
            }

            scheduler.Tasks[key] = new ScheduledTask(key, action)
            {
                FirstScheduledMs = now,
                DueTimeMs = now + windowMs,
                MaxDelayMs = maxDelayMs > 0 ? now + maxDelayMs : null,
            };
        }
    }

    private void AtEndOfTickCore(Scheduler scheduler, Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        lock (_syncLock)
        {
            scheduler.EndOfTickQueue.Enqueue(action);
        }
    }

    private void CancelCore(Scheduler scheduler, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_syncLock)
        {
            scheduler.Tasks.Remove(key);
        }
    }

    private bool IsPendingCore(Scheduler scheduler, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_syncLock)
        {
            return scheduler.Tasks.ContainsKey(key);
        }
    }

    /// <summary>
    /// Disposes the service, stops all schedulers, and clears pending work.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
