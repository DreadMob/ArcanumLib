using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ArcanumLib.Core;
using ArcanumLib.Events;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Diagnostics;

/// <summary>
/// Registers the <c>/arcanum</c> chat commands and their handlers, delegating
/// to <see cref="DiagnosticsReporter" /> and <see cref="RuntimeMonitor" />.
/// </summary>
internal sealed class DiagnosticsCommands
{
    private readonly DiagnosticsReporter _reporter;
    private readonly RuntimeMonitor _monitor;

    private static IEventBusService? EventBusService => ArcanumServices.Get<IEventBusService>();

    /// <summary>
    /// Creates the command registrar.
    /// </summary>
    /// <param name="reporter">The diagnostics reporter used by the diagnose command.</param>
    /// <param name="monitor">The runtime monitor used by the monitor command.</param>
    public DiagnosticsCommands(DiagnosticsReporter reporter, RuntimeMonitor monitor)
    {
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }

    /// <summary>
    /// Registers the <c>/arcanum diagnose</c>, <c>/arcanum monitor</c>, and
    /// <c>/arcanum eventbus</c> chat commands with the server.
    /// </summary>
    /// <param name="sapi">The server API instance.</param>
    public void Register(ICoreServerAPI sapi)
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
                    var report = _reporter.RunDiagnostics(sapi);
                    var summary = DiagnosticsReporter.ExtractSummary(report);
                    return TextCommandResult.Success(summary);
                });

            cmd.BeginSubCommand("monitor")
                .WithDescription("Show runtime monitor data (tick time, memory, entities)")
                .HandleWith(_ =>
                {
                    var report = _monitor.GetMonitorReport();
                    return TextCommandResult.Success(report);
                });

            cmd.BeginSubCommand("eventbus")
                .WithDescription("Show EventBus subscription details")
                .HandleWith(_ =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("=== EventBus Subscriptions ===");
                    var subs = EventBusService?.GetDiagnostics() ?? new List<EventBusSubscriptionInfo>();
                    sb.AppendLine($"Total tracked: {subs.Count}");
                    sb.AppendLine($"Active: {EventBusService?.ActiveSubscriptionCount() ?? 0}");
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
                    var dangling = EventBusService?.GetDanglingSubscriptions() ?? new List<string>();
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
}
