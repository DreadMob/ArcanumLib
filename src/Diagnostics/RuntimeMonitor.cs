using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Vintagestory.API.Server;

namespace ArcanumLib.Diagnostics;

/// <summary>
/// Snapshot of runtime metrics captured at a single monitoring interval.
/// </summary>
internal sealed class MonitorSnapshot
{
    /// <summary>Gets or sets the timestamp the snapshot was taken.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the tick overhead in milliseconds.</summary>
    public double TickMs { get; set; }

    /// <summary>Gets or sets the captured private memory in bytes.</summary>
    public long MemoryBytes { get; set; }

    /// <summary>Gets or sets the number of active entities.</summary>
    public int ActiveEntities { get; set; }

    /// <summary>Gets or sets the number of loaded chunks.</summary>
    public int LoadedChunks { get; set; }

    /// <summary>Gets or sets the number of players online.</summary>
    public int PlayersOnline { get; set; }
}

/// <summary>
/// Periodically samples runtime metrics (tick overhead, memory, entity and
/// player counts) and produces a formatted monitor report. Owned by
/// <see cref="DiagnosticsModSystem" /> and started/stopped with the server.
/// </summary>
internal sealed class RuntimeMonitor
{
    private ICoreServerAPI? _sapi;
    private long _monitorListenerId;
    private long _lastMonitorTickMs;
    private double _lastTickDurationMs;
    private long _monitorTickCount;
    private double _maxTickDurationMs;
    private double _totalTickDurationMs;
    private long _lastMemoryBytes;
    private long _peakMemoryBytes;
    private readonly List<MonitorSnapshot> _monitorHistory = new();
    private const int MaxMonitorHistory = 60;

    /// <summary>Gets the total number of ticks sampled since monitoring started.</summary>
    internal long MonitorTickCount => _monitorTickCount;

    /// <summary>Gets the cumulative tick overhead in milliseconds across all samples.</summary>
    internal double TotalTickDurationMs => _totalTickDurationMs;

    /// <summary>Gets the maximum tick overhead observed in milliseconds.</summary>
    internal double MaxTickDurationMs => _maxTickDurationMs;

    /// <summary>Gets the most recently sampled private memory in bytes.</summary>
    internal long LastMemoryBytes => _lastMemoryBytes;

    /// <summary>Gets the peak private memory observed in bytes.</summary>
    internal long PeakMemoryBytes => _peakMemoryBytes;

    /// <summary>
    /// Starts the periodic runtime monitor that samples tick time, memory, and
    /// entity counts every 5 seconds.
    /// </summary>
    /// <param name="sapi">The server API instance.</param>
    public void StartMonitoring(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        _lastMonitorTickMs = sapi.World.ElapsedMilliseconds;
        _monitorListenerId = sapi.Event.RegisterGameTickListener(OnMonitorTick, 5000);
    }

    /// <summary>
    /// Stops the monitor listener and releases the server API reference.
    /// </summary>
    /// <param name="sapi">The server API instance, or null if already cleared.</param>
    public void StopMonitoring(ICoreServerAPI? sapi)
    {
        if (sapi != null && _monitorListenerId != 0)
        {
            try
            {
                sapi.Event.UnregisterGameTickListener(_monitorListenerId);
            }
            catch (Exception ex)
            {
                sapi.Logger?.Warning("[ArcanumLib/Diagnostics] Failed to unregister monitor listener: {0}", ex.Message);
            }
        }

        _monitorListenerId = 0;
        _sapi = null;
    }

    /// <summary>
    /// Called every 5 seconds to sample runtime metrics.
    /// </summary>
    /// <param name="deltaTime">The delta time value.</param>
    private void OnMonitorTick(float deltaTime)
    {
        if (_sapi?.World == null) return;

        long now = _sapi.World.ElapsedMilliseconds;
        double tickMs = now - _lastMonitorTickMs;
        _lastMonitorTickMs = now;

        // Subtract the 5s interval to get actual tick overhead
        _lastTickDurationMs = tickMs - 5000;
        if (_lastTickDurationMs < 0) _lastTickDurationMs = 0;

        _monitorTickCount++;
        _totalTickDurationMs += _lastTickDurationMs;
        if (_lastTickDurationMs > _maxTickDurationMs)
            _maxTickDurationMs = _lastTickDurationMs;

        long mem = Process.GetCurrentProcess().PrivateMemorySize64;
        _lastMemoryBytes = mem;
        if (mem > _peakMemoryBytes) _peakMemoryBytes = mem;

        // Sample entity/chunk/player counts
        var snapshot = new MonitorSnapshot
        {
            Timestamp = DateTime.Now,
            TickMs = _lastTickDurationMs,
            MemoryBytes = mem,
            ActiveEntities = _sapi.World.LoadedEntities?.Count ?? 0,
            LoadedChunks = 0,
            PlayersOnline = _sapi.Server?.Players?.Length ?? 0
        };

        _monitorHistory.Add(snapshot);
        if (_monitorHistory.Count > MaxMonitorHistory)
            _monitorHistory.RemoveAt(0);
    }

    /// <summary>
    /// Returns a formatted runtime monitor report for the
    /// <c>/arcanum monitor</c> command.
    /// </summary>
    /// <returns>The monitor report string.</returns>
    public string GetMonitorReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ArcanumLib Runtime Monitor ===");
        sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (_monitorHistory.Count == 0)
        {
            sb.AppendLine("  (no samples yet — wait a few seconds)");
            return sb.ToString();
        }

        sb.AppendLine($"  Samples: {_monitorHistory.Count}/{MaxMonitorHistory}");
        sb.AppendLine($"  Total monitored ticks: {_monitorTickCount}");
        double avgTick = _monitorTickCount > 0 ? _totalTickDurationMs / _monitorTickCount : 0;
        sb.AppendLine($"  Avg tick overhead: {avgTick:F2} ms");
        sb.AppendLine($"  Max tick overhead: {_maxTickDurationMs:F2} ms");
        sb.AppendLine($"  Current memory: {_lastMemoryBytes / 1024 / 1024} MB");
        sb.AppendLine($"  Peak memory: {_peakMemoryBytes / 1024 / 1024} MB");
        sb.AppendLine();

        var latest = _monitorHistory[^1];
        sb.AppendLine("-- Latest Sample --");
        sb.AppendLine($"  Time: {latest.Timestamp:HH:mm:ss}");
        sb.AppendLine($"  Tick overhead: {latest.TickMs:F2} ms");
        sb.AppendLine($"  Memory: {latest.MemoryBytes / 1024 / 1024} MB");
        sb.AppendLine($"  Active entities: {latest.ActiveEntities}");
        sb.AppendLine($"  Players online: {latest.PlayersOnline}");
        sb.AppendLine();

        // Recent trend (last 10 samples)
        sb.AppendLine("-- Recent Trend (last 10) --");
        var recent = _monitorHistory.Skip(Math.Max(0, _monitorHistory.Count - 10)).ToList();
        foreach (var s in recent)
        {
            string tickBar = new('#', Math.Min(20, (int)(s.TickMs / 2)));
            sb.AppendLine($"  {s.Timestamp:HH:mm:ss} | {s.TickMs,6:F1} ms | {tickBar}");
        }

        sb.AppendLine();
        sb.AppendLine("=== End Monitor ===");
        return sb.ToString();
    }
}
