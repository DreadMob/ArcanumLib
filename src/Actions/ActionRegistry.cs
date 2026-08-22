using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// Central registry for <see cref="IActionHandler"/> implementations.
/// Mods register their handlers during startup; the registry is then used to
/// execute <see cref="ActionDescriptor"/> instances loaded from JSON assets.
/// </summary>
public static class ActionRegistry
{
    private static readonly Dictionary<string, IActionHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _syncLock = new();

    /// <summary>
    /// Registers an action handler. Replaces any existing handler with the same id.
    /// </summary>
    public static void Register(IActionHandler handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (string.IsNullOrWhiteSpace(handler.Id))
            throw new ArgumentException("Handler must have a non-empty Id.", nameof(handler));

        lock (_syncLock)
        {
            _handlers[handler.Id] = handler;
        }
    }

    /// <summary>
    /// Registers multiple action handlers.
    /// </summary>
    public static void RegisterAll(IEnumerable<IActionHandler> handlers)
    {
        if (handlers == null) return;
        foreach (var h in handlers) Register(h);
    }

    /// <summary>
    /// Unregisters a handler by id.
    /// </summary>
    public static bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_syncLock)
        {
            return _handlers.Remove(id);
        }
    }

    /// <summary>
    /// Returns the handler for the given id, or null if not registered.
    /// </summary>
    public static IActionHandler? GetHandler(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_syncLock)
        {
            return _handlers.TryGetValue(id, out var h) ? h : null;
        }
    }

    /// <summary>
    /// Returns true if a handler with the given id is registered.
    /// </summary>
    public static bool IsRegistered(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_syncLock)
        {
            return _handlers.ContainsKey(id);
        }
    }

    /// <summary>
    /// Returns a snapshot of all registered handler ids.
    /// </summary>
    public static IReadOnlyList<string> GetRegisteredIds()
    {
        lock (_syncLock)
        {
            return new List<string>(_handlers.Keys);
        }
    }

    /// <summary>
    /// Validates that an action descriptor can be executed: the handler must
    /// exist and <see cref="IActionHandler.IsAvailable"/> must return true.
    /// </summary>
    public static ActionResult Validate(ActionDescriptor descriptor, ActionContext context)
    {
        if (descriptor == null) return ActionResult.Invalid("Action descriptor is null.");
        if (string.IsNullOrWhiteSpace(descriptor.Id))
            return ActionResult.Invalid("Action descriptor has no id.");

        var handler = GetHandler(descriptor.Id);
        if (handler == null)
            return ActionResult.HandlerNotFound($"No handler registered for action '{descriptor.Id}'.");

        try
        {
            if (!handler.IsAvailable(context))
                return ActionResult.NotAvailable($"Action '{descriptor.Id}' is not available in this context.");
        }
        catch (Exception ex)
        {
            return ActionResult.Failed($"IsAvailable threw for '{descriptor.Id}': {ex.Message}");
        }

        return ActionResult.Success();
    }

    /// <summary>
    /// Executes an action descriptor. Validates first, then calls the handler.
    /// Exceptions in the handler are caught and returned as <see cref="ActionOutcome.Failed"/>.
    /// </summary>
    public static ActionResult Execute(ActionDescriptor descriptor, ActionContext context)
    {
        if (descriptor == null) return ActionResult.Invalid("Action descriptor is null.");
        if (string.IsNullOrWhiteSpace(descriptor.Id))
            return ActionResult.Invalid("Action descriptor has no id.");

        var handler = GetHandler(descriptor.Id);
        if (handler == null)
            return ActionResult.HandlerNotFound($"No handler registered for action '{descriptor.Id}'.");

        try
        {
            if (!handler.IsAvailable(context))
                return ActionResult.NotAvailable($"Action '{descriptor.Id}' is not available in this context.");
        }
        catch (Exception ex)
        {
            return ActionResult.Failed($"IsAvailable threw for '{descriptor.Id}': {ex.Message}");
        }

        try
        {
            return handler.Execute(context);
        }
        catch (Exception ex)
        {
            return ActionResult.Failed($"Execute threw for '{descriptor.Id}': {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a sequence of action descriptors in order. Stops at the first failure
    /// unless <paramref name="continueOnError"/> is true.
    /// </summary>
    /// <returns>The list of results, one per descriptor.</returns>
    public static List<ActionResult> ExecuteAll(
        IEnumerable<ActionDescriptor> descriptors,
        ActionContext context,
        bool continueOnError = false)
    {
        var results = new List<ActionResult>();
        if (descriptors == null) return results;

        foreach (var descriptor in descriptors)
        {
            var result = Execute(descriptor, context);
            results.Add(result);
            if (!result.IsSuccess && !continueOnError) break;
        }

        return results;
    }

    /// <summary>
    /// Clears all registered handlers. Intended for world unload / test teardown.
    /// </summary>
    public static void Clear()
    {
        lock (_syncLock)
        {
            _handlers.Clear();
        }
    }
}
