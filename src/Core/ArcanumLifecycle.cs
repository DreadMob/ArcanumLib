using System;
using System.Collections.Generic;
using ArcanumLib.Logging;

namespace ArcanumLib.Core;

/// <summary>
/// Central coordinator for static registries that need to be initialized and disposed with the world.
/// </summary>
public static class ArcanumLifecycle
{
    private readonly struct Registration
    {
        /// <summary>Human-readable name used in diagnostics.</summary>
        public string Name { get; }
        /// <summary>Action invoked when the registry is initialized.</summary>
        public Action Init { get; }
        /// <summary>Action invoked when the registry is disposed.</summary>
        public Action Dispose { get; }

        /// <summary>Creates a lifecycle registration.</summary>
        /// <param name="name">Human-readable name used in diagnostics.</param>
        /// <param name="init">Action to run during initialization.</param>
        /// <param name="dispose">Action to run during disposal.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <see langword="null" />.</exception>
        public Registration(string name, Action init, Action dispose)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Init = init ?? throw new ArgumentNullException(nameof(init));
            Dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }
    }

    private static readonly List<Registration> _registrations = new();
    private static bool _initialized;
    private static readonly object _syncLock = new();

    /// <summary>
    /// Registers a static registry that should be initialized/disposed alongside ArcanumLib.
    /// If <see cref="InitializeAll" /> has already been called, <paramref name="init" /> is invoked immediately.
    /// </summary>
    /// <param name="name">A human-readable name for the registration.</param>
    /// <param name="init">The action to invoke during initialization.</param>
    /// <param name="dispose">The action to invoke during disposal.</param>
    public static void Register(string name, Action init, Action dispose)
    {
        lock (_syncLock)
        {
            _registrations.Add(new Registration(name, init, dispose));
            if (_initialized)
            {
                try { init(); }
                catch (Exception ex)
                {
                    StaticLogSink.Log($"[ArcanumLib] Lifecycle init for '{name}' failed: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Calls the <c>Init</c> action of every registered handler.
    /// </summary>
    public static void InitializeAll()
    {
        List<Registration> copy;
        lock (_syncLock)
        {
            _initialized = true;
            copy = new List<Registration>(_registrations);
        }

        foreach (var reg in copy)
        {
            try { reg.Init(); }
            catch (Exception ex)
            {
                StaticLogSink.Log($"[ArcanumLib] Lifecycle init for '{reg.Name}' failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Calls the <c>Dispose</c> action of every registered handler, in reverse registration order.
    /// </summary>
    public static void DisposeAll()
    {
        List<Registration> copy;
        lock (_syncLock)
        {
            _initialized = false;
            copy = new List<Registration>(_registrations);
            _registrations.Clear();
        }

        for (int i = copy.Count - 1; i >= 0; i--)
        {
            try { copy[i].Dispose(); }
            catch (Exception ex)
            {
                StaticLogSink.Log($"[ArcanumLib] Lifecycle dispose for '{copy[i].Name}' failed: {ex.Message}");
            }
        }
    }
}
