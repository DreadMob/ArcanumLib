using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace ArcanumLib.Persistence;

/// <summary>
/// Result of loading or saving a mod config.
/// </summary>
public enum ConfigResultKind
{
    /// <summary>Operation succeeded.</summary>
    Success,
    /// <summary>Config file was missing; defaults were used.</summary>
    DefaultsUsed,
    /// <summary>Config file existed but could not be parsed; defaults were used.</summary>
    ParseFailed,
    /// <summary>Validation failed; the invalid config was rejected.</summary>
    ValidationFailed,
    /// <summary>An I/O error occurred.</summary>
    IOError
}

/// <summary>
/// Outcome of a config load or save operation.
/// </summary>
public readonly struct ConfigResult
{
    /// <summary>
    /// Whether the operation succeeded (kind is Success or DefaultsUsed).
    /// </summary>
    public bool IsSuccess => Kind == ConfigResultKind.Success || Kind == ConfigResultKind.DefaultsUsed;

    /// <summary>
    /// The result kind.
    /// </summary>
    public ConfigResultKind Kind { get; }

    /// <summary>
    /// Human-readable message describing the outcome, including errors.
    /// </summary>
    public string? Message { get; }

    public ConfigResult(ConfigResultKind kind, string? message = null)
    {
        Kind = kind;
        Message = message;
    }

    public static ConfigResult Success() => new(ConfigResultKind.Success);
    public static ConfigResult DefaultsUsed(string? msg = null) => new(ConfigResultKind.DefaultsUsed, msg);
    public static ConfigResult ParseFailed(string? msg = null) => new(ConfigResultKind.ParseFailed, msg);
    public static ConfigResult ValidationFailed(string? msg = null) => new(ConfigResultKind.ValidationFailed, msg);
    public static ConfigResult IOError(string? msg = null) => new(ConfigResultKind.IOError, msg);
}

/// <summary>
/// Typed wrapper around the Vintage Story mod config system.
/// Loads and saves a JSON config of type <typeparamref name="T"/> with optional
/// validation, default fallback, and automatic migration hooks.
/// </summary>
/// <typeparam name="T">The config type. Must be JSON-serializable with Newtonsoft.Json.</typeparam>
public sealed class ModConfig<T> where T : class, new()
{
    private readonly ICoreAPI _api;
    private readonly string _filename;
    private readonly System.Func<T, bool>? _validate;
    private readonly System.Action<T>? _onLoaded;
    private readonly JsonSerializerSettings _serializerSettings;

    /// <summary>
    /// The currently loaded config. Falls back to a new <c>T()</c> if loading failed.
    /// </summary>
    public T Current { get; internal set; }

    /// <summary>
    /// Creates a config wrapper.
    /// </summary>
    /// <param name="api">The core API.</param>
    /// <param name="filename">The config filename (e.g. <c>MyModConfig.json</c>).</param>
    /// <param name="validate">Optional validation predicate. If it returns false, defaults are used.</param>
    /// <param name="onLoaded">Optional callback invoked after a successful load (including defaults).</param>
    /// <param name="serializerSettings">Optional JSON serializer settings. Defaults to standard VS settings.</param>
    public ModConfig(
        ICoreAPI api,
        string filename,
        System.Func<T, bool>? validate = null,
        System.Action<T>? onLoaded = null,
        JsonSerializerSettings? serializerSettings = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be empty.", nameof(filename));
        _filename = filename;
        _validate = validate;
        _onLoaded = onLoaded;
        _serializerSettings = serializerSettings ?? new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        Current = new T();
    }

    /// <summary>
    /// Loads the config from disk. If the file is missing or invalid, falls back to
    /// <c>new T()</c> and logs a warning. Returns the load result.
    /// </summary>
    public ConfigResult Load()
    {
        try
        {
            T? loaded = _api.LoadModConfig<T>(_filename);
            if (loaded == null)
            {
                Current = new T();
                _onLoaded?.Invoke(Current);
                _api.Logger?.Notification("[ArcanumLib] Config '{0}' not found; using defaults.", _filename);
                return ConfigResult.DefaultsUsed($"Config '{_filename}' not found.");
            }

            if (_validate != null && !_validate(loaded))
            {
                Current = new T();
                _onLoaded?.Invoke(Current);
                _api.Logger?.Warning("[ArcanumLib] Config '{0}' failed validation; using defaults.", _filename);
                return ConfigResult.ValidationFailed($"Config '{_filename}' failed validation.");
            }

            Current = loaded;
            _onLoaded?.Invoke(Current);
            return ConfigResult.Success();
        }
        catch (Exception ex)
        {
            Current = new T();
            _onLoaded?.Invoke(Current);
            _api.Logger?.Warning("[ArcanumLib] Failed to load config '{0}': {1}", _filename, ex.Message);
            return ConfigResult.ParseFailed($"Failed to load config '{_filename}': {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current config to disk.
    /// </summary>
    public ConfigResult Save()
    {
        try
        {
            string json = JsonConvert.SerializeObject(Current, _serializerSettings);
            _api.StoreModConfig(_filename, json);
            return ConfigResult.Success();
        }
        catch (Exception ex)
        {
            _api.Logger?.Warning("[ArcanumLib] Failed to save config '{0}': {1}", _filename, ex.Message);
            return ConfigResult.IOError($"Failed to save config '{_filename}': {ex.Message}");
        }
    }

    /// <summary>
    /// Reloads the config from disk, replacing <see cref="Current"/>.
    /// </summary>
    public ConfigResult Reload() => Load();

    /// <summary>
    /// Serializes the current config to a JSON string.
    /// </summary>
    public string ToJson()
        => JsonConvert.SerializeObject(Current, _serializerSettings);

    /// <summary>
    /// Replaces the current config with a deserialized JSON string and runs validation.
    /// Returns false if validation fails; in that case <see cref="Current"/> is not changed.
    /// </summary>
    public bool TryApplyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            T? parsed = JsonConvert.DeserializeObject<T>(json, _serializerSettings);
            if (parsed == null) return false;

            if (_validate != null && !_validate(parsed))
                return false;

            Current = parsed;
            _onLoaded?.Invoke(Current);
            return true;
        }
        catch (Exception ex)
        {
            _api.Logger?.Warning("[ArcanumLib] Failed to apply JSON to config '{0}': {1}", _filename, ex.Message);
            return false;
        }
    }
}
