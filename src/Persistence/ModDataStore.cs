using System;
using ArcanumLib.Core;
using Vintagestory.API.Server;

namespace ArcanumLib.Persistence
{
    /// <summary>
    /// Static facade over the instance-based <see cref="ModDataStoreRegistry" />.
    /// The registry is resolved through <see cref="ArcanumServices" /> and lives
    /// in the current <see cref="ArcanumRuntime" />, so stores are scoped to a
    /// world and disposed with it. The static API is preserved for backward
    /// compatibility with existing callers.
    /// </summary>
    public static class ModDataStore
    {
        /// <summary>
        /// Resolves the active <see cref="ModDataStoreRegistry" /> from
        /// <see cref="ArcanumServices" />, or <c>null</c> if no runtime is active.
        /// </summary>
        private static ModDataStoreRegistry? ResolveRegistry()
            => ArcanumServices.Get<ModDataStoreRegistry>();

        /// <summary>
        /// Resolves the server API registered in <see cref="ArcanumServices" /> under
        /// <see cref="ArcanumServiceScope.Server" />. Returns <c>null</c> if no server API
        /// has been registered (e.g., before <see cref="ArcanumLib.Core.ArcanumDataModSystem" />
        /// has started or after it has been disposed).
        /// </summary>
        private static ICoreServerAPI? ResolveSapi()
            => ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server);

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
            var registry = ResolveRegistry()
                ?? throw new InvalidOperationException(
                    "ModDataStoreRegistry is not registered. Ensure ArcanumDataModSystem has started and registered the registry in ArcanumServices.");
            return registry.GetOrCreate(sapi, modId, storeId, dataVersion, factory);
        }

        /// <summary>
        /// Gets or creates a versioned data store using the globally registered server API.
        /// Requires <see cref="ArcanumLib.Core.ArcanumDataModSystem" /> to have registered the
        /// server API in <see cref="ArcanumServices" /> under <see cref="ArcanumServiceScope.Server" />.
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
            var sapi = ResolveSapi();
            if (sapi == null)
            {
                throw new InvalidOperationException(
                    "ModDataStore has not been initialized. Call the overload with ICoreServerAPI or ensure ArcanumDataModSystem is loaded and has registered the server API in ArcanumServices.");
            }

            return GetOrCreate(sapi, modId, storeId, dataVersion, factory);
        }

        /// <summary>
        /// Loads all registered stores from the current savegame.
        /// </summary>
        internal static void LoadAll()
        {
            var registry = ResolveRegistry();
            if (registry == null) return;
            var sapi = ResolveSapi();
            registry.LoadAll(sapi);
        }

        /// <summary>
        /// Saves all registered stores into the current savegame.
        /// </summary>
        internal static void SaveAll()
        {
            var registry = ResolveRegistry();
            if (registry == null) return;
            var sapi = ResolveSapi();
            registry.SaveAll(sapi);
        }

        /// <summary>
        /// Clears the store registry. Intended for use in tests and shutdown paths.
        /// </summary>
        public static void Clear()
        {
            ResolveRegistry()?.Clear();
        }
    }
}
