using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Persistence
{
    /// <summary>
    /// ModSystem that loads and saves all registered <see cref="IModDataStore"/> instances
    /// at the appropriate world lifecycle events.
    /// </summary>
    public class ModDataStoreModSystem : ModSystem
    {
        /// <summary>
        /// Registers save/load event handlers on the server.
        /// </summary>
        public override void StartServerSide(ICoreServerAPI sapi)
        {
            ModDataStore.Sapi = sapi;

            sapi.Event.SaveGameLoaded += OnSaveGameLoaded;
            sapi.Event.SaveGameCreated += OnSaveGameCreated;
            sapi.Event.GameWorldSave += OnGameWorldSave;
        }

        /// <summary>
        /// Detaches save/load event handlers.
        /// </summary>
        public override void Dispose()
        {
            if (ModDataStore.Sapi is ICoreServerAPI sapi)
            {
                sapi.Event.SaveGameLoaded -= OnSaveGameLoaded;
                sapi.Event.SaveGameCreated -= OnSaveGameCreated;
                sapi.Event.GameWorldSave -= OnGameWorldSave;
            }

            ModDataStore.Sapi = null;
            ModDataStore.Clear();
        }

        private void OnSaveGameLoaded()
        {
            ModDataStore.LoadAll();
        }

        private void OnSaveGameCreated()
        {
            ModDataStore.LoadAll();
        }

        private void OnGameWorldSave()
        {
            ModDataStore.SaveAll();
        }
    }
}
