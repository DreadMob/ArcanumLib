using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Diagnostics;

/// <summary>
/// Diagnostic ModSystem that validates ArcanumLib module and service registration,
/// EventBus health, dependency chain integrity, and runtime performance after all
/// other ModSystems have started. Coordinates <see cref="DiagnosticsReporter" />,
/// <see cref="RuntimeMonitor" />, and <see cref="DiagnosticsCommands" /> and exposes
/// <c>/arcanum diagnose</c> and <c>/arcanum monitor</c> commands.
/// </summary>
public class DiagnosticsModSystem : ModSystem
{
    private ICoreServerAPI? _sapi;
    private RuntimeMonitor? _monitor;
    private DiagnosticsReporter? _reporter;
    private DiagnosticsCommands? _commands;

    /// <summary>Runs after all other ArcanumLib systems so registrations are complete.</summary>
    /// <returns>The execute order.</returns>
    public override double ExecuteOrder() => 1000;

    /// <summary>Server-side only: diagnostics are authoritative on the server.</summary>
    /// <param name="forSide">The for side value.</param>
    /// <returns>true if the operation should load; otherwise, false.</returns>
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    /// <summary>
    /// Registers the diagnostics commands, starts runtime monitoring, and runs
    /// the first validation pass.
    /// </summary>
    /// <param name="sapi">The server API instance.</param>
    public override void StartServerSide(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        var logPath = Path.Combine(sapi.GetOrCreateDataPath("Logs"), "arcanumlib-diagnostics.log");

        _monitor = new RuntimeMonitor();
        _reporter = new DiagnosticsReporter(_monitor, logPath, sapi.Logger);
        _commands = new DiagnosticsCommands(_reporter, _monitor);

        _commands.Register(sapi);
        _monitor.StartMonitoring(sapi);
        // Delay the first diagnostics pass so event-driven systems have a chance
        // to publish at least once (player join, objective accepted, etc.). Running
        // immediately at startup produces false "never-published" warnings for
        // events that simply haven't fired yet.
        sapi.Event.RegisterCallback(_ => _reporter.RunDiagnostics(sapi), 30000);
    }

    /// <summary>
    /// Stops the monitor listener and releases owned components on world unload.
    /// </summary>
    public override void Dispose()
    {
        _monitor?.StopMonitoring(_sapi);
        _sapi = null;
        base.Dispose();
    }
}
