---
layout: default
title: TypedNetworkChannel
nav_order: 110
has_children: true
---

# TypedNetworkChannel

## What is it for?

`ArcanumLib.Network.TypedNetworkChannel` wraps a Vintage Story `IClientNetworkChannel` or `IServerNetworkChannel` to reduce the boilerplate of registering message types, sending packets, and handling them.

## When to use it

- You need a server-to-client or client-to-server channel and want to avoid manual packet serialization setup.
- You want strongly-typed message handlers instead of raw byte arrays.
- You are sending small POCO messages that have a public parameterless constructor.

## Quick example

```csharp
using ArcanumLib.Network;

public class MyMessage { public string Value; }

public override void StartServerSide(ICoreServerAPI sapi)
{
    var ch = new TypedNetworkChannel(sapi, "mymod:mychannel")
        .OnServer<MyMessage>((player, msg) => sapi.Logger.Notification("Got {0} from {1}", msg.Value, player.PlayerName));

    // Broadcast
    ch.Send(new MyMessage { Value = "hello" });

    // Targeted
    ch.SendToPlayer(new MyMessage { Value = "hello you" }, player);

    // To a filtered list
    ch.SendToPlayers(new MyMessage { Value = "hello group" }, onlinePlayers.Where(p => p.HasPrivilege("special")));

    // To everyone except the sender
    ch.SendToAllExcept(new MyMessage { Value = "not you" }, player);
}

public override void StartClientSide(ICoreClientAPI capi)
{
    var ch = new TypedNetworkChannel(capi, "mymod:mychannel")
        .On<MyMessage>(msg => capi.ShowChatMessage($"Server says: {msg.Value}"));

    ch.Send(new MyMessage { Value = "hi" });
}
```

## API overview

`TypedNetworkChannel` automatically detects whether the passed `ICoreAPI` is a client or server API and creates the correct underlying channel.

| Method | Side | Description |
|--------|------|-------------|
| `On<T>(Action<T> handler)` | Client | Register a handler for incoming messages of type `T`. |
| `OnServer<T>(Action<IServerPlayer, T> handler)` | Server | Register a handler for incoming messages of type `T` from a player. |
| `Send<T>(T message)` | Both | Send a message over the channel. On the server, broadcasts to all connected players. |
| `SendToPlayer<T>(T message, IServerPlayer player)` | Server | Send a message to one specific player. |
| `SendToPlayers<T>(T message, IEnumerable<IServerPlayer> players)` | Server | Send a message to a collection of players. |
| `SendToAllExcept<T>(T message, IServerPlayer? exceptPlayer)` | Server | Broadcast to all online players except the given one. |

## Notes

- `OnServer<T>` requires `Action<IServerPlayer, T>`.
- `On<T>` is client-side and requires `Action<T>`.
- `SendToPlayer<T>`, `SendToPlayers<T>`, and `SendToAllExcept<T>` only work on a server channel.
- `Send<T>` on the server is broadcast; use `SendToPlayer` or `SendToPlayers` for targeted delivery.
- Message types must have a public parameterless constructor.
