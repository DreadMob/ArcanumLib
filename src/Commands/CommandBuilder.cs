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

    /// <summary>Default value used when the argument is omitted and <see cref="Required"/> is false.</summary>
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

    /// <summary>Gets a required string argument.</summary>
    public string String(string name) => _values[name] as string ?? "";

    /// <summary>Gets a required int argument.</summary>
    public int Int(string name) => _values[name] is int v ? v : 0;

    /// <summary>Gets a required float argument.</summary>
    public float Float(string name) => _values[name] is float v ? v : 0f;

    /// <summary>Gets a required bool argument.</summary>
    public bool Bool(string name) => _values[name] is bool v && v;

    /// <summary>Gets an optional string argument, returning <paramref name="fallback"/> if absent.</summary>
    public string StringOr(string name, string fallback) => _values.TryGetValue(name, out var v) ? v as string ?? fallback : fallback;

    /// <summary>Gets an optional int argument, returning <paramref name="fallback"/> if absent.</summary>
    public int IntOr(string name, int fallback) => _values.TryGetValue(name, out var v) && v is int iv ? iv : fallback;

    /// <summary>Gets an optional float argument, returning <paramref name="fallback"/> if absent.</summary>
    public float FloatOr(string name, float fallback) => _values.TryGetValue(name, out var v) && v is float fv ? fv : fallback;

    /// <summary>Gets an optional bool argument, returning <paramref name="fallback"/> if absent.</summary>
    public bool BoolOr(string name, bool fallback) => _values.TryGetValue(name, out var v) && v is bool bv ? bv : fallback;

    /// <summary>True if the argument was provided.</summary>
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
    /// Command names should include a mod prefix, e.g. <c>mymod.give</c>.
    /// </summary>
    public static CommandBuilder Create(ICoreServerAPI sapi, string name) => new(sapi, name);

    /// <summary>Sets the command description shown in help.</summary>
    public CommandBuilder WithDescription(string description)
    {
        _description = description ?? "";
        return this;
    }

    /// <summary>Sets the required permission level. Empty string means no special permission.</summary>
    public CommandBuilder WithPermission(string permission)
    {
        _permission = permission ?? "";
        return this;
    }

    /// <summary>Adds a required string argument.</summary>
    public CommandBuilder Arg(string name, System.Func<ICoreServerAPI, IServerPlayer, string[]?>? autocomplete = null)
        => ArgTyped(name, typeof(string), required: true, @default: null, autocomplete);

    /// <summary>Adds a required argument of the specified type.</summary>
    public CommandBuilder Arg<T>(string name, System.Func<ICoreServerAPI, IServerPlayer, string[]?>? autocomplete = null)
        => ArgTyped(name, typeof(T), required: true, @default: null, autocomplete);

    /// <summary>Adds an optional argument of the specified type with a default value.</summary>
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

    /// <summary>Sets the handler invoked when the command is run with valid arguments.</summary>
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

        var parser = BuildParser();
        cmd.WithArgs(parser)
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

    private ICommandArgumentParser BuildParser()
    {
        // Use a single WordOrSaturated parser for all arguments and split manually.
        // This preserves the original CommandBuilder argument parsing semantics.
        return _sapi.ChatCommands.Parsers.Word("args");
    }

    private Dictionary<string, object?>? ResolveArgsFromParsed(TextCommandCallingArgs parsed, string syntax)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        object? rawValue = parsed.Parsers.Count > 0 ? parsed.Parsers[0].GetValue() : null;
        string? rawInput = rawValue?.ToString() ?? "";

        var tokens = rawInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < _args.Count; i++)
        {
            var arg = _args[i];
            string? token = i < tokens.Length ? tokens[i] : null;

            if (string.IsNullOrEmpty(token))
            {
                if (arg.Required)
                {
                    return null;
                }
                result[arg.Name] = arg.Default;
                continue;
            }

            object? value;
            try
            {
                value = arg.Type == typeof(string) ? token
                    : arg.Type == typeof(int) ? (object)int.Parse(token)
                    : arg.Type == typeof(float) ? (object)float.Parse(token, System.Globalization.CultureInfo.InvariantCulture)
                    : arg.Type == typeof(bool) ? (object)bool.Parse(token)
                    : token;
            }
            catch
            {
                return null;
            }

            result[arg.Name] = value;
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
