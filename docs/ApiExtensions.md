# ApiExtensions

`ArcanumLib.Common.ApiExtensions` adds `IsClient()` and `IsServer()` helpers for `ICoreAPI`, `ICoreClientAPI`, `ICoreServerAPI`, and `IWorldAccessor`.

## Quick example

```csharp
using ArcanumLib.Common;

if (api.IsServer())
{
    // server-only logic
}

if (world.IsClient())
{
    // client-only logic
}
```
