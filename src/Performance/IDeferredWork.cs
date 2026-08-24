using System;

namespace ArcanumLib.Performance;

/// <summary>
/// Side-scoped deferred work scheduler.
/// </summary>
public interface IDeferredWork
{
    /// <summary>
    /// Schedules a one-shot action to run after <paramref name="delayMs"/>.
    /// </summary>
    void Schedule(string key, Action action, int delayMs);

    /// <summary>
    /// Schedules a one-shot callback via <see cref="Vintagestory.API.Common.ICoreAPI.Event.RegisterCallback"/>.
    /// </summary>
    void ScheduleCallback(string key, Action action, int delayMs);

    /// <summary>
    /// Cancels a pending callback by key.
    /// </summary>
    void CancelCallback(string key);

    /// <summary>
    /// Returns true when a callback with the given key is pending.
    /// </summary>
    bool IsCallbackPending(string key);

    /// <summary>
    /// Cancels all callbacks whose key starts with <paramref name="prefix"/>.
    /// </summary>
    void CancelCallbacksByPrefix(string prefix);

    /// <summary>
    /// Coalesces repeated calls for the same key into a single execution.
    /// </summary>
    void Coalesce(string key, Action action, int windowMs, int maxDelayMs = -1);

    /// <summary>
    /// Queues an action to run at the end of the current game tick.
    /// </summary>
    void AtEndOfTick(Action action);

    /// <summary>
    /// Cancels a pending task by key.
    /// </summary>
    void Cancel(string key);

    /// <summary>
    /// Returns true when a task with the given key is pending.
    /// </summary>
    bool IsPending(string key);
}
