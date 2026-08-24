using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ArcanumLib.Actions;

/// <summary>
/// Instance-based registry for <see cref="IActionHandler" /> implementations.
/// Mods register their handlers during startup; the registry is then used to
/// execute <see cref="ActionDescriptor" /> instances loaded from JSON assets.
/// </summary>
internal sealed class ActionRegistryService
{
    private readonly Dictionary<string, IActionHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    /// <summary>
    /// Registers an action handler. Replaces any existing handler with the same id.
    /// </summary>
    /// <param name="handler">The handler value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="handler" /> is invalid.</exception>
    public void Register(IActionHandler handler)
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
    /// <param name="handlers">The collection of handlers values.</param>
    public void RegisterAll(IEnumerable<IActionHandler> handlers)
    {
        if (handlers == null) return;
        foreach (var h in handlers) Register(h);
    }

    /// <summary>
    /// Unregisters a handler by id.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public bool Unregister(string id)
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
    /// <param name="id">The identifier.</param>
    /// <returns>The handler, or null if none is found.</returns>
    public IActionHandler? GetHandler(string id)
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
    /// <param name="id">The identifier.</param>
    /// <returns>true if registered; otherwise, false.</returns>
    public bool IsRegistered(string id)
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
    /// <returns>A collection of registered ids values.</returns>
    public IReadOnlyList<string> GetRegisteredIds()
    {
        lock (_syncLock)
        {
            return new List<string>(_handlers.Keys);
        }
    }

    /// <summary>
    /// Validates that an action descriptor can be executed: the handler must
    /// exist and <see cref="IActionHandler.IsAvailable" /> must return true.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="context">The operation context.</param>
    /// <returns>The validate.</returns>
    public ActionResult Validate(ActionDescriptor descriptor, ActionContext context)
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
    /// Exceptions in the handler are caught and returned as <see cref="ActionOutcome.Failed" />.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="context">The operation context.</param>
    /// <returns>The execute.</returns>
    public ActionResult Execute(ActionDescriptor descriptor, ActionContext context)
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
    /// unless <paramref name="continueOnError" /> is true.
    /// </summary>
    /// <param name="descriptors">The collection of descriptors values.</param>
    /// <param name="context">The operation context.</param>
    /// <param name="continueOnError">The continue on error value.</param>
    /// <returns>The list of results, one per descriptor.</returns>
    public List<ActionResult> ExecuteAll(
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
    public void Clear()
    {
        lock (_syncLock)
        {
            _handlers.Clear();
        }
    }
}
