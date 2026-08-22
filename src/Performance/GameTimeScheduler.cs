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
public class GameTimeScheduler : ModSystem
{
    private ICoreServerAPI? _sapi;
    private long _tickListenerId;
    private double _lastTotalHours;
    private readonly List<GameSchedule> _schedules = new();
    private static readonly object _syncLock = new();

    /// <summary>
    /// Enables or disables the scheduler at runtime. When disabled, schedules
    /// are not checked but are not removed.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// How often (in ms) the scheduler checks for due schedules.
    /// </summary>
    public static int CheckIntervalMs { get; set; } = 2000;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        _instance = this;
        _lastTotalHours = api.World.Calendar.TotalHours;
        _tickListenerId = api.Event.RegisterGameTickListener(OnTick, CheckIntervalMs);
        api.Logger.Notification("[ArcanumLib] GameTimeScheduler started.");
    }

    public override void Dispose()
    {
        if (_sapi != null && _tickListenerId != 0)
        {
            _sapi.Event.UnregisterGameTickListener(_tickListenerId);
            _tickListenerId = 0;
        }

        lock (_syncLock)
        {
            _schedules.Clear();
        }

        _sapi = null;
        base.Dispose();
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
            _instance?._schedules.Add(schedule);
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
            _instance?._schedules.Add(schedule);
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

        double now = _instance?._sapi?.World.Calendar.TotalHours ?? 0;
        var schedule = new GameSchedule
        {
            Id = NextId(),
            Mode = ScheduleMode.AfterHours,
            TargetTotalHours = now + hours,
            Action = action
        };

        lock (_syncLock)
        {
            _instance?._schedules.Add(schedule);
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
            if (_instance == null) return;
            _instance._schedules.RemoveAll(s => s.Id == scheduleId);
        }
    }

    /// <summary>
    /// Cancels all scheduled actions.
    /// </summary>
    public static void CancelAll()
    {
        lock (_syncLock)
        {
            _instance?._schedules.Clear();
        }
    }

    /// <summary>
    /// Returns the number of active schedules.
    /// </summary>
    public static int GetScheduleCount()
    {
        lock (_syncLock)
        {
            return _instance?._schedules.Count ?? 0;
        }
    }

    private static GameTimeScheduler? _instance;
    private static int _nextId;

    private static int NextId() => System.Threading.Interlocked.Increment(ref _nextId);

    private void OnTick(float dt)
    {
        if (!IsEnabled || _sapi?.World?.Calendar == null) return;

        double currentHours = _sapi.World.Calendar.TotalHours;
        double prevHours = _lastTotalHours;
        _lastTotalHours = currentHours;

        List<GameSchedule> snapshot;
        lock (_syncLock)
        {
            snapshot = new List<GameSchedule>(_schedules);
        }

        var toRemove = new List<int>();
        var toRun = new List<(GameSchedule schedule, double hours)>();

        foreach (var schedule in snapshot)
        {
            bool due = false;

            switch (schedule.Mode)
            {
                case ScheduleMode.Daily:
                {
                    // Fire when the in-game hour crosses the target hour.
                    int prevHour = (int)prevHours % 24;
                    int curHour = (int)currentHours % 24;
                    if (prevHour != curHour && curHour == schedule.TargetHour)
                        due = true;
                    break;
                }

                case ScheduleMode.Hourly:
                {
                    // Fire when the minute within the current hour crosses the target.
                    double prevFraction = prevHours - Math.Floor(prevHours);
                    double curFraction = currentHours - Math.Floor(currentHours);
                    int prevMinute = (int)(prevFraction * 60);
                    int curMinute = (int)(curFraction * 60);
                    if (prevMinute != curMinute && curMinute == schedule.TargetMinute)
                        due = true;
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

        if (toRemove.Count > 0)
        {
            lock (_syncLock)
            {
                foreach (var id in toRemove)
                    _schedules.RemoveAll(s => s.Id == id);
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
