using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Vintagestory.API.Common;

namespace ArcanumLib.Logging;

/// <summary>
/// Categorized file logger for Vintage Story mods. Writes structured logs to
/// categorized files in a configurable subfolder of the game's Logs directory.
/// All warnings/errors and explicitly important events are also copied to
/// <c>important.log</c>. Thread-safe, with periodic auto-flush and debug throttling.
/// </summary>
/// <remarks>
/// Consumers should call <see cref="Init"/> during mod startup and
/// <see cref="Dispose"/> (or let the singleton dispose itself) on shutdown.
/// Categories are arbitrary strings supplied by the consumer — for example
/// <c>"combat"</c>, <c>"economy/trades"</c>, <c>"system/errors"</c>.
/// Subcategories are created automatically using the forward slash separator.
/// </remarks>
public class CategorizedLogger : IDisposable
{
    /// <summary>
    /// Singleton instance. Set by <see cref="Init"/> and cleared by <see cref="Dispose"/>.
    /// </summary>
    public static CategorizedLogger? Instance { get; protected set; }

    /// <summary>
    /// Initializes the singleton with the given API, config, and log subfolder name.
    /// Disposes any previous instance and creates fresh log files.
    /// </summary>
    /// <param name="api">The core API used to resolve the log directory and mirror to console.</param>
    /// <param name="config">Optional config. Defaults to <see cref="LogMode.Production"/> with file logging enabled.</param>
    /// <param name="logFolderName">Name of the subfolder inside the game's Logs directory.</param>
    /// <param name="consolePrefix">Prefix used in console mirror messages, e.g. <c>[ModName]</c>.</param>
    public static void Init(ICoreAPI api, LogConfig? config = null, string logFolderName = "mod", string consolePrefix = "CategorizedLogger")
    {
        Instance?.Dispose();
        Instance = new CategorizedLogger(api, config, logFolderName, consolePrefix);
        api?.Logger?.Notification("[{0}] Singleton initialized (mode: {1}, folder: {2}).",
            consolePrefix, Instance.Config.Mode, logFolderName);
    }

    /// <summary>
    /// Applies a new config to the current instance, updating the log path if needed.
    /// </summary>
    public static void ApplyConfig(LogConfig config)
    {
        if (Instance == null || config == null) return;
        Instance.Config = config;
        Instance.UpdateBaseLogPath();
        Instance.api?.Logger?.Notification("[{0}] Config applied (mode: {1}).", Instance._consolePrefix, config.Mode);
    }

    private readonly ICoreAPI api;
    private readonly string _logFolderName;
    private readonly string _consolePrefix;
    private string? baseLogPath;
    private readonly ConcurrentDictionary<string, StreamWriter?> writers = new();
    private readonly ConcurrentDictionary<string, object> writeLocks = new();
    private readonly ConcurrentDictionary<string, long> lastLogMsByKey = new();
    private readonly SemaphoreSlim flushSemaphore = new(1, 1);
    private long lastFlushMs = 0;
    private const long FlushIntervalMs = 5000;
    private const long DebugThrottleMs = 1000;
    private bool disposed = false;

    /// <summary>
    /// Current logging configuration.
    /// </summary>
    public LogConfig Config { get; protected set; } = new();

    /// <summary>
    /// Creates a categorized logger.
    /// </summary>
    /// <param name="api">The core API used to resolve the log directory and mirror to console.</param>
    /// <param name="config">Optional config. Defaults to <see cref="LogMode.Production"/> with file logging enabled.</param>
    /// <param name="logFolderName">Name of the subfolder inside the game's Logs directory.</param>
    /// <param name="consolePrefix">Prefix used in console mirror messages, e.g. <c>[ModName]</c>.</param>
    public CategorizedLogger(ICoreAPI api, LogConfig? config = null, string logFolderName = "mod", string consolePrefix = "CategorizedLogger")
    {
        this.api = api;
        this._logFolderName = logFolderName ?? "mod";
        this._consolePrefix = consolePrefix ?? "CategorizedLogger";
        if (config != null) Config = config;
        UpdateBaseLogPath();
        api?.Logger?.Notification($"[{_consolePrefix}] Initialized. File logging: {(Config.EnableFileLog ? "enabled" : "disabled")}.");
    }

    private void UpdateBaseLogPath()
    {
        if (!Config.EnableFileLog)
        {
            baseLogPath = null;
            return;
        }

        string dataPath = api.GetOrCreateDataPath("Logs");
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            api?.Logger?.Warning("[{0}] GetOrCreateDataPath('Logs') returned null; file logging disabled.", _consolePrefix);
            baseLogPath = null;
            return;
        }
        baseLogPath = Path.Combine(dataPath, _logFolderName);

