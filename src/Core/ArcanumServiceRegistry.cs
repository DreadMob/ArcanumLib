using System;
using System.Collections.Generic;
using ArcanumLib.Logging;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Core;

/// <summary>
/// Instance-based service registry for ArcanumLib. Each <see cref="ArcanumRuntime" />
/// owns one registry instance. ModSystems register their services during <c>Start*</c>
/// and the registry is cleared when the runtime is disposed.
/// </summary>
public sealed class ArcanumServiceRegistry : IDisposable
{
    private readonly Dictionary<(Type, ArcanumServiceScope), object> _services = new();
    private readonly object _syncLock = new();
    private bool _disposed;

    /// <summary>
    /// Registers or replaces a service of type <typeparamref name="T" /> in the given <paramref name="scope" />.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The service instance to register.</param>
    /// <param name="scope">The scope the service belongs to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service" /> is <see langword="null" />.</exception>
    public void Register<T>(T service, ArcanumServiceScope scope = ArcanumServiceScope.Global) where T : class
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        object? previous = null;
        lock (_syncLock)
        {
            var key = (typeof(T), scope);
            if (_services.TryGetValue(key, out previous) && !ReferenceEquals(previous, service))
            {
                ArcanumRuntime.Current?.Api?.Logger?.Warning(
                    "[ArcanumLib] Service {0} in scope {1} is being overwritten; previous instance will be disposed now.",
                    typeof(T).FullName, scope);
            }

            _services[key] = service;
        }

        if (previous != null && !ReferenceEquals(previous, service))
        {
            TryDispose(previous);
        }
    }

    /// <summary>
    /// Removes the registered service of type <typeparamref name="T" /> from the given <paramref name="scope" /> and disposes it if it is disposable.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="scope">The scope to remove the service from.</param>
    public void Unregister<T>(ArcanumServiceScope scope = ArcanumServiceScope.Global) where T : class
    {
        object? removed;
        lock (_syncLock)
        {
            _services.TryGetValue((typeof(T), scope), out removed);
            _services.Remove((typeof(T), scope));
        }
        TryDispose(removed);
    }

    /// <summary>
    /// Returns the registered service of type <typeparamref name="T" /> from the requested <paramref name="scope" />, or null.
    /// If <paramref name="scope" /> is <c>null</c>, any scope is accepted and the first match is returned.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="scope">The scope to search, or <c>null</c> to search all scopes.</param>
    /// <returns>The registered service instance, or <c>null</c> if no service is registered.</returns>
    public T? Get<T>(ArcanumServiceScope? scope = null) where T : class
    {
        lock (_syncLock)
        {
            if (scope.HasValue)
            {
                return _services.TryGetValue((typeof(T), scope.Value), out var service) ? (T)service : null;
            }

            foreach (var s in new[] { ArcanumServiceScope.Global, ArcanumServiceScope.Server, ArcanumServiceScope.Client, ArcanumServiceScope.World })
            {
                if (_services.TryGetValue((typeof(T), s), out var service))
                {
                    return (T)service;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Tries to return the registered service of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">When this method returns, contains the registered service, or <c>null</c> if no service is registered.</param>
    /// <param name="scope">The scope to search, or <c>null</c> to search all scopes.</param>
    /// <returns><c>true</c> if a service of the specified type is registered; otherwise <c>false</c>.</returns>
    public bool TryGet<T>(out T? service, ArcanumServiceScope? scope = null) where T : class
    {
        service = Get<T>(scope);
        return service != null;
    }

    /// <summary>
    /// Returns the existing service of type <typeparamref name="T" /> or creates and registers a new one.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="factory">Factory used to create the service if it is not already registered.</param>
    /// <param name="scope">The scope to register the service in.</param>
    /// <returns>The existing or newly created service instance.</returns>
    public T EnsureInitialized<T>(Func<T> factory, ArcanumServiceScope scope = ArcanumServiceScope.Global) where T : class
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        lock (_syncLock)
        {
            if (_services.TryGetValue((typeof(T), scope), out var existing) && existing is T t)
            {
                return t;
            }

            var created = factory();
            if (created == null) throw new InvalidOperationException($"Factory for {typeof(T).Name} returned null.");
            _services[(typeof(T), scope)] = created;
            return created;
        }
    }

    /// <summary>
    /// Returns the registered service of the given <paramref name="type" />, or null.
    /// </summary>
    /// <param name="type">The service type.</param>
    /// <param name="scope">The scope to search, or <c>null</c> to search all scopes.</param>
    /// <returns>The registered service instance, or <c>null</c> if no service is registered.</returns>
    public object? Get(Type type, ArcanumServiceScope? scope = null)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        lock (_syncLock)
        {
            if (scope.HasValue)
            {
                return _services.TryGetValue((type, scope.Value), out var service) ? service : null;
            }

            foreach (var s in new[] { ArcanumServiceScope.Global, ArcanumServiceScope.Server, ArcanumServiceScope.Client, ArcanumServiceScope.World })
            {
                if (_services.TryGetValue((type, s), out var service))
                {
                    return service;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Clears all registered services in the given <paramref name="scope" /> and disposes them if possible.
    /// If <paramref name="scope" /> is <c>null</c>, all scopes are cleared. Intended for world shutdown.
    /// </summary>
    /// <param name="scope">The scope to clear, or <c>null</c> to clear all.</param>
    public void Shutdown(ArcanumServiceScope? scope = null)
    {
        List<object> toDispose = new();
        lock (_syncLock)
        {
            if (scope.HasValue)
            {
                var keysToRemove = new List<(Type, ArcanumServiceScope)>();
                foreach (var kvp in _services)
                {
                    if (kvp.Key.Item2 == scope.Value)
                    {
                        toDispose.Add(kvp.Value);
                        keysToRemove.Add(kvp.Key);
                    }
                }
                foreach (var key in keysToRemove)
                {
                    _services.Remove(key);
                }
            }
            else
            {
                toDispose = new List<object>(_services.Values);
                _services.Clear();
            }
        }

        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var service in toDispose)
        {
            if (seen.Add(service))
            {
                TryDispose(service);
            }
        }
    }

    /// <summary>
    /// Returns the scope that should be used for a service owned by the given <paramref name="api" />.
    /// </summary>
    /// <param name="api">The API whose side is being tested.</param>
    /// <returns><see cref="ArcanumServiceScope.Client" /> for client APIs, <see cref="ArcanumServiceScope.Server" /> for server APIs, or <see cref="ArcanumServiceScope.Global" /> if unknown.</returns>
    public static ArcanumServiceScope ScopeFor(ICoreAPI? api)
    {
        if (api is ICoreServerAPI) return ArcanumServiceScope.Server;
        if (api is ICoreClientAPI) return ArcanumServiceScope.Client;
        return ArcanumServiceScope.Global;
    }

    /// <summary>
    /// Disposes all registered services and clears the registry.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }

    private static void TryDispose(object? service)
    {
        if (service is IDisposable disposable)
        {
            try { disposable.Dispose(); }
            catch (Exception ex)
            {
                StaticLogSink.Log($"[ArcanumLib] Service disposal failed: {ex.Message}");
            }
        }
    }
}
