---
layout: default
title: LoggerExtensions
---

# LoggerExtensions

`ArcanumLib.Common.LoggerExtensions` adds non-critical warning logging and `SafeExecute` wrappers to `ICoreAPI`, `ICoreClientAPI`, and `ICoreServerAPI`.

## Quick example

```csharp
using ArcanumLib.Common;

sapi.SafeExecute("spawn particles", () =>
{
    // code that may throw
});
```
