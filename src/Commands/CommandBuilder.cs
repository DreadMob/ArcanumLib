using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Commands;

/// <summary>
/// Defines a single typed command argument.
/// </summary>
public sealed class CommandArgument
{
    /// <summary>The argument name shown in syntax help.</summary>
    public string Name { get; init; } = "";

    /// <summary>The argument type: <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c>, <c>player</c>.</summary>
    public Type Type { get; init; } = typeof(string);

    /// <summary>Whether the argument is required.</summary>
    public bool Required { get; init; } = true;

    /// <summary>Default value used when the argument is omitted and <see cref="Required" /> is false.</summary>
    public object? Default { get; init; }

    /// <summary>Optional autocomplete values for this argument.</summary>
    public System.Func<ICoreServerAPI, IServerPlayer, string[]?>? Autocomplete { get; init; }
}

/// <summary>
/// Resolved argument values passed to the command handler.
/// </summary>
public sealed class CommandArgs
{
    private readonly Dictionary<string, object?> _values;

    internal CommandArgs(Dictionary<string, object?> values)
    {
        _values = values;
    }

    /// <summary>
    /// Gets a required string argument.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <returns>The string value, or an empty string if the value is not a string.</returns>
    public string String(string name) => _values[name] as string ?? "";

    /// <summary>
    /// Gets a required int argument.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <returns>The int value, or <c>0</c> if the value is not an int.</returns>
    public int Int(string name) => _values[name] is int v ? v : 0;

    /// <summary>
    /// Gets a required float argument.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <returns>The float value, or <c>0f</c> if the value is not a float.</returns>
    public float Float(string name) => _values[name] is float v ? v : 0f;

    /// <summary>
    /// Gets a required bool argument.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <returns>The bool value, or <c>false</c> if the value is not a bool.</returns>
    public bool Bool(string name) => _values[name] is bool v && v;

    /// <summary>
    /// Gets an optional string argument, returning <paramref name="fallback" /> if absent.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The default value to return when the argument is missing.</param>
    /// <returns>The string value, or <paramref name="fallback" /> if the argument is missing or not a string.</returns>
    public string StringOr(string name, string fallback) => _values.TryGetValue(name, out var v) ? v as string ?? fallback : fallback;

    /// <summary>
    /// Gets an optional int argument, returning <paramref name="fallback" /> if absent.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The default value to return when the argument is missing.</param>
    /// <returns>The int value, or <paramref name="fallback" /> if the argument is missing or not an int.</returns>
    public int IntOr(string name, int fallback) => _values.TryGetValue(name, out var v) && v is int iv ? iv : fallback;

    /// <summary>
    /// Gets an optional float argument, returning <paramref name="fallback" /> if absent.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The default value to return when the argument is missing.</param>
    /// <returns>The float value, or <paramref name="fallback" /> if the argument is missing or not a float.</returns>
    public float FloatOr(string name, float fallback) => _values.TryGetValue(name, out var v) && v is float fv ? fv : fallback;

    /// <summary>
    /// Gets an optional bool argument, returning <paramref name="fallback" /> if absent.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The default value to return when the argument is missing.</param>
    /// <returns>The bool value, or <paramref name="fallback" /> if the argument is missing or not a bool.</returns>
    public bool BoolOr(string name, bool fallback) => _values.TryGetValue(name, out var v) && v is bool bv ? bv : fallback;

    /// <summary>
    /// Determines whether the argument was provided.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <returns><c>true</c> if the argument was provided; otherwise <c>false</c>.</returns>
    public bool Has(string name) => _values.ContainsKey(name);
}

/// <summary>
/// Fluent command builder for Vintage Story server commands.
/// Provides typed arguments, permission gating, autocomplete, and a clean handler signature.
/// </summary>
public sealed class CommandBuilder
{
    private readonly ICoreServerAPI _sapi;
    private readonly string _name;
    private readonly List<CommandArgument> _args = new();
    private string _description = "";
    private string _permission = "";
    private Action<ICoreServerAPI, IServerPlayer, CommandArgs>? _handler;
    private bool _registered;

