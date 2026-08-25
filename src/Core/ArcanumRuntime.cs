using System;
using Vintagestory.API.Common;

namespace ArcanumLib.Core;

/// <summary>
/// Instance-based runtime for ArcanumLib. Each world load creates a new runtime
/// that owns the <see cref="ArcanumServiceRegistry" /> and coordinates lifecycle disposal.
/// The static facades (<see cref="ArcanumServices" />, <see cref="ArcanumLifecycle" />)
/// delegate to <see cref="Current" /> for backward compatibility.
/// </summary>
public sealed class ArcanumRuntime : IDisposable
{
    private static ArcanumRuntime? _current;
    private static readonly object _currentLock = new();
    private bool _disposed;

    /// <summary>
    /// The active runtime instance, or <c>null</c> if no world is loaded.
    /// Accessing this property does not auto-create a runtime; use <see cref="Activate" /> instead.
    /// </summary>
    public static ArcanumRuntime? Current
    {
        get
        {
            lock (_currentLock)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// The service registry for this runtime.
    /// </summary>
    public ArcanumServiceRegistry Services { get; }

    /// <summary>
    /// The core API for the current side, or <c>null</c> if not yet initialized.
    /// </summary>
    public ICoreAPI? Api { get; internal set; }

    /// <summary>
    /// The application side this runtime is running on.
    /// </summary>
    public EnumAppSide Side { get; internal set; }

    /// <summary>
    /// True after the runtime has been fully initialized via <see cref="Initialize" />.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Creates a new runtime instance without activating it.
    /// </summary>
    public ArcanumRuntime()
    {
        Services = new ArcanumServiceRegistry();
    }

    /// <summary>
    /// Creates a new runtime, sets it as <see cref="Current" />, and returns it.
    /// If a runtime is already active, it is disposed first.
    /// </summary>
    /// <returns>The newly activated runtime.</returns>
    public static ArcanumRuntime Activate()
    {
        lock (_currentLock)
        {
            _current?.Dispose();
            var runtime = new ArcanumRuntime();
            _current = runtime;
            return runtime;
        }
    }

    /// <summary>
    /// Marks the runtime as initialized and runs all pending lifecycle init handlers.
    /// </summary>
    public void Initialize()
    {
        if (IsInitialized) return;
        IsInitialized = true;
        ArcanumLifecycle.InitializeAll();
    }

    /// <summary>
    /// Disposes the runtime: runs lifecycle disposal, shuts down all services,
    /// and clears <see cref="Current" /> if this instance is the active one.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            ArcanumLifecycle.DisposeAll();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ArcanumLib] Lifecycle disposal failed: {0}", ex.Message);
        }

        Services.Dispose();

        lock (_currentLock)
        {
            if (_current == this)
            {
                _current = null;
            }
        }
    }
}
