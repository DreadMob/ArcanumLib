using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Runs scheduled and coalesced work on the game tick loop.
/// Use this to debounce repeated events, batch mark-dirty calls, or delay
/// expensive work without storing callback IDs in every caller.
/// </summary>
public static class DeferredWork
{
    private sealed class Scheduler
    {
        public ICoreAPI? Api;
        public long TickListenerId;
        /// <summary>The tasks value.</summary>
        public readonly Dictionary<string, ScheduledTask> Tasks = new(StringComparer.Ordinal);
        /// <summary>The callbacks value.</summary>
        public readonly Dictionary<string, long> Callbacks = new(StringComparer.Ordinal);
        /// <summary>The end of tick queue value.</summary>
        public readonly Queue<Action> EndOfTickQueue = new();
        public Thread? OwnerThread;
        /// <summary>Gets a value indicating whether is running.</summary>
        public bool IsRunning => Api != null;
    }

    private sealed class ScheduledTask
    {
        public string Key;
        public Action Action;
        public long FirstScheduledMs;
        public long DueTimeMs;
        public long? MaxDelayMs;

        /// <summary>Performs the scheduled task operation.</summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="action">The action value.</param>
        public ScheduledTask(string key, Action action)
        {
            Key = key;
            Action = action;
        }
    }

    private static readonly Scheduler _client = new();
    private static readonly Scheduler _server = new();
    private static readonly object _syncLock = new();

    /// <summary>
    /// Enables or disables the scheduler. When disabled, pending work is executed immediately.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Client-side scheduler. Use this from client-side code or for client-only effects.
    /// </summary>
    public static IDeferredWork Client { get; } = new DeferredWorkFacade(_client);

    /// <summary>
    /// Server-side scheduler. Use this from server-side code or for server-authoritative work.
    /// </summary>
    public static IDeferredWork Server { get; } = new DeferredWorkFacade(_server);

    /// <summary>
    /// Starts the deferred work scheduler for the given API side.
    /// </summary>
    /// <param name="api">The core API instance.</param>
    public static void Start(ICoreAPI api)
    {
        if (api is ICoreServerAPI sapi)
        {
            StartScheduler(_server, sapi);
        }
        else if (api is ICoreClientAPI capi)
        {
            StartScheduler(_client, capi);
        }
    }

    /// <summary>
    /// Stops the deferred work scheduler, cancels pending callbacks and clears the task queue.
    /// </summary>
    public static void Stop()
    {
        StopScheduler(_client);
        StopScheduler(_server);
    }

    /// <summary>
    /// Schedules a one-shot action to run after <paramref name="delayMs" />.
    /// Calling <see cref="Schedule" /> again with the same <paramref name="key" />
    /// reschedules and replaces the action, so the work runs only once.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="action">The action value.</param>
    /// <param name="delayMs">The delay ms value.</param>
    public static void Schedule(string key, Action action, int delayMs)
        => Active.Schedule(key, action, delayMs);

    /// <summary>
    /// Schedules a one-shot callback via the API's <c>RegisterCallback</c> mechanism,
    /// which is truly zero-poll: no tick listener runs while the callback is pending.
    /// Calling again with the same <paramref name="key" /> cancels the previous callback.
    /// Use this for timed effects where no polling should occur during the wait.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="action">The action value.</param>
    /// <param name="delayMs">The delay ms value.</param>
    public static void ScheduleCallback(string key, Action action, int delayMs)
        => Active.ScheduleCallback(key, action, delayMs);

    /// <summary>
    /// Cancels a pending <see cref="ScheduleCallback" /> by key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    public static void CancelCallback(string key)
        => Active.CancelCallback(key);

    /// <summary>
    /// Returns true when a callback with the given key is pending.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>true if callback pending; otherwise, false.</returns>
    public static bool IsCallbackPending(string key)
        => Active.IsCallbackPending(key);

    /// <summary>
    /// Cancels all callbacks whose key starts with <paramref name="prefix" />.
    /// Use this to clean up all effects for a player or entity on disconnect.
    /// </summary>
    /// <param name="prefix">The prefix value.</param>
    public static void CancelCallbacksByPrefix(string prefix)
        => Active.CancelCallbacksByPrefix(prefix);

    /// <summary>
    /// Coalesces repeated calls for the same <paramref name="key" /> into a single
    /// execution. The window is extended by <paramref name="windowMs" /> each call,
    /// but the task is forced after <paramref name="maxDelayMs" /> even if calls keep coming.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="action">The action value.</param>
    /// <param name="windowMs">The window ms value.</param>
    /// <param name="maxDelayMs">The max delay ms value.</param>
    public static void Coalesce(string key, Action action, int windowMs, int maxDelayMs = -1)
        => Active.Coalesce(key, action, windowMs, maxDelayMs);

    /// <summary>
    /// Queues an action to run at the end of the current game tick.
    /// Actions that throw are logged; queued actions are not coalesced.
    /// </summary>
    /// <param name="action">The action value.</param>
    public static void AtEndOfTick(Action action)
        => Active.AtEndOfTick(action);

    /// <summary>
    /// Cancels a pending task.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    public static void Cancel(string key)
        => Active.Cancel(key);

