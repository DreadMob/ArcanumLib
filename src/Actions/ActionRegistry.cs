using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// Static facade for the <see cref="ActionRegistryService" />.
/// Mods register their handlers during startup; the registry is then used to
/// execute <see cref="ActionDescriptor" /> instances loaded from JSON assets.
/// </summary>
public static class ActionRegistry
{
    private static ActionRegistryService Service =>
        ArcanumServices.Get<ActionRegistryService>()
        ?? throw new InvalidOperationException(
            "ActionRegistry has not been initialized. Ensure ActionRegistryModSystem is loaded.");

    /// <summary>
    /// Registers an action handler. Replaces any existing handler with the same id.
    /// </summary>
    /// <param name="handler">The handler value.</param>
    public static void Register(IActionHandler handler) => Service.Register(handler);

    /// <summary>
    /// Registers multiple action handlers.
    /// </summary>
    /// <param name="handlers">The collection of handlers values.</param>
    public static void RegisterAll(IEnumerable<IActionHandler> handlers) => Service.RegisterAll(handlers);

    /// <summary>
    /// Unregisters a handler by id.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public static bool Unregister(string id) => Service.Unregister(id);

    /// <summary>
    /// Returns the handler for the given id, or null if not registered.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The handler, or null if none is found.</returns>
    public static IActionHandler? GetHandler(string id) => Service.GetHandler(id);

    /// <summary>
    /// Returns true if a handler with the given id is registered.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>true if registered; otherwise, false.</returns>
    public static bool IsRegistered(string id) => Service.IsRegistered(id);

    /// <summary>
    /// Returns a snapshot of all registered handler ids.
    /// </summary>
    /// <returns>A collection of registered ids values.</returns>
    public static IReadOnlyList<string> GetRegisteredIds() => Service.GetRegisteredIds();

    /// <summary>
    /// Validates that an action descriptor can be executed: the handler must
    /// exist and <see cref="IActionHandler.IsAvailable" /> must return true.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="context">The operation context.</param>
    /// <returns>The validate.</returns>
    public static ActionResult Validate(ActionDescriptor descriptor, ActionContext context)
        => Service.Validate(descriptor, context);

    /// <summary>
    /// Executes an action descriptor. Validates first, then calls the handler.
    /// Exceptions in the handler are caught and returned as <see cref="ActionOutcome.Failed" />.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="context">The operation context.</param>
    /// <returns>The execute.</returns>
    public static ActionResult Execute(ActionDescriptor descriptor, ActionContext context)
        => Service.Execute(descriptor, context);

    /// <summary>
    /// Executes a sequence of action descriptors in order. Stops at the first failure
    /// unless <paramref name="continueOnError" /> is true.
    /// </summary>
    /// <param name="descriptors">The collection of descriptors values.</param>
    /// <param name="context">The operation context.</param>
    /// <param name="continueOnError">The continue on error value.</param>
    /// <returns>The list of results, one per descriptor.</returns>
    public static List<ActionResult> ExecuteAll(
        IEnumerable<ActionDescriptor> descriptors,
        ActionContext context,
        bool continueOnError = false)
        => Service.ExecuteAll(descriptors, context, continueOnError);

    /// <summary>
    /// Clears all registered handlers. Intended for world unload / test teardown.
    /// </summary>
    public static void Clear() => Service.Clear();
}
