using System;
using System.Collections.Generic;

namespace ArcanumLib.Core;

/// <summary>
/// Lightweight, world-scoped service registry for ArcanumLib. ModSystems register
/// their services during <c>Start*</c> and unregister or clear on <c>Dispose</c>.
/// Static public APIs can resolve their backing instances through this registry
/// instead of holding their own static state.
/// </summary>
public static class ArcanumServices
{
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly object _syncLock = new();

    /// <summary>
    /// Registers or replaces a service of type <typeparamref name="T"/>.
    /// </summary>
    public static void Register<T>(T service) where T : class
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        lock (_syncLock)
        {
            _services[typeof(T)] = service;
        }
    }

    /// <summary>
    /// Removes the registered service of type <typeparamref name="T"/>.
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        lock (_syncLock)
        {
            _services.Remove(typeof(T));
        }
    }

    /// <summary>
    /// Returns the registered service of type <typeparamref name="T"/>, or null.
    /// </summary>
    public static T? Get<T>() where T : class
    {
        lock (_syncLock)
        {
            return _services.TryGetValue(typeof(T), out var service) ? (T)service : null;
        }
    }

    /// <summary>
    /// Returns the registered service of the given <paramref name="type"/>, or null.
    /// </summary>
    public static object? Get(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        lock (_syncLock)
        {
            return _services.TryGetValue(type, out var service) ? service : null;
        }
    }

    /// <summary>
    /// Clears all registered services. Intended for world shutdown.
    /// </summary>
    public static void Shutdown()
    {
        lock (_syncLock)
        {
            _services.Clear();
        }
    }
}
