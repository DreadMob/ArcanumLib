using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Extension methods for <see cref="ICoreAPI" />, <see cref="ICoreClientAPI" />,
    /// <see cref="ICoreServerAPI" />, and <see cref="IWorldAccessor" />.
    /// </summary>
    public static class ApiExtensions
    {
        /// <summary>
        /// Returns true if the API is running on the client side.
        /// </summary>
        /// <param name="api">The core API instance.</param>
        /// <returns>true if client; otherwise, false.</returns>
        public static bool IsClient(this ICoreAPI api) => api?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the API is running on the server side.
        /// </summary>
        /// <param name="api">The core API instance.</param>
        /// <returns>true if server; otherwise, false.</returns>
        public static bool IsServer(this ICoreAPI api) => api?.Side == EnumAppSide.Server;

        /// <summary>
        /// Returns true if the client API is running on the client side.
        /// </summary>
        /// <param name="capi">The client API instance.</param>
        /// <returns>true if client; otherwise, false.</returns>
        public static bool IsClient(this ICoreClientAPI capi) => capi?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the client API is running on the server side.
        /// </summary>
        /// <param name="capi">The client API instance.</param>
        /// <returns>true if server; otherwise, false.</returns>
        public static bool IsServer(this ICoreClientAPI capi) => capi?.Side == EnumAppSide.Server;

        /// <summary>
        /// Returns true if the world accessor is running on the client side.
        /// </summary>
        /// <param name="world">The world accessor.</param>
        /// <returns>true if client; otherwise, false.</returns>
        public static bool IsClient(this IWorldAccessor? world) => world?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the world accessor is running on the server side.
        /// </summary>
        /// <param name="world">The world accessor.</param>
        /// <returns>true if server; otherwise, false.</returns>
        public static bool IsServer(this IWorldAccessor? world) => world?.Side == EnumAppSide.Server;
    }
}
