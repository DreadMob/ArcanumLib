namespace ArcanumLib.Logging;

/// <summary>
/// Log output modes. Production keeps console clean; Debug/Verbose mirror more to console.
/// </summary>
public enum LogMode
{
    /// <summary>No file logging and only critical (error) console output.</summary>
    Silent,
    /// <summary>Files for everything; console only for errors. Default.</summary>
    Production,
    /// <summary>Files for everything; console for warnings and errors; debug to file.</summary>
    Debug,
    /// <summary>Mirror almost everything to both file and console (use sparingly).</summary>
    Verbose
}
