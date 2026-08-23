using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Server;

namespace ArcanumLib.Persistence
{
    /// <summary>
    /// Generic implementation of a versioned per-savegame data store.
    /// </summary>
    /// <typeparam name="T">The data type stored by the consumer.</typeparam>
    public class ModDataStoreInstance<T> : IModDataStore<T>
    {
        private readonly ICoreServerAPI? _sapi;
        private readonly Func<T> _factory;
        private readonly List<(int fromVersion, Func<JToken, JToken> migration)> _migrations = new();
        private T? _data;
        private bool _isLoaded;
        private bool _isDirty;

        /// <summary>
        /// The mod that owns this store.
        /// </summary>
        public string ModId { get; }

        /// <summary>
        /// The unique store identifier within the mod.
        /// </summary>
        public string StoreId { get; }

        /// <summary>
        /// The savegame key used for persistence.
        /// </summary>
        public string StoreKey { get; }

        /// <summary>
        /// The current schema version of the stored data.
        /// </summary>
        public int DataVersion { get; }

        /// <summary>
        /// Whether the store has been loaded at least once.
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// Whether the live data has changed since the last save.
        /// </summary>
        public bool IsDirty => _isDirty;

        /// <summary>
        /// Marks the store as dirty so the next <see cref="Save"/> will persist the data.
        /// </summary>
        public void MarkDirty() => _isDirty = true;

        /// <summary>
        /// The live data object.
        /// </summary>
        public T Data
        {
            get
            {
                if (!_isLoaded)
                {
                    Load();
                }

                return _data!;
            }
        }

        /// <summary>
        /// Creates a new store instance.
        /// </summary>
        /// <param name="sapi">The server API. May be null in unit tests, in which case Load/Save are no-ops.</param>
        /// <param name="modId">The owner mod id.</param>
        /// <param name="storeId">The store id.</param>
        /// <param name="dataVersion">The current schema version.</param>
        /// <param name="factory">Factory for creating a fresh data instance.</param>
        public ModDataStoreInstance(ICoreServerAPI? sapi, string modId, string storeId, int dataVersion, Func<T> factory)
        {
            if (string.IsNullOrWhiteSpace(modId))
                throw new ArgumentException("Mod id cannot be empty.", nameof(modId));
            if (string.IsNullOrWhiteSpace(storeId))
                throw new ArgumentException("Store id cannot be empty.", nameof(storeId));
            if (dataVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(dataVersion), "Data version must be at least 1.");
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _sapi = sapi;
            _factory = factory;
            ModId = modId;
            StoreId = storeId;
            DataVersion = dataVersion;
            StoreKey = $"arcanumlib:md:{modId}:{storeId}";
        }

        /// <summary>
        /// Registers a migration from one schema version to the next.
        /// </summary>
        /// <param name="fromVersion">The source schema version.</param>
        /// <param name="migration">A function that transforms the previous JSON payload into the next version.</param>
        public void RegisterMigration(int fromVersion, Func<JToken, JToken> migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));

            _migrations.Add((fromVersion, migration));
        }

        /// <summary>
        /// Loads the data from the current savegame, applying migrations if needed.
        /// </summary>
        public void Load()
        {
            _data = _factory();
            _isDirty = false;

            var saveGame = _sapi?.WorldManager?.SaveGame;
            if (saveGame == null)
            {
                _isLoaded = true;
                return;
            }

            try
            {
                var bytes = saveGame.GetData(StoreKey);
                if (bytes == null || bytes.Length == 0)
                {
                    _isLoaded = true;
                    return;
                }

                var json = Encoding.UTF8.GetString(bytes);
                var envelope = JsonConvert.DeserializeObject<ModDataStoreEnvelope>(json);
                if (envelope == null)
                {
                    _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] Could not parse stored data for {0}, using defaults.", StoreKey);
                    _isLoaded = true;
                    return;
                }

                if (envelope.Version > DataVersion)
                {
                    _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] Stored version {0} is newer than supported version {1} for {2}, using defaults.",
                        envelope.Version, DataVersion, StoreKey);
                    _isLoaded = true;
                    return;
                }

                var token = JToken.Parse(envelope.Payload);

                for (int version = envelope.Version; version < DataVersion; version++)
                {
                    var migrator = _migrations.FirstOrDefault(m => m.fromVersion == version).migration;
                    if (migrator == null)
                    {
                        _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] Missing migration from version {0} for {1}, using defaults.",
                            version, StoreKey);
                        _data = _factory();
                        _isLoaded = true;
                        return;
                    }

                    token = migrator(token);
                    if (token == null)
                    {
                        _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] Migration from version {0} returned null for {1}, using defaults.",
                            version, StoreKey);
                        _data = _factory();
                        _isLoaded = true;
                        return;
                    }
                }

                _data = JsonConvert.DeserializeObject<T>(token.ToString());
                if (_data == null)
                {
                    _data = _factory();
                }
            }
            catch (Exception ex)
            {
                _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] Failed to load {0}: {1}", StoreKey, ex.Message);
                _data = _factory();
            }

            _isLoaded = true;
            _isDirty = false;
        }

        /// <summary>
        /// Saves the current data into the current savegame if <see cref="IsDirty"/> is true.
        /// Resets the dirty flag on success.
        /// </summary>
        public void Save()
        {
            if (!_isDirty) return;

            var saveGame = _sapi?.WorldManager?.SaveGame;
            if (saveGame == null)
            {
                return;
            }

            try
            {
                var payload = JsonConvert.SerializeObject(Data);
                var envelope = new ModDataStoreEnvelope { Version = DataVersion, Payload = payload };
                var json = JsonConvert.SerializeObject(envelope);
                var bytes = Encoding.UTF8.GetBytes(json);
                saveGame.StoreData(StoreKey, bytes);
                _isDirty = false;
            }
            catch (Exception ex)
            {
                _sapi?.Logger?.Warning("[ArcanumLib] [ModDataStore] Failed to save {0}: {1}", StoreKey, ex.Message);
            }
        }

    }
}
