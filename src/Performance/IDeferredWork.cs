using System;

namespace ArcanumLib.Performance;

/// <summary>
/// Side-scoped deferred work scheduler.
/// </summary>
public interface IDeferredWork
{
    /// <summary>
    /// Schedules a one-shot action to run after <paramref name="delayMs" />.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="action">The action value.</param>
    /// <param name="delayMs">The delay ms value.</param>
    void Schedule(string key, Action action, int delayMs);

    /// <summary>
    /// Schedules a one-shot callback via the API's <c>RegisterCallback</c> mechanism.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="action">The action value.</param>
    /// <param name="delayMs">The delay ms value.</param>
    void ScheduleCallback(string key, Action action, int delayMs);

    /// <summary>
    /// Cancels a pending callback by key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    void CancelCallback(string key);

    /// <summary>
    /// Returns true when a callback with the given key is pending.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>true if callback pending; otherwise, false.</returns>
    bool IsCallbackPending(string key);

    /// <summary>
    /// Cancels all callbacks whose key starts with <paramref name="prefix" />.
    /// </summary>
    /// <param name="prefix">The prefix value.</param>
    void CancelCallbacksByPrefix(string prefix);

    /// <summary>
    /// Coalesces repeated calls for the same key into a single execution.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="action">The action value.</param>
    /// <param name="windowMs">The window ms value.</param>
    /// <param name="maxDelayMs">The max delay ms value.</param>
    void Coalesce(string key, Action action, int windowMs, int maxDelayMs = -1);

    /// <summary>
    /// Queues an action to run at the end of the current game tick.
    /// </summary>
    /// <param name="action">The action value.</param>
    void AtEndOfTick(Action action);

    /// <summary>
    /// Cancels a pending task by key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    void Cancel(string key);

    /// <summary>
    /// Returns true when a task with the given key is pending.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>true if pending; otherwise, false.</returns>
    bool IsPending(string key);
}
