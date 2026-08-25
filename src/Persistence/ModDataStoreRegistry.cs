using System;
using System.Collections.Concurrent;
using Vintagestory.API.Server;

namespace ArcanumLib.Persistence
{
    /// <summary>
    /// Instance-based registry of per-savegame data stores. Replaces the former
    /// static <c>ConcurrentDictionary</c> on <see cref="ModDataStore" /> so that
    /// stores are scoped to a runtime and disposed with it.
    /// Registered as a service in <see cref="ArcanumLib.Core.ArcanumServiceRegistry" />
    /// and resolved by the <see cref="ModDataStore" /> static facade.
    /// </summary>
    public sealed class ModDataStoreRegistry : IDisposable
    {
        private readonly ConcurrentDictionary<string, IModDataStore> _stores = new();
        private bool _disposed;

        /// <summary>
        /// Gets or creates a versioned data store for the given mod and store id.
        /// </summary>
        /// <typeparam name="T">The data type. Must have a parameterless constructor.</typeparam>
        /// <param name="sapi">The server API.</param>
        /// <param name="modId">The owner mod id.</param>
        /// <param name="storeId">The store id.</param>
        /// <param name="dataVersion">The current schema version. Start at 1 and increment when the data shape changes.</param>
        /// <returns>The store instance.</returns>
        public IModDataStore<T> GetOrCreate<T>(ICoreServerAPI sapi, string modId, string storeId, int dataVersion = 1) where T : new()
        {
            return GetOrCreate(sapi, modId, storeId, dataVersion, () => new T());
        }

        /// <summary>
        /// Gets or creates a versioned data store with a custom factory.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="sapi">The server API.</param>
        /// <param name="modId">The owner mod id.</param>
        /// <param name="storeId">The store id.</param>
        /// <param name="dataVersion">The current schema version.</param>
        /// <param name="factory">Factory for creating a fresh data instance.</param>
        /// <returns>The store instance.</returns>
        public IModDataStore<T> GetOrCreate<T>(ICoreServerAPI sapi, string modId, string storeId, int dataVersion, Func<T> factory)
        {
            if (sapi == null) throw new ArgumentNullException(nameof(sapi));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(modId)) throw new ArgumentException("modId cannot be empty.", nameof(modId));
            if (string.IsNullOrWhiteSpace(storeId)) throw new ArgumentException("storeId cannot be empty.", nameof(storeId));
            if (dataVersion <= 0) throw new ArgumentOutOfRangeException(nameof(dataVersion), "dataVersion must be greater than 0.");

            var key = $"{modId}:{storeId}";
            return (IModDataStore<T>)_stores.GetOrAdd(key, _ => new ModDataStoreInstance<T>(sapi, modId, storeId, dataVersion, factory));
        }

        /// <summary>
        /// Loads all registered stores from the current savegame.
        /// </summary>
        internal void LoadAll(ICoreServerAPI? sapi)
        {
            foreach (var store in _stores.Values)
            {
                try
                {
                    store.Load();
                }
                catch (Exception ex)
                {
                    sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] LoadAll failed for {0}: {1}", store.StoreKey, ex.Message);
                }
            }
        }

        /// <summary>
        /// Saves all registered stores into the current savegame.
        /// </summary>
        internal void SaveAll(ICoreServerAPI? sapi)
        {
            foreach (var store in _stores.Values)
            {
                try
                {
                    store.Save();
                }
                catch (Exception ex)
                {
                    sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] SaveAll failed for {0}: {1}", store.StoreKey, ex.Message);
                }
            }
        }

        /// <summary>
        /// Clears the store registry. Intended for use in tests and shutdown paths.
        /// </summary>
        public void Clear()
        {
            _stores.Clear();
        }

        /// <summary>
        /// Disposes the registry and clears all stores.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stores.Clear();
        }
    }
}
