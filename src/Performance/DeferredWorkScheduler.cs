using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Runs scheduled and coalesced work on the game tick loop.
/// Use this to debounce repeated events, batch mark-dirty calls, or delay
/// expensive work without storing callback IDs in every caller.
/// </summary>
public class DeferredWork : ModSystem
{
    private static ICoreAPI? _api;
    private static long _tickListenerId;
    private static readonly Dictionary<string, ScheduledTask> _tasks = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> _callbacks = new(StringComparer.Ordinal);
    private static readonly Queue<Action> _endOfTickQueue = new();
    private static readonly object _syncLock = new();

    /// <summary>
    /// Enables or disables the scheduler. When disabled, pending work is executed immediately.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

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

    public override double ExecuteOrder() => 0.1;

    public override bool ShouldLoad(EnumAppSide forSide) => true;

    public override void StartClientSide(ICoreClientAPI capi) => Setup(capi);

    public override void StartServerSide(ICoreServerAPI sapi) => Setup(sapi);

    private static void Setup(ICoreAPI api)
    {
        if (_api is not ICoreServerAPI)
        {
            _api = api;
        }

        if (_tickListenerId == 0)
        {
            _tickListenerId = api.Event.RegisterGameTickListener(OnGameTick, 0);
        }
    }

    public override void Dispose()
    {
        if (_api != null && _tickListenerId != 0)
        {
            _api.Event.UnregisterGameTickListener(_tickListenerId);
            _tickListenerId = 0;
        }

        lock (_syncLock)
        {
            foreach (var callbackId in _callbacks.Values)
            {
                _api?.Event.UnregisterCallback(callbackId);
            }
            _callbacks.Clear();

            _tasks.Clear();
            _endOfTickQueue.Clear();
        }

        _api = null;
    }

    /// <summary>
    /// Schedules a one-shot action to run after <paramref name="delayMs"/>.
    /// Calling <see cref="Schedule"/> again with the same <paramref name="key"/>
    /// reschedules and replaces the action, so the work runs only once.
    /// </summary>
    public static void Schedule(string key, Action action, int delayMs)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (delayMs < 0) delayMs = 0;

        if (!IsEnabled || _api?.World is null)
        {
            action();
            return;
        }

        long now = _api.World.ElapsedMilliseconds;
        lock (_syncLock)
        {
            _tasks[key] = new ScheduledTask(key, action)
            {
                FirstScheduledMs = now,
                DueTimeMs = now + delayMs,
            };
        }
    }

    /// <summary>
    /// Schedules a one-shot callback via <see cref="ICoreAPI.Event.RegisterCallback"/>,
    /// which is truly zero-poll: no tick listener runs while the callback is pending.
    /// Calling again with the same <paramref name="key"/> cancels the previous callback.
    /// Use this for timed effects where no polling should occur during the wait.
    /// </summary>
    /// <param name="key">Unique key for the callback. Re-scheduling with the same key cancels the previous one.</param>
    /// <param name="action">Action to run after the delay.</param>
    /// <param name="delayMs">Delay in milliseconds.</param>
    public static void ScheduleCallback(string key, Action action, int delayMs)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (delayMs < 0) delayMs = 0;

        if (!IsEnabled || _api?.World is null)
        {
            action();
            return;
        }

        lock (_syncLock)
        {
            if (_callbacks.TryGetValue(key, out var existingId))
            {
                _api.Event.UnregisterCallback(existingId);
                _callbacks.Remove(key);
            }

            long callbackId = _api.Event.RegisterCallback((_) =>
            {
                lock (_syncLock)
                {
                    _callbacks.Remove(key);
                }
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _api.Logger?.Warning("[ArcanumLib] Deferred callback '{0}' failed: {1}", key, ex.Message);
                }
            }, delayMs);

            _callbacks[key] = callbackId;
        }
    }

    /// <summary>
    /// Cancels a pending <see cref="ScheduleCallback"/> by key.
    /// </summary>
    public static void CancelCallback(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_syncLock)
        {
            if (_callbacks.TryGetValue(key, out var callbackId))
            {
                _api?.Event.UnregisterCallback(callbackId);
                _callbacks.Remove(key);
            }
        }
    }

    /// <summary>
    /// Returns true when a callback with the given key is pending.
    /// </summary>
    public static bool IsCallbackPending(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_syncLock)
        {
            return _callbacks.ContainsKey(key);
        }
    }

    /// <summary>
    /// Cancels all callbacks whose key starts with <paramref name="prefix"/>.
    /// Use this to clean up all effects for a player or entity on disconnect.
    /// </summary>
    public static void CancelCallbacksByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return;
        lock (_syncLock)
        {
            var toRemove = new List<string>();
            foreach (var kvp in _callbacks)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    _api?.Event.UnregisterCallback(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
                _callbacks.Remove(key);
        }
    }

    /// <summary>
    /// Coalesces repeated calls for the same <paramref name="key"/> into a single
    /// execution. The window is extended by <paramref name="windowMs"/> each call,
    /// but the task is forced after <paramref name="maxDelayMs"/> even if calls keep coming.
    /// </summary>
    public static void Coalesce(string key, Action action, int windowMs, int maxDelayMs = -1)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (windowMs < 0) windowMs = 0;

        if (!IsEnabled || _api?.World is null)
        {
            action();
            return;
        }

        long now = _api.World.ElapsedMilliseconds;
        lock (_syncLock)
        {
            if (_tasks.TryGetValue(key, out var existing))
            {
                existing.Action = action;
                existing.DueTimeMs = now + windowMs;
                return;
            }

            _tasks[key] = new ScheduledTask(key, action)
            {
                FirstScheduledMs = now,
                DueTimeMs = now + windowMs,
                MaxDelayMs = maxDelayMs > 0 ? now + maxDelayMs : null,
            };
        }
    }

    /// <summary>
    /// Queues an action to run at the end of the current game tick.
    /// Actions that throw are logged; queued actions are not coalesced.
    /// </summary>
    public static void AtEndOfTick(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        lock (_syncLock)
        {
            _endOfTickQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Cancels a pending task.
    /// </summary>
    public static void Cancel(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_syncLock)
        {
            _tasks.Remove(key);
        }
    }

    /// <summary>
    /// Returns true when a task with the given key is pending.
    /// </summary>
    public static bool IsPending(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_syncLock)
        {
            return _tasks.ContainsKey(key);
        }
    }

    private static void OnGameTick(float dt)
    {
        if (_api?.World is null) return;

        long now = _api.World.ElapsedMilliseconds;
        var toRun = new List<ScheduledTask>(_tasks.Count);
        var toRemove = new List<string>(_tasks.Count);

        lock (_syncLock)
        {
            foreach (var kvp in _tasks)
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
                _tasks.Remove(key);
        }

        foreach (var task in toRun)
        {
            try
            {
                task.Action();
            }
            catch (Exception ex)
            {
                _api.Logger?.Warning("[ArcanumLib] Deferred task '{0}' failed: {1}", task.Key, ex.Message);
            }
        }

        // End-of-tick work, capped to avoid infinite cascading.
        // Drain the queue under lock, then run actions outside the lock so
        // handlers can safely schedule new end-of-tick work.
        var endOfTickBatch = new List<Action>();
        lock (_syncLock)
        {
            int safety = 0;
            while (_endOfTickQueue.Count > 0 && safety < 100)
            {
                safety++;
                endOfTickBatch.Add(_endOfTickQueue.Dequeue());
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
                _api.Logger?.Warning("[ArcanumLib] End-of-tick task failed: {0}", ex.Message);
            }
        }
    }
}
