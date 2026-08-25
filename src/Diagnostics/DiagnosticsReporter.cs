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
/// Runs static diagnostics: service registration checks, EventBus health,
/// dependency chain validation, and report building. Writes results to the
/// server log and a dedicated diagnostics log file. Delegates the runtime
/// monitor section to the supplied <see cref="RuntimeMonitor" />.
/// </summary>
internal sealed class DiagnosticsReporter
{
    private readonly RuntimeMonitor _monitor;
    private readonly string? _logPath;
    private readonly ILogger _logger;

    private static IEventBusService? EventBusService => ArcanumServices.Get<IEventBusService>();

    /// <summary>
    /// Creates a reporter that writes to <paramref name="logPath" /> and reports
    /// file-write failures through <paramref name="logger" />.
    /// </summary>
    /// <param name="monitor">The runtime monitor supplying live metrics.</param>
    /// <param name="logPath">The diagnostics log file path, or null to skip file logging.</param>
    /// <param name="logger">The logger used for file-write error reporting.</param>
    public DiagnosticsReporter(RuntimeMonitor monitor, string? logPath, ILogger logger)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _logPath = logPath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs a full diagnostics pass and logs the results to the server log and
    /// the diagnostics log file. Returns the report text.
    /// </summary>
    /// <param name="sapi">The server API instance.</param>
    /// <returns>The full diagnostics report text.</returns>
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
        var actionRegistry = ArcanumServices.Get<IActionRegistryService>();
        var actionExecutor = ArcanumServices.Get<IActionExecutorService>();
        var statusEffect = ArcanumServices.Get<IStatusEffectService>();
        var logger = ArcanumServices.Get<ICategorizedLogger>();

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
        if (_monitor.MonitorTickCount > 0)
        {
            double avgTick = _monitor.TotalTickDurationMs / _monitor.MonitorTickCount;
            report.AppendLine($"  [INFO] Monitored ticks: {_monitor.MonitorTickCount}");
            report.AppendLine($"  [INFO] Avg tick: {avgTick:F2} ms");
            report.AppendLine($"  [INFO] Max tick: {_monitor.MaxTickDurationMs:F2} ms");
            if (_monitor.MaxTickDurationMs > 50)
            {
                report.AppendLine($"  [WARN] Max tick exceeded 50ms threshold — possible performance issue");
                warnings++;
            }

            long currentMem = Process.GetCurrentProcess().PrivateMemorySize64;
            report.AppendLine($"  [INFO] Memory: {currentMem / 1024 / 1024} MB");
            report.AppendLine($"  [INFO] Peak memory: {_monitor.PeakMemoryBytes / 1024 / 1024} MB");
            long peak = _monitor.PeakMemoryBytes;
            if (peak > 0 && currentMem > peak * 2 && peak > 100 * 1024 * 1024)
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
        report.AppendLine($"  EventBus subscriptions: {EventBusService?.ActiveSubscriptionCount() ?? 0}");
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
    /// <param name="report">The report builder to append results to.</param>
    /// <returns>Always zero; warnings are reported but do not count as errors.</returns>
    private int CheckEventBus(StringBuilder report)
    {
        int warnings = 0;
        report.AppendLine("-- EventBus Health --");

        int activeCount = EventBusService?.ActiveSubscriptionCount() ?? 0;
        report.AppendLine($"  [INFO] Active subscriptions: {activeCount}");

        var subs = EventBusService?.GetDiagnostics() ?? new List<EventBusSubscriptionInfo>();
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
        var dangling = EventBusService?.GetDanglingSubscriptions() ?? new List<string>();
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
    /// <param name="sapi">The server API instance.</param>
    /// <param name="report">The report builder to append results to.</param>
    /// <returns>The number of errors detected.</returns>
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
    /// Returns true if <paramref name="installed" /> satisfies the
    /// <paramref name="required" /> minimum. Pre-release versions (e.g.
    /// "1.0.0-rc1") are considered lower than the release ("1.0.0").
    /// </summary>
    /// <param name="installed">The installed version string.</param>
    /// <param name="required">The required minimum version string.</param>
    /// <returns>true if the installed version satisfies the required minimum; otherwise, false.</returns>
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

    // ── Logging ──

    /// <summary>
    /// Appends the report to the diagnostics log file.
    /// </summary>
    /// <param name="text">The text to append.</param>
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
            _logger?.Warning("[ArcanumLib/Diagnostics] Failed to write log file: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Extracts the summary section from a full report for chat output.
    /// </summary>
    /// <param name="report">The full report text.</param>
    /// <returns>The extracted summary, or a default message when no summary section is present.</returns>
    internal static string ExtractSummary(string report)
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
}
