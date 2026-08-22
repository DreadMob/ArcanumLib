using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Extension methods for <see cref="ICoreAPI"/>, <see cref="ICoreClientAPI"/>,
    /// <see cref="ICoreServerAPI"/>, and <see cref="IWorldAccessor"/>.
    /// </summary>
    public static class ApiExtensions
    {
        /// <summary>
        /// Returns true if the API is running on the client side.
        /// </summary>
        public static bool IsClient(this ICoreAPI api) => api?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the API is running on the server side.
        /// </summary>
        public static bool IsServer(this ICoreAPI api) => api?.Side == EnumAppSide.Server;

        /// <summary>
        /// Returns true if the client API is running on the client side.
        /// </summary>
        public static bool IsClient(this ICoreClientAPI capi) => capi?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the client API is running on the server side.
        /// </summary>
        public static bool IsServer(this ICoreClientAPI capi) => capi?.Side == EnumAppSide.Server;

        /// <summary>
        /// Returns true if the server API is running on the client side.
        /// </summary>
        public static bool IsClient(this ICoreServerAPI sapi) => sapi?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the server API is running on the server side.
        /// </summary>
        public static bool IsServer(this ICoreServerAPI sapi) => sapi?.Side == EnumAppSide.Server;

        /// <summary>
        /// Returns true if the world accessor is running on the client side.
        /// </summary>
        public static bool IsClient(this IWorldAccessor? world) => world?.Side == EnumAppSide.Client;

        /// <summary>
        /// Returns true if the world accessor is running on the server side.
        /// </summary>
        public static bool IsServer(this IWorldAccessor? world) => world?.Side == EnumAppSide.Server;
    }
}
