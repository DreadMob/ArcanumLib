using System;
using Newtonsoft.Json.Linq;

namespace ArcanumLib.Persistence
{
    /// <summary>
    /// Common members for a versioned, per-savegame data store.
    /// </summary>
    public interface IModDataStore
    {
        /// <summary>
        /// The mod that owns this store.
        /// </summary>
        string ModId { get; }

        /// <summary>
        /// The unique store identifier within the mod.
        /// </summary>
        string StoreId { get; }

        /// <summary>
        /// The savegame key used for persistence.
        /// </summary>
        string StoreKey { get; }

        /// <summary>
        /// The current schema version of the stored data.
        /// </summary>
        int DataVersion { get; }

        /// <summary>
        /// Whether the store has been loaded at least once.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Loads the data from the current savegame, applying migrations if needed.
        /// </summary>
        void Load();

        /// <summary>
        /// Saves the current data into the current savegame.
        /// </summary>
        void Save();
    }

    /// <summary>
    /// A typed, versioned per-savegame data store.
    /// </summary>
    /// <typeparam name="T">The data type. Must have a parameterless constructor or a registered factory.</typeparam>
    public interface IModDataStore<T> : IModDataStore
    {
        /// <summary>
        /// The live data object.
        /// </summary>
        T Data { get; }

        /// <summary>
        /// Registers a migration from one schema version to the next.
        /// Migrations must form a continuous chain from the stored version up to <see cref="DataVersion"/>.
        /// </summary>
        /// <param name="fromVersion">The source schema version.</param>
        /// <param name="migration">A function that transforms the previous JSON payload into the next version.</param>
        void RegisterMigration(int fromVersion, Func<JToken, JToken> migration);
    }
}
