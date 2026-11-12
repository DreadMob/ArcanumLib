#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using ArcanumLib.Common;
using Vintagestory.API.Util;

namespace ArcanumLib.Network;

/// <summary>What a dispatch did with a packet.</summary>
internal enum PacketDispatch
{
    /// <summary>Not one of ours — hand it to the base block entity.</summary>
    NotMine,
    /// <summary>Decoded, gated and handled.</summary>
    Handled,
    /// <summary>Ours, but refused (gate, bad payload, rate limit, handler error).</summary>
    Dropped
}

/// <summary>
/// Side-agnostic core of <see cref="BlockEntityPackets"/>: message ids, decoding, gate and rate limit. Generic
/// over the player type so it can be tested without an <c>IServerPlayer</c> (the engine's player interfaces
/// cannot be proxied outside the game's own test assembly).
/// </summary>
internal sealed class BlockEntityPacketTable<TPlayer> where TPlayer : class
{
    private sealed class ClientEntry
    {
        public required Type Type;
        public required BlockEntityAction Action;
        public required int MinIntervalMs;
        public required Func<TPlayer, byte[]?, PacketDispatch> Run;
        public long LastAt = long.MinValue / 2;
    }

    private readonly Dictionary<int, ClientEntry> _fromClient = new();
    private readonly Dictionary<int, (Type Type, Func<byte[]?, PacketDispatch> Run)> _fromServer = new();

    /// <summary>Server-side permission check; <see cref="BlockEntityAction.None"/> never reaches it.</summary>
    public Func<TPlayer, BlockEntityAction, bool> Gate;
    /// <summary>Milliseconds clock for the per-message rate limit.</summary>
    public Func<long> Clock;
    /// <summary>Refusal sink (rate-limited by the caller); never gets the payload.</summary>
    public Action<TPlayer?, BlockEntityAction, string> Deny;
    /// <summary>Handler crash sink.</summary>
    public Action<string, Exception> Error;

    public BlockEntityPacketTable(Func<TPlayer, BlockEntityAction, bool> gate, Func<long> clock,
        Action<TPlayer?, BlockEntityAction, string> deny, Action<string, Exception> error)
    {
        Gate = gate; Clock = clock; Deny = deny; Error = error;
    }

    public bool HasClientId(int packetid) => _fromClient.ContainsKey(packetid);
    public bool HasServerId(int packetid) => _fromServer.ContainsKey(packetid);

    public void AddFromClient<T>(Action<TPlayer, T> handler, BlockEntityAction action, int minIntervalMs) where T : class, new()
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        int id = BlockEntityPackets.IdOf<T>();
        if (_fromClient.TryGetValue(id, out var old))
            throw new InvalidOperationException(old.Type == typeof(T)
                ? $"Client message {typeof(T).FullName} is registered twice on one block entity."
                : $"Client messages {old.Type.FullName} and {typeof(T).FullName} hash to the same packet id {id}; rename one.");
        string name = typeof(T).Name;
        _fromClient[id] = new ClientEntry
        {
            Type = typeof(T), Action = action, MinIntervalMs = minIntervalMs,
            Run = (player, data) =>
            {
                if (!TryDecode(data, out T? msg)) { Deny(player, action, "bad-payload:" + name); return PacketDispatch.Dropped; }
                try { handler(player, msg!); }
                catch (Exception e) { Error("Handler for " + name + " failed", e); return PacketDispatch.Dropped; }
                return PacketDispatch.Handled;
            }
        };
    }

    public void AddFromServer<T>(Action<T> handler) where T : class, new()
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        int id = BlockEntityPackets.IdOf<T>();
        if (_fromServer.TryGetValue(id, out var old))
            throw new InvalidOperationException(old.Type == typeof(T)
                ? $"Server message {typeof(T).FullName} is registered twice on one block entity."
                : $"Server messages {old.Type.FullName} and {typeof(T).FullName} hash to the same packet id {id}; rename one.");
        string name = typeof(T).Name;
        _fromServer[id] = (typeof(T), data =>
        {
            if (!TryDecode(data, out T? msg)) return PacketDispatch.Dropped;
            try { handler(msg!); }
            catch (Exception e) { Error("Handler for " + name + " failed", e); return PacketDispatch.Dropped; }
            return PacketDispatch.Handled;
        });
    }

    /// <summary>Server side: a packet a client sent to this block entity.</summary>
    public PacketDispatch DispatchClient(TPlayer? player, int packetid, byte[]? data)
    {
        if (!_fromClient.TryGetValue(packetid, out var entry)) return PacketDispatch.NotMine;
        if (player == null) return PacketDispatch.Dropped;
        if (entry.Action != BlockEntityAction.None && !Gate(player, entry.Action)) return PacketDispatch.Dropped;
        if (entry.MinIntervalMs > 0)
        {
            long now = Clock();
            if (now - entry.LastAt < entry.MinIntervalMs) return PacketDispatch.Dropped;   // GUI spam, not worth a log line
            entry.LastAt = now;
        }
        return entry.Run(player, data);
    }

    /// <summary>Client side: a packet the server sent to this block entity.</summary>
    public PacketDispatch DispatchServer(int packetid, byte[]? data)
        => _fromServer.TryGetValue(packetid, out var entry) ? entry.Run(data) : PacketDispatch.NotMine;

    /// <summary>ProtoBuf decode that never throws: an empty payload is a fresh message (that is how an empty message
    /// travels), anything that fails to read or reads as null is refused.</summary>
    internal static bool TryDecode<T>(byte[]? data, out T? msg) where T : class, new()
    {
        msg = null;
        if (data == null || data.Length == 0) { msg = new T(); return true; }
        try { msg = SerializerUtil.Deserialize<T>(data); }
        catch (Exception) { return false; }   // forged bytes can throw nearly anything while read
        return msg != null;
    }

    internal static byte[] Encode<T>(T msg) where T : class
        => msg == null ? throw new ArgumentNullException(nameof(msg)) : SerializerUtil.Serialize(msg);

    /// <summary>FNV-1a of the UTF-8 full type name.</summary>
    internal static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 16777619; }
        return h;
    }
}
