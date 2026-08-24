using System;
using System.Collections.Concurrent;
using Vintagestory.API.Server;

namespace ArcanumLib.Persistence
{
    /// <summary>
    /// Static registry and factory for versioned per-savegame data stores.
    /// </summary>
    public static class ModDataStore
    {
        private static readonly ConcurrentDictionary<string, IModDataStore> _stores = new();
        private static ICoreServerAPI? _sapi;

        /// <summary>
        /// The server API used by the parameterless <see cref="GetOrCreate{T}(string, string, int)" /> overload.
        /// Set automatically by <see cref="ArcanumLib.Core.ArcanumDataModSystem" />.
        /// </summary>
        internal static ICoreServerAPI? Sapi
        {
            get => _sapi;
            set => _sapi = value;
        }

        /// <summary>
        /// Gets or creates a versioned data store for the given mod and store id.
        /// </summary>
        /// <typeparam name="T">The data type. Must have a parameterless constructor.</typeparam>
        /// <param name="sapi">The server API.</param>
        /// <param name="modId">The owner mod id.</param>
        /// <param name="storeId">The store id.</param>
        /// <param name="dataVersion">The current schema version. Start at 1 and increment when the data shape changes.</param>
        /// <returns>The store instance.</returns>
        public static IModDataStore<T> GetOrCreate<T>(ICoreServerAPI sapi, string modId, string storeId, int dataVersion = 1) where T : new()
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
        public static IModDataStore<T> GetOrCreate<T>(ICoreServerAPI sapi, string modId, string storeId, int dataVersion, Func<T> factory)
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
        /// Gets or creates a versioned data store using the globally registered server API.
        /// Requires <see cref="ArcanumLib.Core.ArcanumDataModSystem" /> to have been loaded, or
        /// <see cref="Sapi" /> to have been set manually.
        /// </summary>
        /// <typeparam name="T">The data type. Must have a parameterless constructor.</typeparam>
        /// <param name="modId">The owner mod id.</param>
        /// <param name="storeId">The store id.</param>
        /// <param name="dataVersion">The current schema version.</param>
        /// <returns>The store instance.</returns>
        public static IModDataStore<T> GetOrCreate<T>(string modId, string storeId, int dataVersion = 1) where T : new()
        {
            return GetOrCreate<T>(modId, storeId, dataVersion, () => new T());
        }

        /// <summary>
        /// Gets or creates a versioned data store using the globally registered server API and a custom factory.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="modId">The owner mod id.</param>
        /// <param name="storeId">The store id.</param>
        /// <param name="dataVersion">The current schema version.</param>
        /// <param name="factory">Factory for creating a fresh data instance.</param>
        /// <returns>The store instance.</returns>
        public static IModDataStore<T> GetOrCreate<T>(string modId, string storeId, int dataVersion, Func<T> factory)
        {
            if (_sapi == null)
            {
                throw new InvalidOperationException(
                    "ModDataStore has not been initialized. Call the overload with ICoreServerAPI or ensure ModDataStoreModSystem is loaded.");
            }

            return GetOrCreate(_sapi, modId, storeId, dataVersion, factory);
        }

        /// <summary>
        /// Loads all registered stores from the current savegame.
        /// </summary>
        internal static void LoadAll()
        {
            foreach (var store in _stores.Values)
            {
                try
                {
                    store.Load();
                }
                catch (Exception ex)
                {
                    _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] LoadAll failed for {0}: {1}", store.StoreKey, ex.Message);
                }
            }
        }

        /// <summary>
        /// Saves all registered stores into the current savegame.
        /// </summary>
        internal static void SaveAll()
        {
            foreach (var store in _stores.Values)
            {
                try
                {
                    store.Save();
                }
                catch (Exception ex)
                {
                    _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] SaveAll failed for {0}: {1}", store.StoreKey, ex.Message);
                }
            }
        }

        /// <summary>
        /// Clears the store registry. Intended for use in tests and shutdown paths.
        /// </summary>
        public static void Clear()
        {
            _stores.Clear();
        }
    }
}
