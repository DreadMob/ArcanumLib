# TypedNetworkChannel

`ArcanumLib.Network.TypedNetworkChannel` wraps a Vintage Story `IClientNetworkChannel` or `IServerNetworkChannel` to reduce the boilerplate of registering message types, sending packets, and handling them.

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

## Notes

- The wrapper detects whether the passed `ICoreAPI` is a client or server API.
- `OnServer<T>` requires `Action<IServerPlayer, T>`.
- `On<T>` is client-side and requires `Action<T>`.
- `SendToPlayer<T>` only works on a server channel.
- Message types must have a public parameterless constructor.
