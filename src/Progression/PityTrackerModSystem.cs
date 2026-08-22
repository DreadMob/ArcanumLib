using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Progression
{
    /// <summary>
    /// ModSystem that initializes and exposes the global <see cref="PityTracker"/>.
    /// Pity tracking is server-side only; this system does not register any client logic.
    /// </summary>
    public class PityTrackerModSystem : ModSystem
    {
        /// <summary>
        /// Creates and initializes the global tracker on the server, unless a consumer has already set one.
        /// Consumers can add legacy fallback keys to <see cref="PityTracker.Current"/> before the first save.
        /// </summary>
        public override void StartServerSide(ICoreServerAPI sapi)
        {
            PityTracker.Current ??= new PityTracker(sapi);
        }

        /// <summary>
        /// Saves any pending data and clears the global instance.
        /// </summary>
        public override void Dispose()
        {
            PityTracker.Current?.Save();
            PityTracker.Current = null;
        }
    }
}
