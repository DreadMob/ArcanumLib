using System;
using System.Collections.Generic;
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
        private readonly HashSet<Type> _registeredMessageTypes = new();
        private IClientNetworkChannel? _clientChannel;
        private IServerNetworkChannel? _serverChannel;
        private bool _warnedExistingChannel;

        /// <summary>
        /// Creates a wrapper for the named channel.
        /// </summary>
        /// <param name="api">The core API, either client or server.</param>
        /// <param name="name">The channel name.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="api"/> or <paramref name="name"/> is null.</exception>
        public TypedNetworkChannel(ICoreAPI api, string name)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Registers the channel if it does not already exist for the current API side.
        /// </summary>
        /// <returns>The current channel for method chaining.</returns>
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
        /// <typeparam name="T">The packet type. Must have a parameterless constructor.</typeparam>
        /// <returns>The current channel for method chaining.</returns>
        public TypedNetworkChannel AddMessageType<T>() where T : new()
        {
            EnsureChannel();
            if (_registeredMessageTypes.Add(typeof(T)))
            {
                _serverChannel?.RegisterMessageType<T>();
                _clientChannel?.RegisterMessageType<T>();
            }
            return this;
        }

        /// <summary>
        /// Registers a message type and a server-side handler with the sending player.
        /// </summary>
        /// <typeparam name="T">The packet type. Must have a parameterless constructor.</typeparam>
        /// <param name="handler">The handler invoked when a packet is received from a player.</param>
        /// <returns>The current channel for method chaining.</returns>
        public TypedNetworkChannel OnServer<T>(Action<IServerPlayer, T> handler) where T : new()
        {
            AddMessageType<T>();
            _serverChannel?.SetMessageHandler<T>((player, msg) => handler(player, msg));
            return this;
        }

        /// <summary>
        /// Registers a message type and a client-side handler.
        /// </summary>
        /// <typeparam name="T">The packet type. Must have a parameterless constructor.</typeparam>
        /// <param name="handler">The handler invoked when a packet is received.</param>
        /// <returns>The current channel for method chaining.</returns>
        public TypedNetworkChannel On<T>(Action<T> handler) where T : new()
        {
            AddMessageType<T>();
            _clientChannel?.SetMessageHandler<T>(msg => handler(msg));
            return this;
        }

        /// <summary>
        /// Sends a packet from the current side. On the client the packet is sent to the server.
        /// On the server the packet is broadcast to all currently connected players (no-op if none are connected).
        /// </summary>
        /// <typeparam name="T">The packet type.</typeparam>
        /// <param name="message">The packet to send.</param>
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
        /// <typeparam name="T">The packet type.</typeparam>
        /// <param name="message">The packet to send.</param>
        /// <param name="player">The target player.</param>
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
                var existing = sapi.Network.GetChannel(_name);
                if (existing != null)
                {
                    WarnExistingChannel("server");
                }

                _serverChannel = existing ?? sapi.Network.RegisterChannel(_name);
            }
            else if (_api is ICoreClientAPI capi)
            {
                var existing = capi.Network.GetChannel(_name);
                if (existing != null)
                {
                    WarnExistingChannel("client");
                }

                _clientChannel = existing ?? capi.Network.RegisterChannel(_name);
            }
        }

        private void WarnExistingChannel(string side)
        {
            if (_warnedExistingChannel) return;
            _warnedExistingChannel = true;
            _api.Logger?.Warning("[ArcanumLib] [TypedNetworkChannel] Channel '{0}' already exists on the {1}; " +
                "another mod may be using the same channel name. Message ids may desynchronize.", _name, side);
        }
    }
}
