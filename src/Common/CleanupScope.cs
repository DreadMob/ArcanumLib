using System;
using System.Collections.Generic;
using ArcanumLib.Performance;
using Vintagestory.API.Common;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Collects disposable resources and cancels them in reverse order when disposed.
    /// Use this to keep cleanup for <see cref="DeferredWork"/> keys, game tick listeners,
    /// and nested <see cref="IDisposable"/> objects in one place.
    /// </summary>
    public sealed class CleanupScope : IDisposable
    {
        private readonly ICoreAPI? _api;
        private readonly List<IDisposable> _disposables = new();
        private readonly List<long> _listenerIds = new();
        private readonly List<string> _deferredKeys = new();
        private bool _disposed;

        /// <summary>
        /// Creates a new cleanup scope with an optional logger source.
        /// </summary>
        public CleanupScope(ICoreAPI? api = null)
        {
            _api = api;
        }

        /// <summary>
        /// Registers a <see cref="DeferredWork"/> key to cancel on dispose.
        /// </summary>
        public CleanupScope AddDeferred(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
            if (_disposed) throw new ObjectDisposedException(nameof(CleanupScope));
            _deferredKeys.Add(key);
            return this;
        }

        /// <summary>
        /// Registers a game tick listener ID to unregister on dispose.
        /// </summary>
        public CleanupScope AddListener(long listenerId)
        {
            if (listenerId == 0) throw new ArgumentException("Listener ID cannot be zero.", nameof(listenerId));
            if (_disposed) throw new ObjectDisposedException(nameof(CleanupScope));
            _listenerIds.Add(listenerId);
            return this;
        }

        /// <summary>
        /// Registers a nested disposable to dispose on dispose.
        /// </summary>
        public CleanupScope Add(IDisposable disposable)
        {
            if (disposable == null) throw new ArgumentNullException(nameof(disposable));
            if (_disposed) throw new ObjectDisposedException(nameof(CleanupScope));
            _disposables.Add(disposable);
            return this;
        }

        /// <summary>
        /// Alias for <see cref="Add(IDisposable)"/>.
        /// </summary>
        public CleanupScope Use(IDisposable disposable) => Add(disposable);

        /// <summary>
        /// Cancels deferred work, unregisters listeners, and disposes nested objects
        /// in reverse registration order. Exceptions are swallowed so earlier cleanup
        /// is not blocked by later failures.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    _disposables[i].Dispose();
                }
                catch (Exception ex)
                {
                    _api?.Logger?.Warning("[ArcanumLib] CleanupScope disposable failed: {0}", ex.Message);
                }
            }
            _disposables.Clear();

            for (int i = _deferredKeys.Count - 1; i >= 0; i--)
            {
                try
                {
                    DeferredWork.Cancel(_deferredKeys[i]);
                }
                catch (Exception ex)
                {
                    _api?.Logger?.Warning("[ArcanumLib] CleanupScope could not cancel '{0}': {1}", _deferredKeys[i], ex.Message);
                }
            }
            _deferredKeys.Clear();

            for (int i = _listenerIds.Count - 1; i >= 0; i--)
            {
                try
                {
                    _api?.Event?.UnregisterGameTickListener(_listenerIds[i]);
                }
                catch (Exception ex)
                {
                    _api?.Logger?.Warning("[ArcanumLib] CleanupScope could not unregister listener {0}: {1}", _listenerIds[i], ex.Message);
                }
            }
            _listenerIds.Clear();
        }
    }

    /// <summary>
    /// Helpers for creating cleanup scopes from a Vintage Story API instance.
    /// </summary>
    public static class CleanupScopeExtensions
    {
        /// <summary>
        /// Creates a new <see cref="CleanupScope"/> tied to the given API.
        /// </summary>
        public static CleanupScope CreateCleanupScope(this ICoreAPI api)
        {
            if (api == null) throw new ArgumentNullException(nameof(api));
            return new CleanupScope(api);
        }
    }
}
