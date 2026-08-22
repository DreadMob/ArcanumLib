using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Effects
{
    /// <summary>
    /// ModSystem that ticks the <see cref="StatusEffectManager"/> each game tick.
    /// </summary>
    public class StatusEffectModSystem : ModSystem
    {
        private long _listenerId;

        /// <summary>
        /// Registers the tick listener on the client.
        /// </summary>
        public override void StartClientSide(ICoreClientAPI capi)
        {
            _listenerId = capi.Event.RegisterGameTickListener(dt => StatusEffectManager.Tick(dt), 1000);
        }

        /// <summary>
        /// Registers the tick listener on the server.
        /// </summary>
        public override void StartServerSide(ICoreServerAPI sapi)
        {
            _listenerId = sapi.Event.RegisterGameTickListener(dt => StatusEffectManager.Tick(dt), 1000);
        }

        /// <summary>
        /// Unregisters the tick listener.
        /// </summary>
        public override void Dispose()
        {
            // The listener is already tied to the API lifecycle, but we clear state for tests.
            StatusEffectManager.Clear();
        }
    }
}
