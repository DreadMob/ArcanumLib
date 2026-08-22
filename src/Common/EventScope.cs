using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Collects event subscriptions and unsubscribes them in reverse order when disposed.
    /// Use this in <see cref="ModSystem.Dispose"/> or <c>IDisposable</c> implementations
    /// to avoid leaking callbacks when a mod system unloads.
    /// </summary>
    public sealed class EventScope : IDisposable
    {
        private readonly List<Action> _unsubscribes = new();
        private readonly ICoreAPI? _api;
        private bool _disposed;

        /// <summary>
        /// Creates a new scope with an optional logger source.
        /// </summary>
        public EventScope(ICoreAPI? api = null)
        {
            _api = api;
        }

        /// <summary>
        /// Subscribe and record the matching unsubscribe action.
        /// </summary>
        /// <param name="subscribe">Called immediately to add the handler.</param>
        /// <param name="unsubscribe">Called in reverse order on dispose.</param>
        public EventScope Add(Action subscribe, Action unsubscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (unsubscribe == null) throw new ArgumentNullException(nameof(unsubscribe));
            if (_disposed) throw new ObjectDisposedException(nameof(EventScope));

            subscribe();
            _unsubscribes.Insert(0, unsubscribe);
            return this;
        }

        /// <summary>
        /// Alias for <see cref="Add"/>.
        /// </summary>
        public EventScope On(Action subscribe, Action unsubscribe) => Add(subscribe, unsubscribe);

        /// <summary>
        /// Unsubscribes all registered callbacks in reverse registration order.
        /// Exceptions are swallowed so earlier unsubscriptions are not blocked.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var unsubscribe in _unsubscribes)
            {
                try
                {
                    unsubscribe();
                }
                catch (Exception ex)
                {
                    _api?.Logger?.Warning("[ArcanumLib] EventScope unsubscribe failed: {0}", ex.Message);
                }
            }

            _unsubscribes.Clear();
        }
    }

    /// <summary>
    /// Helpers for creating event scopes from a Vintage Story API instance.
    /// </summary>
    public static class EventScopeExtensions
    {
        /// <summary>
        /// Creates a new <see cref="EventScope"/> tied to the given API.
        /// </summary>
        public static EventScope CreateEventScope(this ICoreAPI api)
        {
            if (api == null) throw new ArgumentNullException(nameof(api));
            return new EventScope(api);
        }
    }
}
