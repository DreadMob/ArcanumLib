namespace ArcanumLib.Logging;

/// <summary>
/// Centralized logging configuration. Simple: pick a mode and toggle file logging.
/// </summary>
public class LogConfig
{
    /// <summary>Logging verbosity mode.</summary>
    public LogMode Mode { get; set; } = LogMode.Production;

    /// <summary>If false, all file logging is disabled. Errors still go to console.</summary>
    public bool EnableFileLog { get; set; } = true;
}
