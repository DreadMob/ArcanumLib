using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Performance;

/// <summary>
/// Schedules actions based on in-game time rather than real time.
/// Supports recurring schedules (e.g. "every day at 6:00", "every hour on the hour")
/// and one-shot schedules at a specific in-game hour.
/// </summary>
/// <remarks>
/// Uses <see cref="ICoreServerAPI.World.Calendar"/> for time queries and a game
/// tick listener to check for due schedules. This is server-side only because
/// in-game time is authoritative on the server.
/// </remarks>
public static class GameTimeScheduler
{
    private static ICoreServerAPI? _sapi;
    private static long _tickListenerId;
    private static double _lastTotalHours;
    private static readonly List<GameSchedule> _schedules = new();
    private static readonly object _syncLock = new();
    private static bool _started;

    /// <summary>
    /// Enables or disables the scheduler at runtime. When disabled, schedules
    /// are not checked but are not removed.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// How often (in ms) the scheduler checks for due schedules.
    /// </summary>
    public static int CheckIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Starts the in-game time scheduler on the server.
    /// </summary>
    public static void Start(ICoreServerAPI api)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));

        lock (_syncLock)
        {
            if (_started)
            {
                Stop();
            }

            _started = true;
            _sapi = api;
            // Calendar may be null during start on some run phases.
            // Use -1 as a sentinel; OnTick will capture the first real hour.
            _lastTotalHours = api.World?.Calendar?.TotalHours ?? -1.0;
            _tickListenerId = api.Event.RegisterGameTickListener(OnTick, CheckIntervalMs);
            api.Logger.Notification("[ArcanumLib] GameTimeScheduler started.");
        }
    }

    /// <summary>
    /// Stops the in-game time scheduler and clears all pending schedules.
    /// </summary>
    public static void Stop()
    {
        lock (_syncLock)
        {
            if (_sapi != null && _tickListenerId != 0)
            {
                _sapi.Event.UnregisterGameTickListener(_tickListenerId);
                _tickListenerId = 0;
            }

            _schedules.Clear();
            _started = false;
            _sapi = null;
        }
    }

    /// <summary>
    /// Schedules a recurring action that fires every in-game day at the given hour.
    /// </summary>
    /// <param name="hour">Hour of the in-game day (0-23).</param>
    /// <param name="action">The action to run. Receives the current total hours.</param>
    /// <returns>A schedule ID that can be used to cancel with <see cref="Cancel"/>.</returns>
    public static int ScheduleDaily(int hour, Action<double> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (hour < 0 || hour > 23) throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be 0-23.");

        var schedule = new GameSchedule
        {
            Id = NextId(),
            Mode = ScheduleMode.Daily,
            TargetHour = hour,
            Action = action
        };

        lock (_syncLock)
        {
            if (!_started)
                throw new InvalidOperationException("GameTimeScheduler has not been started.");
            _schedules.Add(schedule);
        }

        return schedule.Id;
    }

    /// <summary>
    /// Schedules a recurring action that fires every in-game hour at the given minute mark.
    /// </summary>
    /// <param name="minute">Minute within the hour (0-59). Use 0 for on-the-hour.</param>
    /// <param name="action">The action to run.</param>
    public static int ScheduleHourly(int minute, Action<double> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (minute < 0 || minute > 59) throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be 0-59.");

        var schedule = new GameSchedule
        {
            Id = NextId(),
            Mode = ScheduleMode.Hourly,
            TargetMinute = minute,
            Action = action
        };

        lock (_syncLock)
        {
            if (!_started)
                throw new InvalidOperationException("GameTimeScheduler has not been started.");
            _schedules.Add(schedule);
        }

        return schedule.Id;
    }

    /// <summary>
    /// Schedules a one-shot action to fire after the given number of in-game hours
    /// have elapsed from the time of scheduling.
    /// </summary>
    public static int ScheduleAfterHours(double hours, Action<double> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be positive.");

        var schedule = new GameSchedule
        {
            Id = NextId(),
            Mode = ScheduleMode.AfterHours,
            Action = action
        };

        lock (_syncLock)
        {
            if (!_started)
                throw new InvalidOperationException("GameTimeScheduler has not been started.");
            schedule.TargetTotalHours = (_sapi?.World.Calendar.TotalHours ?? 0) + hours;
            _schedules.Add(schedule);
        }

        return schedule.Id;
    }

    /// <summary>
    /// Cancels a scheduled action by ID.
    /// </summary>
    public static void Cancel(int scheduleId)
    {
        lock (_syncLock)
        {
            if (!_started) return;
            _schedules.RemoveAll(s => s.Id == scheduleId);
        }
    }

    /// <summary>
    /// Cancels all scheduled actions.
    /// </summary>
    public static void CancelAll()
    {
        lock (_syncLock)
        {
            _schedules.Clear();
        }
    }

    /// <summary>
    /// Returns the number of active schedules.
    /// </summary>
    public static int GetScheduleCount()
    {
        lock (_syncLock)
        {
            return _started ? _schedules.Count : 0;
        }
    }

    private static int _nextId;

    private static int NextId() => System.Threading.Interlocked.Increment(ref _nextId);

    private static void OnTick(float dt)
    {
        if (!IsEnabled || _sapi?.World?.Calendar == null) return;

        double currentHours = _sapi.World.Calendar.TotalHours;

        // First real tick: just capture the baseline to avoid firing all schedules at once.
        if (_lastTotalHours < 0)
        {
            _lastTotalHours = currentHours;
            return;
        }

        double prevHours = _lastTotalHours;
        _lastTotalHours = currentHours;

        var toRun = new List<(GameSchedule schedule, double hours)>();
        var toRemove = new HashSet<int>();

        lock (_syncLock)
        {
            foreach (var schedule in _schedules)
            {
                bool due = false;

                switch (schedule.Mode)
                {
                    case ScheduleMode.Daily:
                    {
                        // Fire when the next daily target hour is crossed, even if time jumps.
                        double dayStart = Math.Floor(prevHours / 24.0) * 24.0;
                        double target = dayStart + schedule.TargetHour;
                        if (target <= prevHours)
                            target += 24.0;

                        due = currentHours >= target;
                        break;
                    }

                    case ScheduleMode.Hourly:
                    {
                        // Fire when the next target minute within the hour is crossed.
                        double hourStart = Math.Floor(prevHours);
                        double target = hourStart + schedule.TargetMinute / 60.0;
                        if (target <= prevHours)
                            target += 1.0;

                        due = currentHours >= target;
                        break;
                    }

                    case ScheduleMode.AfterHours:
                    {
                        if (currentHours >= schedule.TargetTotalHours)
                        {
                            due = true;
                            toRemove.Add(schedule.Id);
                        }
                        break;
                    }
                }

                if (due)
                    toRun.Add((schedule, currentHours));
            }

            if (toRemove.Count > 0)
            {
                _schedules.RemoveAll(s => toRemove.Contains(s.Id));
            }
        }

        foreach (var (schedule, hours) in toRun)
        {
            try
            {
                schedule.Action(hours);
            }
            catch (Exception ex)
            {
                _sapi.Logger?.Warning("[ArcanumLib] GameTimeScheduler action {0} failed: {1}", schedule.Id, ex.Message);
            }
        }
    }

    private enum ScheduleMode
    {
        Daily,
        Hourly,
        AfterHours
    }

    private sealed class GameSchedule
    {
        public int Id;
        public ScheduleMode Mode;
        public int TargetHour;
        public int TargetMinute;
        public double TargetTotalHours;
        public Action<double> Action = null!;
    }
}