    private CommandBuilder(ICoreServerAPI sapi, string name)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Creates a new command builder for the given command name.
    /// </summary>
    /// <param name="sapi">The server API.</param>
    /// <param name="name">The command name, including a mod prefix.</param>
    /// <returns>A new <see cref="CommandBuilder" /> instance.</returns>
    public static CommandBuilder Create(ICoreServerAPI sapi, string name) => new(sapi, name);

    /// <summary>
    /// Sets the command description shown in help.
    /// </summary>
    /// <param name="description">The command description.</param>
    /// <returns>The current builder for method chaining.</returns>
    public CommandBuilder WithDescription(string description)
    {
        _description = description ?? "";
        return this;
    }

    /// <summary>
    /// Sets the required permission level.
    /// </summary>
    /// <param name="permission">The privilege code. An empty string means no special permission.</param>
    /// <returns>The current builder for method chaining.</returns>
    public CommandBuilder WithPermission(string permission)
    {
        _permission = permission ?? "";
        return this;
    }

    /// <summary>
    /// Adds a required string argument.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <param name="autocomplete">Optional autocomplete values for this argument.</param>
    /// <returns>The current builder for method chaining.</returns>
    public CommandBuilder Arg(string name, System.Func<ICoreServerAPI, IServerPlayer, string[]?>? autocomplete = null)
        => ArgTyped(name, typeof(string), required: true, @default: null, autocomplete);

    /// <summary>
    /// Adds a required argument of the specified type.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="name">The argument name.</param>
    /// <param name="autocomplete">Optional autocomplete values for this argument.</param>
    /// <returns>The current builder for method chaining.</returns>
    public CommandBuilder Arg<T>(string name, System.Func<ICoreServerAPI, IServerPlayer, string[]?>? autocomplete = null)
        => ArgTyped(name, typeof(T), required: true, @default: null, autocomplete);

    /// <summary>
    /// Adds an optional argument of the specified type with a default value.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="name">The argument name.</param>
    /// <param name="defaultValue">The default value used when the argument is omitted.</param>
    /// <param name="autocomplete">Optional autocomplete values for this argument.</param>
    /// <returns>The current builder for method chaining.</returns>
    public CommandBuilder Arg<T>(string name, T defaultValue, System.Func<ICoreServerAPI, IServerPlayer, string[]?>? autocomplete = null)
        => ArgTyped(name, typeof(T), required: false, @default: defaultValue, autocomplete);

    private CommandBuilder ArgTyped(string name, Type type, bool required, object? @default,
        System.Func<ICoreServerAPI, IServerPlayer, string[]?>? autocomplete)
    {
        _args.Add(new CommandArgument
        {
            Name = name,
            Type = type,
            Required = required,
            Default = @default,
            Autocomplete = autocomplete
        });
        return this;
    }

    /// <summary>
    /// Sets the handler invoked when the command is run with valid arguments.
    /// </summary>
    /// <param name="handler">The command handler.</param>
    /// <returns>The current builder for method chaining.</returns>
    public CommandBuilder OnExecute(Action<ICoreServerAPI, IServerPlayer, CommandArgs> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>
    /// Registers the command with the server API. Can only be called once per builder.
    /// </summary>
    public void Register()
    {
        if (_registered) throw new InvalidOperationException("Command already registered.");
        if (_handler == null) throw new InvalidOperationException("No handler set. Call OnExecute before Register.");
        _registered = true;

        string syntax = BuildSyntax();

        var cmd = _sapi.ChatCommands.Create(_name)
            .WithDescription(_description)
            .RequiresPrivilege(_permission);

        if (_args.Count == 0)
        {
            cmd.HandleWith(args =>
            {
                try
                {
                    var player = args.Caller.Player as IServerPlayer ?? args.Caller.Entity as IServerPlayer;
                    if (player == null)
                        return TextCommandResult.Error("Command must be run by a player.");

                    _handler!(_sapi, player, new CommandArgs(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)));
                    return TextCommandResult.Success("");
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning("[ArcanumLib] Command '{0}' failed: {1}", _name, ex.Message);
                    return TextCommandResult.Error($"Command error: {ex.Message}");
                }
            });
            return;
        }

        var parsers = BuildParsers();
        cmd.WithArgs(parsers)
            .HandleWith(args =>
            {
                try
                {
                    var player = args.Caller.Player as IServerPlayer ?? args.Caller.Entity as IServerPlayer;
                    if (player == null)
                        return TextCommandResult.Error("Command must be run by a player.");

                    var resolved = ResolveArgsFromParsed(args, syntax);
                    if (resolved == null)
                    {
                        return TextCommandResult.Error($"Invalid arguments. Usage: {_name} {syntax}");
                    }

                    _handler!(_sapi, player, new CommandArgs(resolved));
                    return TextCommandResult.Success("");
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning("[ArcanumLib] Command '{0}' failed: {1}", _name, ex.Message);
                    return TextCommandResult.Error($"Command error: {ex.Message}");
                }
            });
    }

