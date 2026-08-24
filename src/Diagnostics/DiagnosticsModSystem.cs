using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ArcanumLib.Actions;
using ArcanumLib.Core;
using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Logging;
using ArcanumLib.Progression;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Diagnostics;

/// <summary>
/// Diagnostic ModSystem that validates ArcanumLib module and service registration,
/// EventBus health, dependency chain integrity, and runtime performance after all
/// other ModSystems have started. Writes results to the server log, a dedicated
/// diagnostics log file, and exposes <c>/arcanum diagnose</c> and
/// <c>/arcanum monitor</c> commands for on-demand checks.
/// </summary>
public class DiagnosticsModSystem : ModSystem
{
    private ICoreServerAPI? _sapi;
    private string? _logPath;
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

    /// <summary>Runs after all other ArcanumLib systems so registrations are complete.</summary>
    public override double ExecuteOrder() => 1000;

    /// <summary>Server-side only: diagnostics are authoritative on the server.</summary>
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    /// <summary>
    /// Registers the diagnostics commands, starts runtime monitoring, and runs
    /// the first validation pass.
    /// </summary>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        _logPath = Path.Combine(sapi.GetOrCreateDataPath("Logs"), "arcanumlib-diagnostics.log");

        RegisterCommand(sapi);
        StartMonitoring(sapi);
        // Delay the first diagnostics pass so event-driven systems have a chance
        // to publish at least once (player join, objective accepted, etc.). Running
        // immediately at startup produces false "never-published" warnings for
        // events that simply haven't fired yet.
        sapi.Event.RegisterCallback(_ => RunDiagnostics(sapi), 30000);
    }

    // ── Static diagnostics ──

    /// <summary>
    /// Runs a full diagnostics pass and logs the results to the server log and the
    /// diagnostics log file. Returns the report text.
    /// </summary>
    public string RunDiagnostics(ICoreServerAPI sapi)
    {
        var report = new StringBuilder();
        report.AppendLine("=== ArcanumLib Diagnostics ===");
        report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();

        int errors = 0;
        int warnings = 0;

        // 1. Core API registration
        var coreApi = ArcanumServices.Get<ICoreAPI>();
        var serverApi = ArcanumServices.Get<ICoreServerAPI>();
        if (coreApi == null)
        {
            report.AppendLine("[ERROR] ICoreAPI is not registered in ArcanumServices.");
            errors++;
        }
        if (serverApi == null)
        {
            report.AppendLine("[ERROR] ICoreServerAPI is not registered in ArcanumServices.");
            errors++;
        }

        // 2. Expected services
        report.AppendLine("-- Services --");
        var actionRegistry = ArcanumServices.Get<ActionRegistryService>();
        var actionExecutor = ArcanumServices.Get<ActionExecutorService>();
        var statusEffect = ArcanumServices.Get<StatusEffectService>();
        var logger = ArcanumServices.Get<CategorizedLogger>();

        if (actionRegistry != null)
            report.AppendLine("  [OK]   ActionRegistryService");
        else
        {
            report.AppendLine("  [FAIL] ActionRegistryService — required service not registered");
            errors++;
        }

        if (actionExecutor != null)
            report.AppendLine("  [OK]   ActionExecutorService");
        else
        {
            report.AppendLine("  [FAIL] ActionExecutorService — required service not registered");
            errors++;
        }

        if (statusEffect != null)
            report.AppendLine("  [OK]   StatusEffectService");
        else
        {
            report.AppendLine("  [WARN] StatusEffectService — optional service not registered");
            warnings++;
        }

        if (logger != null)
            report.AppendLine("  [OK]   CategorizedLogger");
        else
        {
            report.AppendLine("  [WARN] CategorizedLogger — optional service not registered");
            warnings++;
        }
        report.AppendLine();

        // 3. Expected ModSystems
        report.AppendLine("-- ModSystems --");
        var expectedSystems = new (string ClassName, bool Required)[]
        {
            ("ArcanumLibModSystem", true),
            ("ArcanumDataModSystem", true),
            ("ArcanumPerformanceModSystem", true),
            ("StatusEffectModSystem", false),
        };

        var loadedSystems = sapi.ModLoader.Systems.Select(s => s.GetType().Name).ToHashSet();
        foreach (var (className, required) in expectedSystems)
        {
            if (loadedSystems.Contains(className))
            {
                report.AppendLine($"  [OK]   {className}");
            }
            else if (required)
            {
                report.AppendLine($"  [FAIL] {className} — required ModSystem not loaded");
                errors++;
            }
            else
            {
                report.AppendLine($"  [WARN] {className} — optional ModSystem not loaded");
                warnings++;
            }
        }
        report.AppendLine();

        // 4. PityTracker singleton
        report.AppendLine("-- Singletons --");
        if (PityTracker.Current != null)
        {
            report.AppendLine("  [OK]   PityTracker.Current");
        }
        else
        {
            report.AppendLine("  [WARN] PityTracker.Current — not initialized");
            warnings++;
        }
        report.AppendLine();

        // 5. EventBus health
        errors += CheckEventBus(report);

        // 6. Dependency chain analysis
        errors += CheckDependencies(sapi, report);

        // 7. Dependent mods listing
        report.AppendLine("-- Dependent Mods --");
        var dependentMods = new List<(string ModId, string Name, string Version)>();
        try
        {
            var mods = sapi.ModLoader.Mods;
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    if (mod?.Info?.ModID == null) continue;
                    if (string.Equals(mod.Info.ModID, "arcanumlib", StringComparison.OrdinalIgnoreCase)) continue;

                    var deps = mod.Info.Dependencies;
                    if (deps == null || deps.Count == 0) continue;
                    bool dependsOnArcanum = deps.Any(d =>
                        string.Equals(d.ModID, "arcanumlib", StringComparison.OrdinalIgnoreCase));
                    if (!dependsOnArcanum) continue;

                    dependentMods.Add((mod.Info.ModID, mod.Info.Name ?? mod.Info.ModID, mod.Info.Version ?? "?"));
                }
            }
        }
        catch (Exception ex)
        {
            report.AppendLine($"  [ERROR] Failed to enumerate mods: {ex.Message}");
            errors++;
        }

        if (dependentMods.Count == 0)
        {
            report.AppendLine("  (no dependent mods loaded)");
        }
        else
        {
            foreach (var (modId, name, version) in dependentMods)
            {
                report.AppendLine($"  [INFO] {modId} v{version} — {name}");
            }
        }
        report.AppendLine();

        // 8. Runtime monitor snapshot
        report.AppendLine("-- Runtime Monitor --");
        if (_monitorTickCount > 0)
        {
            double avgTick = _totalTickDurationMs / _monitorTickCount;
            report.AppendLine($"  [INFO] Monitored ticks: {_monitorTickCount}");
            report.AppendLine($"  [INFO] Avg tick: {avgTick:F2} ms");
            report.AppendLine($"  [INFO] Max tick: {_maxTickDurationMs:F2} ms");
            if (_maxTickDurationMs > 50)
            {
                report.AppendLine($"  [WARN] Max tick exceeded 50ms threshold — possible performance issue");
                warnings++;
            }

            long currentMem = Process.GetCurrentProcess().PrivateMemorySize64;
            report.AppendLine($"  [INFO] Memory: {currentMem / 1024 / 1024} MB");
            report.AppendLine($"  [INFO] Peak memory: {_peakMemoryBytes / 1024 / 1024} MB");
            if (_peakMemoryBytes > 0 && currentMem > _peakMemoryBytes * 2 && _peakMemoryBytes > 100 * 1024 * 1024)
            {
                report.AppendLine($"  [WARN] Memory doubled since peak — possible leak");
                warnings++;
            }
        }
        else
        {
            report.AppendLine("  (monitor not yet running — use /arcanum monitor for live data)");
        }
        report.AppendLine();

        // 9. Summary
        report.AppendLine("-- Summary --");
        report.AppendLine($"  Errors:   {errors}");
        report.AppendLine($"  Warnings: {warnings}");
        report.AppendLine($"  Dependent mods: {dependentMods.Count}");
        report.AppendLine($"  EventBus subscriptions: {EventBus.ActiveSubscriptionCount()}");
        report.AppendLine($"  Result: {(errors == 0 ? "PASS" : "FAIL")}");
        report.AppendLine("=== End Diagnostics ===");

        var text = report.ToString();

        // Log to server console
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains("[FAIL]") || line.Contains("[ERROR]"))
                sapi.Logger.Error("[ArcanumLib/Diagnostics] {0}", line.Trim());
            else if (line.Contains("[WARN]"))
                sapi.Logger.Warning("[ArcanumLib/Diagnostics] {0}", line.Trim());
            else
                sapi.Logger.Notification("[ArcanumLib/Diagnostics] {0}", line.Trim());
        }

        // Write to diagnostics log file
        WriteLogFile(text);

        return text;
    }

    // ── EventBus health checks ──

    /// <summary>
    /// Checks EventBus subscription health: active count, disposed-but-not-removed,
    /// dangling subscriptions (subscribed but never published), and slow handlers.
    /// </summary>
    private int CheckEventBus(StringBuilder report)
    {
        int warnings = 0;
        report.AppendLine("-- EventBus Health --");

        int activeCount = EventBus.ActiveSubscriptionCount();
        report.AppendLine($"  [INFO] Active subscriptions: {activeCount}");

        var subs = EventBus.GetDiagnostics();
        int disposedButTracked = subs.Count(s => s.IsDisposed);
        if (disposedButTracked > 0)
        {
            report.AppendLine($"  [WARN] {disposedButTracked} disposed subscriptions still tracked — possible leak");
            warnings++;
        }

        // Slow handlers (> 10ms average)
        var slowHandlers = subs.Where(s => s.AverageInvocationMs > 10 && s.InvocationCount > 0).ToList();
        foreach (var s in slowHandlers)
        {
            report.AppendLine($"  [WARN] Slow handler: {s.EventType.Name}[{s.Tag}] avg {s.AverageInvocationMs:F1} ms over {s.InvocationCount} calls");
            warnings++;
        }

        // Handlers with errors
        var errorHandlers = subs.Where(s => !string.IsNullOrEmpty(s.LastError)).ToList();
        foreach (var s in errorHandlers)
        {
            report.AppendLine($"  [WARN] Handler {s.EventType.Name}[{s.Tag}] last error: {s.LastError}");
            warnings++;
        }

        // Dangling subscriptions (subscribed but never published)
        var dangling = EventBus.GetDanglingSubscriptions();
        if (dangling.Count > 0)
        {
            report.AppendLine($"  [WARN] {dangling.Count} subscription(s) on never-published tags (possible typos):");
            foreach (var d in dangling.Take(10))
                report.AppendLine($"         - {d}");
            if (dangling.Count > 10)
                report.AppendLine($"         ... and {dangling.Count - 10} more");
            warnings++;
        }

        if (warnings == 0)
            report.AppendLine("  [OK]   No EventBus issues detected");

        report.AppendLine();
        return 0; // warnings don't count as errors
    }

    // ── Dependency chain analysis ──

    /// <summary>
    /// Checks dependency chains: version conflicts against the loaded ArcanumLib,
    /// missing dependencies, and load-order issues.
    /// </summary>
    private int CheckDependencies(ICoreServerAPI sapi, StringBuilder report)
    {
        int errors = 0;
        int warnings = 0;
        report.AppendLine("-- Dependency Analysis --");

        // Find the loaded arcanumlib version
        string? arcanumVersion = null;
        var mods = sapi.ModLoader.Mods;
        if (mods != null)
        {
            foreach (var mod in mods)
            {
                if (mod?.Info is { ModID: not null } info &&
                    string.Equals(info.ModID, "arcanumlib", StringComparison.OrdinalIgnoreCase))
                {
                    arcanumVersion = info.Version ?? "";
                    break;
                }
            }
        }

        if (arcanumVersion == null)
        {
            report.AppendLine("  [FAIL] arcanumlib is not loaded — cannot check dependency versions");
            errors++;
            report.AppendLine();
            return errors;
        }

        report.AppendLine($"  [INFO] arcanumlib version: {arcanumVersion}");

        // Check each mod that depends on arcanumlib for version conflicts
        if (mods != null)
        {
            var loadedModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in mods)
                if (m?.Info?.ModID != null)
                    loadedModIds.Add(m.Info.ModID);

            foreach (var mod in mods)
            {
                if (mod?.Info?.ModID == null) continue;
                var deps = mod.Info.Dependencies;
                if (deps == null || deps.Count == 0) continue;

                foreach (var dep in deps)
                {
                    // Check arcanumlib version specifically
                    if (string.Equals(dep.ModID, "arcanumlib", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(dep.Version) && !IsVersionSatisfied(arcanumVersion, dep.Version))
                        {
                            report.AppendLine($"  [FAIL] {mod.Info.ModID} requires arcanumlib@{dep.Version} but {arcanumVersion} is loaded");
                            errors++;
                        }
                    }

                    // Check missing dependencies
                    if (!loadedModIds.Contains(dep.ModID) &&
                        !string.Equals(dep.ModID, "game", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(dep.ModID, "survival", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(dep.ModID, "creative", StringComparison.OrdinalIgnoreCase))
                    {
                        report.AppendLine($"  [FAIL] {mod.Info.ModID} requires {dep.ModID}@{dep.Version} but it is not loaded");
                        errors++;
                    }
                }
            }
        }

        // Check ExecuteOrder issues — mods that depend on arcanumlib should not run before it
        var arcanumSystems = sapi.ModLoader.Systems
            .Where(s => s.GetType().Assembly.GetName().Name?.Contains("ArcanumLib") == true)
            .ToList();
        foreach (var sys in arcanumSystems)
        {
            try
            {
                double order = sys.ExecuteOrder();
                if (order < -1000)
                {
                    report.AppendLine($"  [WARN] {sys.GetType().Name} has ExecuteOrder {order} — may run before dependents are ready");
                    warnings++;
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"  [WARN] Failed to check ExecuteOrder for {sys.GetType().Name}: {ex.Message}");
                warnings++;
            }
        }

        if (errors == 0 && warnings == 0)
            report.AppendLine("  [OK]   No dependency issues detected");

        report.AppendLine();
        return errors;
    }

    /// <summary>
    /// Simple semver pre-release-aware version satisfaction check.
    /// Returns true if <paramref name="installed"/> satisfies the <paramref name="required"/> minimum.
    /// Pre-release versions (e.g. "1.0.0-rc1") are considered lower than the release ("1.0.0").
    /// </summary>
    private static bool IsVersionSatisfied(string installed, string required)
    {
        if (string.IsNullOrEmpty(required)) return true;
        if (string.IsNullOrEmpty(installed)) return false;

        // Strip pre-release suffix for comparison
        string installedCore = installed.Split('-')[0];
        string requiredCore = required.Split('-')[0];

        // Parse major.minor.patch
        if (!TryParseVersion(installedCore, out var instMajor, out var instMinor, out var instPatch))
            return false;
        if (!TryParseVersion(requiredCore, out var reqMajor, out var reqMinor, out var reqPatch))
            return false;

        // Compare core versions
        if (instMajor != reqMajor) return instMajor > reqMajor;
        if (instMinor != reqMinor) return instMinor > reqMinor;
        if (instPatch != reqPatch) return instPatch > reqPatch;

        // Core versions are equal — pre-release matters
        bool installedPre = installed.Contains('-');
        bool requiredPre = required.Contains('-');

        // If required is pre-release and installed is release, installed is higher → satisfied
        if (requiredPre && !installedPre) return true;
        // If both are pre-release, compare the pre-release strings
        if (requiredPre && installedPre)
        {
            string instPre = installed.Substring(installed.IndexOf('-') + 1);
            string reqPre = required.Substring(required.IndexOf('-') + 1);
            return string.Compare(instPre, reqPre, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        // If required is release and installed is pre-release, installed is lower → not satisfied
        if (!requiredPre && installedPre) return false;

        // Both are release, equal versions → satisfied
        return true;
    }

    private static bool TryParseVersion(string s, out int major, out int minor, out int patch)
    {
        major = minor = patch = 0;
        var parts = s.Split('.');
        if (parts.Length < 1 || !int.TryParse(parts[0], out major)) return false;
        if (parts.Length >= 2 && !int.TryParse(parts[1], out minor)) return false;
        if (parts.Length >= 3 && !int.TryParse(parts[2], out patch)) return false;
        return true;
    }

    // ── Runtime monitoring ──

    /// <summary>
    /// Snapshot of runtime metrics at a single monitoring interval.
    /// </summary>
    private sealed class MonitorSnapshot
    {
        public DateTime Timestamp;
        public double TickMs;
        public long MemoryBytes;
        int _activeEntities;
        int _loadedChunks;
        int _playersOnline;

        public int ActiveEntities { get => _activeEntities; set => _activeEntities = value; }
        public int LoadedChunks { get => _loadedChunks; set => _loadedChunks = value; }
        public int PlayersOnline { get => _playersOnline; set => _playersOnline = value; }
    }

    /// <summary>
    /// Starts the periodic runtime monitor that samples tick time, memory, and entity counts.
    /// </summary>
    private void StartMonitoring(ICoreServerAPI sapi)
    {
        _lastMonitorTickMs = sapi.World.ElapsedMilliseconds;
        _monitorListenerId = sapi.Event.RegisterGameTickListener(OnMonitorTick, 5000);
    }

    /// <summary>
    /// Called every 5 seconds to sample runtime metrics.
    /// </summary>
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
    /// Returns a formatted runtime monitor report for the <c>/arcanum monitor</c> command.
    /// </summary>
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

    // ── Logging ──

    /// <summary>
    /// Appends the report to the diagnostics log file.
    /// </summary>
    private void WriteLogFile(string text)
    {
        if (string.IsNullOrEmpty(_logPath)) return;

        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(_logPath, text + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _sapi?.Logger?.Warning("[ArcanumLib/Diagnostics] Failed to write log file: {0}", ex.Message);
        }
    }

    // ── Commands ──

    /// <summary>
    /// Registers the <c>/arcanum diagnose</c> and <c>/arcanum monitor</c> chat commands.
    /// </summary>
    private void RegisterCommand(ICoreServerAPI sapi)
    {
        try
        {
            var cmd = sapi.ChatCommands.Create("arcanum")
                .WithDescription("ArcanumLib diagnostics and monitoring")
                .RequiresPrivilege("controlserver");

            cmd.BeginSubCommand("diagnose")
                .WithDescription("Run ArcanumLib diagnostics and show the report")
                .HandleWith(_ =>
                {
                    var report = RunDiagnostics(sapi);
                    var summary = ExtractSummary(report);
                    return TextCommandResult.Success(summary);
                });

            cmd.BeginSubCommand("monitor")
                .WithDescription("Show runtime monitor data (tick time, memory, entities)")
                .HandleWith(_ =>
                {
                    var report = GetMonitorReport();
                    return TextCommandResult.Success(report);
                });

            cmd.BeginSubCommand("eventbus")
                .WithDescription("Show EventBus subscription details")
                .HandleWith(_ =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("=== EventBus Subscriptions ===");
                    var subs = EventBus.GetDiagnostics();
                    sb.AppendLine($"Total tracked: {subs.Count}");
                    sb.AppendLine($"Active: {EventBus.ActiveSubscriptionCount()}");
                    sb.AppendLine();
                    foreach (var s in subs.Take(20))
                    {
                        string status = s.IsDisposed ? "DISPOSED" : "ACTIVE";
                        sb.AppendLine($"  [{status}] {s.EventType.Name}[{s.Tag}] calls={s.InvocationCount} avg={s.AverageInvocationMs:F2}ms");
                        if (!string.IsNullOrEmpty(s.LastError))
                            sb.AppendLine($"           last error: {s.LastError}");
                    }
                    if (subs.Count > 20)
                        sb.AppendLine($"  ... and {subs.Count - 20} more");
                    sb.AppendLine();
                    var dangling = EventBus.GetDanglingSubscriptions();
                    if (dangling.Count > 0)
                    {
                        sb.AppendLine($"Dangling (never published): {dangling.Count}");
                        foreach (var d in dangling.Take(10))
                            sb.AppendLine($"  - {d}");
                    }
                    sb.AppendLine("=== End EventBus ===");
                    return TextCommandResult.Success(sb.ToString());
                });
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning("[ArcanumLib/Diagnostics] Failed to register command: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Extracts the summary section from a full report for chat output.
    /// </summary>
    private static string ExtractSummary(string report)
    {
        var summary = new StringBuilder();
        bool inSummary = false;
        foreach (var line in report.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains("-- Summary --")) inSummary = true;
            if (inSummary) summary.AppendLine(line.Trim());
            if (line.Contains("=== End Diagnostics ===")) break;
        }
        return summary.Length > 0 ? summary.ToString() : "Diagnostics completed. See server log for details.";
    }

    /// <summary>
    /// Stops the monitor listener and clears the API reference on world unload.
    /// </summary>
    public override void Dispose()
    {
        if (_sapi != null && _monitorListenerId != 0)
        {
            try { _sapi.Event.UnregisterGameTickListener(_monitorListenerId); }
            catch (Exception ex) { _sapi.Logger?.Warning("[ArcanumLib/Diagnostics] Failed to unregister monitor listener: {0}", ex.Message); }
        }
        _sapi = null;
        base.Dispose();
    }
}