        try
        {
            if (!string.IsNullOrWhiteSpace(baseLogPath))
                Directory.CreateDirectory(baseLogPath);
            api?.Logger?.Notification($"[{_consolePrefix}] Logs directory: {baseLogPath}");
        }
        catch (Exception ex)
        {
            api?.Logger?.Error("[{0}] Failed to create logs directory {1}: {2}", _consolePrefix, baseLogPath, ex);
            baseLogPath = null;
        }
    }

    /// <summary>
    /// Write an explicitly important event. Goes to both the category file and the consolidated important.log.
    /// </summary>
    public void Important(string category, string message, Exception? ex = null)
    {
        var fullMessage = BuildMessage(message, ex);
        if (Config.EnableFileLog)
        {
            WriteImportant(category, "IMPORTANT", fullMessage);
            if (!string.Equals(category, "important", StringComparison.OrdinalIgnoreCase))
                WriteLog(category, "INFO", fullMessage);
        }
        if (ShouldMirrorToConsoleImportant())
            api?.Logger?.Notification($"[{_consolePrefix}/{category}] {fullMessage}");
    }

    /// <summary>
    /// Write an error. Errors go to the category file, important.log and the console.
    /// </summary>
    public void Error(string category, string message, Exception? ex = null)
    {
        var fullMessage = BuildMessage(message, ex);
        if (Config.EnableFileLog)
        {
            WriteLog(category, "ERROR", fullMessage);
            WriteImportant(category, "ERROR", fullMessage);
        }
        api?.Logger?.Error($"[{_consolePrefix}/{category}] {fullMessage}");
    }

    /// <summary>
    /// Write a warning. Warnings go to the category file and the consolidated important.log.
    /// </summary>
    public void Warning(string category, string message, Exception? ex = null)
    {
        var fullMessage = BuildMessage(message, ex);
        if (Config.EnableFileLog)
        {
            WriteLog(category, "WARN", fullMessage);
            WriteImportant(category, "WARN", fullMessage);
        }
        if (ShouldMirrorToConsoleWarning())
            api?.Logger?.Warning($"[{_consolePrefix}/{category}] {fullMessage}");
    }

    /// <summary>
    /// Write an informational log entry to the category file only.
    /// </summary>
    public void Info(string category, string message)
    {
        if (Config.EnableFileLog)
            WriteLog(category, "INFO", message);
        if (ShouldMirrorToConsoleInfo())
            api?.Logger?.Notification($"[{_consolePrefix}/{category}] {message}");
    }

    /// <summary>
    /// Write a debug log entry (throttled by <see cref="DebugThrottleMs"/> to avoid per-tick bloat).
    /// </summary>
    public void Debug(string category, string message)
    {
        if (Config.Mode != LogMode.Debug && Config.Mode != LogMode.Verbose)
            return;
        if (ShouldThrottle(category, message))
            return;
        if (Config.EnableFileLog)
            WriteLog(category, "DEBUG", message);
        if (ShouldMirrorToConsoleDebug())
            api?.Logger?.Debug($"[{_consolePrefix}/{category}] {message}");
    }

    /// <summary>
    /// Write a structured log entry with key-value pairs.
    /// </summary>
    public void Structured(string category, string eventType, params (string key, string value)[] fields)
    {
        var sb = new StringBuilder();
        sb.Append($"[{eventType}]");

        foreach (var (key, value) in fields)
        {
            sb.Append($" {key}={value}");
        }

        var line = sb.ToString();
        if (Config.EnableFileLog)
        {
            WriteLog(category, "STRUCTURED", line);
            WriteImportant(category, "STRUCTURED", line);
        }
        if (ShouldMirrorToConsoleInfo())
            api?.Logger?.Notification($"[{_consolePrefix}/{category}] {line}");
    }

    private static string BuildMessage(string message, Exception? ex)
    {
        return ex != null
            ? $"{message} | Exception: {ex}"
            : message;
    }

    private void WriteImportant(string category, string level, string message)
    {
        WriteLog("important", level, $"[{category}] {message}");
    }

    private bool ShouldMirrorToConsoleImportant()
    {
        return Config.Mode == LogMode.Debug || Config.Mode == LogMode.Verbose;
    }

    private bool ShouldMirrorToConsoleWarning()
    {
        return Config.Mode == LogMode.Debug || Config.Mode == LogMode.Verbose;
    }

    private bool ShouldMirrorToConsoleInfo()
    {
        return Config.Mode == LogMode.Verbose;
    }

    private bool ShouldMirrorToConsoleDebug()
    {
        return Config.Mode == LogMode.Verbose;
    }

    private bool ShouldThrottle(string category, string message)
    {
        long now = 0;
        try
        {
            now = api?.World?.ElapsedMilliseconds ?? 0L;
        }
        catch
        {
            return false;
        }

        var key = $"{category}:{message}";
        var existing = lastLogMsByKey.AddOrUpdate(key, now, (_, last) =>
        {
            if (now - last < DebugThrottleMs) return last;
            return now;
        });

        return existing != now;
    }

    private void WriteLog(string category, string level, string message)
    {
        if (disposed) return;

        try
        {
            var writer = GetOrCreateWriter(category);
            if (writer == null) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp}] [{level}] {message}";

            var lockObj = writeLocks.GetOrAdd(category, _ => new object());
            lock (lockObj)
            {
                writer.WriteLine(logLine);
            }

            TryAutoFlush();
        }
        catch (Exception ex)
        {
            api?.Logger?.Error("[{0}] Failed to write log for category '{1}': {2}", _consolePrefix, category, ex);
        }
    }

    private StreamWriter? GetOrCreateWriter(string category)
    {
        if (writers.TryGetValue(category, out var existing))
            return existing;

        var lockObj = writeLocks.GetOrAdd(category, _ => new object());
        lock (lockObj)
        {
            if (writers.TryGetValue(category, out existing))
                return existing;

            try
            {
                if (!Config.EnableFileLog || string.IsNullOrWhiteSpace(baseLogPath))
                {
                    return null;
                }

                var sanitized = category.Replace("..", "").Replace("\\", "/").Trim('/');
                var logFile = Path.Combine(baseLogPath, sanitized + ".log");

                var dir = Path.GetDirectoryName(logFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Append to avoid truncating an existing log; a concurrent GetOrAdd could otherwise
                // call the factory twice and the second FileMode.Create would wipe the first writer.
                var stream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.Read);
                var writer = new StreamWriter(stream) { AutoFlush = false };

                writers[category] = writer;

                api?.Logger?.Debug($"[{_consolePrefix}] Created log file: {logFile}");
                return writer;
            }
            catch (Exception ex)
            {
                api?.Logger?.Error("[{0}] Failed to create writer for '{1}': {2}", _consolePrefix, category, ex);
                return null;
            }
        }
    }

    private void TryAutoFlush()
    {
        long now = 0;
        try
        {
            now = api?.World?.ElapsedMilliseconds ?? 0;
        }
        catch
        {
            return;
        }

        var last = Interlocked.Read(ref lastFlushMs);

        if (now - last < FlushIntervalMs) return;

        if (flushSemaphore.Wait(0))
        {
            try
            {
                try
                {
                    now = api?.World?.ElapsedMilliseconds ?? 0;
                }
                catch
                {
                    return;
                }

                last = Interlocked.Read(ref lastFlushMs);

                if (now - last >= FlushIntervalMs)
                {
                    Interlocked.Exchange(ref lastFlushMs, now);

                    foreach (var writer in writers.Values)
                    {
                        try
                        {
                            writer?.Flush();
                        }
                        catch (Exception ex)
                        {
                            api?.Logger?.Warning("[{0}] Failed to flush writer: {1}", _consolePrefix, ex);
                        }
                    }
                }
            }
            finally
            {
                flushSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Flush all log writers immediately.
    /// </summary>
    public void FlushAll()
    {
        if (flushSemaphore.Wait(3000))
        {
            try
            {
                foreach (var writer in writers.Values)
                {
                    try
                    {
                        writer?.Flush();
                    }
                    catch (Exception ex)
                    {
                        api?.Logger?.Warning("[{0}] Failed to flush writer: {1}", _consolePrefix, ex);
                    }
                }
                Interlocked.Exchange(ref lastFlushMs, api?.World?.ElapsedMilliseconds ?? 0);
            }
            finally
            {
                flushSemaphore.Release();
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        try
        {
            FlushAll();

            foreach (var writer in writers.Values)
            {
                try
                {
                    writer?.Dispose();
                }
                catch (Exception ex)
                {
                    api?.Logger?.Warning("[{0}] Failed to dispose writer: {1}", _consolePrefix, ex);
                }
            }

            writers.Clear();
            writeLocks.Clear();
            lastLogMsByKey.Clear();

            api?.Logger?.Notification("[{0}] Disposed and all logs flushed.", _consolePrefix);
        }
        catch (Exception ex)
        {
            api?.Logger?.Error("[{0}] Error during disposal: {1}", _consolePrefix, ex);
        }
        finally
        {
            flushSemaphore.Dispose();
            Instance = null;
        }
    }
}
