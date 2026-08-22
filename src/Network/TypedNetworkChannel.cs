using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Network
{
    /// <summary>
    /// A thin, typed wrapper around a Vintage Story network channel.
    /// Reduces boilerplate when registering message types, sending packets, and handling them.
    /// Works on both client and server APIs.
    /// </summary>
    public sealed class TypedNetworkChannel
    {
        private readonly ICoreAPI _api;
        private readonly string _name;
        private IClientNetworkChannel? _clientChannel;
        private IServerNetworkChannel? _serverChannel;

        /// <summary>
        /// Creates a wrapper for the named channel.
        /// </summary>
        public TypedNetworkChannel(ICoreAPI api, string name)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Registers the channel if it does not already exist for the current API side.
        /// </summary>
        public TypedNetworkChannel Register()
        {
            if (_serverChannel == null && _clientChannel == null)
            {
                if (_api is ICoreServerAPI sapi) _serverChannel = sapi.Network.RegisterChannel(_name);
                else if (_api is ICoreClientAPI capi) _clientChannel = capi.Network.RegisterChannel(_name);
            }
            return this;
        }

        /// <summary>
        /// Registers a message type on the channel.
        /// </summary>
        public TypedNetworkChannel AddMessageType<T>() where T : new()
        {
            EnsureChannel();
            _serverChannel?.RegisterMessageType<T>();
            _clientChannel?.RegisterMessageType<T>();
            return this;
        }

        /// <summary>
        /// Registers a message type and a server-side handler with the sending player.
        /// </summary>
        public TypedNetworkChannel OnServer<T>(Action<IServerPlayer, T> handler) where T : new()
        {
            EnsureChannel();
            _serverChannel?.RegisterMessageType<T>();
            _serverChannel?.SetMessageHandler<T>((player, msg) => handler(player, msg));
            return this;
        }

        /// <summary>
        /// Registers a message type and a client-side handler.
        /// </summary>
        public TypedNetworkChannel On<T>(Action<T> handler) where T : new()
        {
            EnsureChannel();
            _clientChannel?.RegisterMessageType<T>();
            _clientChannel?.SetMessageHandler<T>(msg => handler(msg));
            return this;
        }

        /// <summary>
        /// Sends a packet from the current side. On the server, no-op if no players are connected.
        /// </summary>
        public void Send<T>(T message)
        {
            if (_serverChannel != null)
            {
                if (_api is ICoreServerAPI sapi && sapi.World?.AllOnlinePlayers?.Length > 0)
                {
                    _serverChannel.SendPacket(message);
                }
            }

            _clientChannel?.SendPacket(message);
        }

        /// <summary>
        /// Sends a packet to a specific player from the server.
        /// </summary>
        public void SendToPlayer<T>(T message, IServerPlayer player)
        {
            if (player == null) return;
            _serverChannel?.SendPacket(message, player);
        }

        private void EnsureChannel()
        {
            if (_serverChannel != null || _clientChannel != null) return;

            if (_api is ICoreServerAPI sapi)
            {
                _serverChannel = sapi.Network.GetChannel(_name) ?? sapi.Network.RegisterChannel(_name);
            }
            else if (_api is ICoreClientAPI capi)
            {
                _clientChannel = capi.Network.GetChannel(_name) ?? capi.Network.RegisterChannel(_name);
            }
        }
    }
}
