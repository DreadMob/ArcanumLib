using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Core;

/// <summary>
/// The scope of a service registered in <see cref="ArcanumServiceRegistry" />.
/// </summary>
public enum ArcanumServiceScope
{
    /// <summary>
    /// Service is shared across client and server (e.g., common data or utilities).
    /// </summary>
    Global,

    /// <summary>
    /// Service belongs to the client side.
    /// </summary>
    Client,

    /// <summary>
    /// Service belongs to the server side.
    /// </summary>
    Server,

    /// <summary>
    /// Service is tied to the currently loaded world and is cleared on world unload.
    /// </summary>
    World
}

/// <summary>
/// Static facade over <see cref="ArcanumRuntime.Current" />.<see cref="ArcanumRuntime.Services" />.
/// Delegates all operations to the active runtime's service registry.
/// For instance-based access, use <see cref="ArcanumRuntime.Current" />.<see cref="ArcanumRuntime.Services" /> directly.
/// </summary>
public static class ArcanumServices
{
    private static ArcanumServiceRegistry? Registry => ArcanumRuntime.Current?.Services;

    /// <summary>
    /// Registers or replaces a service of type <typeparamref name="T" /> in the given <paramref name="scope" />.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The service instance to register.</param>
    /// <param name="scope">The scope the service belongs to.</param>
    /// <exception cref="InvalidOperationException">Thrown when no <see cref="ArcanumRuntime" /> is active.</exception>
    public static void Register<T>(T service, ArcanumServiceScope scope = ArcanumServiceScope.Global) where T : class
    {
        var registry = Registry
            ?? throw new InvalidOperationException(
                "ArcanumRuntime is not initialized. Ensure ArcanumLibModSystem has started.");
        registry.Register<T>(service, scope);
    }

    /// <summary>
    /// Removes the registered service of type <typeparamref name="T" /> from the given <paramref name="scope" /> and disposes it if it is disposable.
    /// No-op when no runtime is active.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="scope">The scope to remove the service from.</param>
    public static void Unregister<T>(ArcanumServiceScope scope = ArcanumServiceScope.Global) where T : class
        => Registry?.Unregister<T>(scope);

    /// <summary>
    /// Returns the registered service of type <typeparamref name="T" /> from the requested <paramref name="scope" />, or null.
    /// If <paramref name="scope" /> is <c>null</c>, any scope is accepted and the first match is returned.
    /// Returns <c>null</c> when no runtime is active.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="scope">The scope to search, or <c>null</c> to search all scopes.</param>
    /// <returns>The registered service instance, or <c>null</c> if no service is registered or no runtime is active.</returns>
    public static T? Get<T>(ArcanumServiceScope? scope = null) where T : class
        => Registry?.Get<T>(scope);

    /// <summary>
    /// Tries to return the registered service of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">When this method returns, contains the registered service, or <c>null</c> if no service is registered.</param>
    /// <param name="scope">The scope to search, or <c>null</c> to search all scopes.</param>
    /// <returns><c>true</c> if a service of the specified type is registered; otherwise <c>false</c>.</returns>
    public static bool TryGet<T>(out T? service, ArcanumServiceScope? scope = null) where T : class
    {
        service = Registry?.Get<T>(scope);
        return service != null;
    }

    /// <summary>
    /// Returns the existing service of type <typeparamref name="T" /> or creates and registers a new one.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="factory">Factory used to create the service if it is not already registered.</param>
    /// <param name="scope">The scope to register the service in.</param>
    /// <returns>The existing or newly created service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no <see cref="ArcanumRuntime" /> is active.</exception>
    public static T EnsureInitialized<T>(Func<T> factory, ArcanumServiceScope scope = ArcanumServiceScope.Global) where T : class
    {
        var registry = Registry
            ?? throw new InvalidOperationException(
                "ArcanumRuntime is not initialized. Ensure ArcanumLibModSystem has started.");
        return registry.EnsureInitialized<T>(factory, scope);
    }

    /// <summary>
    /// Returns the registered service of the given <paramref name="type" />, or null.
    /// Returns <c>null</c> when no runtime is active.
    /// </summary>
    /// <param name="type">The service type.</param>
    /// <param name="scope">The scope to search, or <c>null</c> to search all scopes.</param>
    /// <returns>The registered service instance, or <c>null</c> if no service is registered or no runtime is active.</returns>
    public static object? Get(Type type, ArcanumServiceScope? scope = null)
        => Registry?.Get(type, scope);

    /// <summary>
    /// Clears all registered services in the given <paramref name="scope" /> and disposes them if possible.
    /// If <paramref name="scope" /> is <c>null</c>, all scopes are cleared. No-op when no runtime is active.
    /// </summary>
    /// <param name="scope">The scope to clear, or <c>null</c> to clear all.</param>
    public static void Shutdown(ArcanumServiceScope? scope = null)
        => Registry?.Shutdown(scope);

    /// <summary>
    /// Returns the scope that should be used for a service owned by the given <paramref name="api" />.
    /// </summary>
    /// <param name="api">The API whose side is being tested.</param>
    /// <returns><see cref="ArcanumServiceScope.Client" /> for client APIs, <see cref="ArcanumServiceScope.Server" /> for server APIs, or <see cref="ArcanumServiceScope.Global" /> if unknown.</returns>
    public static ArcanumServiceScope ScopeFor(ICoreAPI? api)
        => ArcanumServiceRegistry.ScopeFor(api);
}
