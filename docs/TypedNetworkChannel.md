---
layout: default
title: TypedNetworkChannel
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

    ch.Send(new MyMessage { Value = "hello" });
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
| `Send<T>(T message)` | Both | Send a message over the channel. |
| `SendToPlayer<T>(IServerPlayer player, T message)` | Server | Send a message to one specific player. |

## Notes

- `OnServer<T>` requires `Action<IServerPlayer, T>`.
- `On<T>` is client-side and requires `Action<T>`.
- `SendToPlayer<T>` only works on a server channel.
- Message types must have a public parameterless constructor.