    private ICommandArgumentParser[] BuildParsers()
    {
        var parsers = new ICommandArgumentParser[_args.Count];
        for (int i = 0; i < _args.Count; i++)
        {
            var arg = _args[i];
            parsers[i] = arg.Required
                ? BuildRequiredParser(arg)
                : BuildOptionalParser(arg);
        }
        return parsers;
    }

    private ICommandArgumentParser BuildRequiredParser(CommandArgument arg)
    {
        if (arg.Type == typeof(int)) return _sapi.ChatCommands.Parsers.Int(arg.Name);
        if (arg.Type == typeof(float)) return _sapi.ChatCommands.Parsers.Float(arg.Name);

        var suggestions = GetSuggestions(arg);
        if (arg.Type == typeof(bool)) return _sapi.ChatCommands.Parsers.Word(arg.Name, suggestions);
        return _sapi.ChatCommands.Parsers.Word(arg.Name, suggestions);
    }

    private ICommandArgumentParser BuildOptionalParser(CommandArgument arg)
    {
        if (arg.Type == typeof(int)) return _sapi.ChatCommands.Parsers.OptionalInt(arg.Name, arg.Default is int di ? di : 0);
        if (arg.Type == typeof(float)) return _sapi.ChatCommands.Parsers.OptionalFloat(arg.Name, arg.Default is float df ? df : 0f);

        var suggestions = GetSuggestions(arg);
        // Optional bool and string are parsed from a word so we can apply the configured default value.
        return new Vintagestory.API.Common.WordArgParser(arg.Name, false, suggestions);
    }

    private string[] GetSuggestions(CommandArgument arg)
    {
        try
        {
            return arg.Autocomplete?.Invoke(_sapi, null!) ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _sapi.Logger?.Warning("[ArcanumLib] Command '{0}' autocomplete for '{1}' failed: {2}", _name, arg.Name, ex.Message);
            return Array.Empty<string>();
        }
    }

    private Dictionary<string, object?>? ResolveArgsFromParsed(TextCommandCallingArgs parsed, string syntax)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _args.Count; i++)
        {
            var arg = _args[i];
            object? raw;
            try
            {
                raw = parsed.Parsers[i].GetValue();
            }
            catch (Exception ex)
            {
                _sapi.Logger?.Warning("[ArcanumLib] Command '{0}' failed to read argument '{1}': {2}", _name, arg.Name, ex.Message);
                return null;
            }

            if (arg.Type == typeof(bool))
            {
                string? token = raw?.ToString();
                if (string.IsNullOrWhiteSpace(token))
                {
                    if (arg.Required) return null;
                    result[arg.Name] = arg.Default;
                    continue;
                }

                if (bool.TryParse(token, out bool b))
                {
                    result[arg.Name] = b;
                    continue;
                }

                _sapi.Logger?.Warning("[ArcanumLib] Command '{0}' failed to parse bool argument '{1}' with value '{2}'", _name, arg.Name, token);
                return null;
            }

            if (raw == null || (arg.Type == typeof(string) && string.IsNullOrWhiteSpace(raw as string)))
            {
                if (arg.Required) return null;
                result[arg.Name] = arg.Default;
                continue;
            }

            result[arg.Name] = raw;
        }

        return result;
    }

    private string BuildSyntax()
    {
        var parts = new List<string>();
        foreach (var arg in _args)
        {
            if (arg.Required)
                parts.Add($"<{arg.Name}>");
            else
                parts.Add($"[{arg.Name}]");
        }
        return parts.Count > 0 ? string.Join(" ", parts) : "";
    }
}