    /// <summary>
    /// Returns true when a task with the given key is pending.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>true if pending; otherwise, false.</returns>
    public static bool IsPending(string key)
        => Active.IsPending(key);

    private static IDeferredWork Active
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

                // Fallback: prefer whichever side is running.
                if (_server.IsRunning) return Server;
                if (_client.IsRunning) return Client;

                // No scheduler running: create an immediate no-op to avoid null.
                return Server;
            }
        }
    }

    private static void StartScheduler(Scheduler scheduler, ICoreAPI api)
    {
        lock (_syncLock)
        {
            if (scheduler.IsRunning)
            {
                StopScheduler(scheduler);
            }

            scheduler.Api = api;
            scheduler.OwnerThread = Thread.CurrentThread;
            scheduler.TickListenerId = api.Event.RegisterGameTickListener(dt => OnGameTick(scheduler, dt), 0);
        }
    }

    private static void StopScheduler(Scheduler scheduler)
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

    private static void OnGameTick(Scheduler scheduler, float dt)
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
            try
            {
                task.Action();
            }
            catch (Exception ex)
            {
                api.Logger?.Warning("[ArcanumLib] Deferred task '{0}' failed: {1}", task.Key, ex.Message);
            }
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
            try
            {
                current();
            }
            catch (Exception ex)
            {
                api.Logger?.Warning("[ArcanumLib] End-of-tick task failed: {0}", ex.Message);
            }
        }
    }

    private sealed class DeferredWorkFacade : IDeferredWork
    {
        private readonly Scheduler _scheduler;

        /// <summary>Performs the deferred work facade operation.</summary>
        /// <param name="scheduler">The scheduler value.</param>
        public DeferredWorkFacade(Scheduler scheduler)
        {
            _scheduler = scheduler;
        }

        /// <summary>Performs the schedule operation.</summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="action">The action value.</param>
        /// <param name="delayMs">The delay ms value.</param>
        public void Schedule(string key, Action action, int delayMs)
            => ScheduleCore(_scheduler, key, action, delayMs);

        /// <summary>Performs the schedule callback operation.</summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="action">The action value.</param>
        /// <param name="delayMs">The delay ms value.</param>
        public void ScheduleCallback(string key, Action action, int delayMs)
            => ScheduleCallbackCore(_scheduler, key, action, delayMs);

        /// <summary>Returns a value indicating whether the operation can cel callback.</summary>
        /// <param name="key">The key to look up.</param>
        public void CancelCallback(string key)
            => CancelCallbackCore(_scheduler, key);

        /// <summary>Returns a value indicating whether callback pending.</summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>true if callback pending; otherwise, false.</returns>
        public bool IsCallbackPending(string key)
            => IsCallbackPendingCore(_scheduler, key);

        /// <summary>Returns a value indicating whether the operation can cel callbacks by prefix.</summary>
        /// <param name="prefix">The prefix value.</param>
        public void CancelCallbacksByPrefix(string prefix)
            => CancelCallbacksByPrefixCore(_scheduler, prefix);

        /// <summary>Performs the coalesce operation.</summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="action">The action value.</param>
        /// <param name="windowMs">The window ms value.</param>
        /// <param name="maxDelayMs">The max delay ms value.</param>
        public void Coalesce(string key, Action action, int windowMs, int maxDelayMs = -1)
            => CoalesceCore(_scheduler, key, action, windowMs, maxDelayMs);

        /// <summary>Performs the at end of tick operation.</summary>
        /// <param name="action">The action value.</param>
        public void AtEndOfTick(Action action)
            => AtEndOfTickCore(_scheduler, action);

        /// <summary>Returns a value indicating whether the operation can cel.</summary>
        /// <param name="key">The key to look up.</param>
        public void Cancel(string key)
            => CancelCore(_scheduler, key);

        /// <summary>Returns a value indicating whether pending.</summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>true if pending; otherwise, false.</returns>
        public bool IsPending(string key)
            => IsPendingCore(_scheduler, key);
    }

    private static void ScheduleCore(Scheduler scheduler, string key, Action action, int delayMs)
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

    private static void ScheduleCallbackCore(Scheduler scheduler, string key, Action action, int delayMs)
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
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    api.Logger?.Warning("[ArcanumLib] Deferred callback '{0}' failed: {1}", key, ex.Message);
                }
            }, delayMs);

            scheduler.Callbacks[key] = callbackId;
        }
    }

    private static void CancelCallbackCore(Scheduler scheduler, string key)
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

    private static bool IsCallbackPendingCore(Scheduler scheduler, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_syncLock)
        {
            return scheduler.Callbacks.ContainsKey(key);
        }
    }

    private static void CancelCallbacksByPrefixCore(Scheduler scheduler, string prefix)
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

    private static void CoalesceCore(Scheduler scheduler, string key, Action action, int windowMs, int maxDelayMs = -1)
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

    private static void AtEndOfTickCore(Scheduler scheduler, Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        lock (_syncLock)
        {
            scheduler.EndOfTickQueue.Enqueue(action);
        }
    }

    private static void CancelCore(Scheduler scheduler, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_syncLock)
        {
            scheduler.Tasks.Remove(key);
        }
    }

    private static bool IsPendingCore(Scheduler scheduler, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_syncLock)
        {
            return scheduler.Tasks.ContainsKey(key);
        }
    }
}
