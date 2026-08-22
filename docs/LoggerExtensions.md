---
layout: default
title: LoggerExtensions
---

# LoggerExtensions

## What is it for?

`ArcanumLib.Common.LoggerExtensions` adds non-critical warning logging and `SafeExecute` wrappers to `ICoreAPI`, `ICoreClientAPI`, and `ICoreServerAPI`. It lets you wrap optional operations with a consistent try/catch and context prefix without scattering boilerplate handlers through a mod.

## When to use it

- Wrap code that may throw but should not crash the mod.
- Log non-critical warnings with a context label.
- Provide consistent client, server, and shared API logging helpers.

## Quick example

```csharp
using ArcanumLib.Common;

sapi.SafeExecute("spawn particles", () =>
{
    // code that may throw
});
```

## API overview

| Method | Returns | Description |
|---|---|---|
| `LogNonCriticalWarning(this ICoreAPI, context, ex)` | `void` | Logs a non-critical warning on a shared API. |
| `LogGuiWarning(this ICoreClientAPI, context, ex)` | `void` | Logs a client-side GUI warning. |
| `LogNonCriticalWarning(this ICoreServerAPI, context, ex)` | `void` | Logs a server-side non-critical warning. |
| `SafeExecute(this ICoreAPI, context, Action)` | `void` | Executes an action and logs exceptions via `LogNonCriticalWarning`. |
| `SafeExecute(this ICoreClientAPI, context, Action)` | `void` | Executes an action and logs exceptions via `LogGuiWarning`. |
| `SafeExecute(this ICoreServerAPI, context, Action)` | `void` | Executes an action and logs exceptions via `LogNonCriticalWarning`. |

## Notes

- `SafeExecute` catches all exceptions; it does not rethrow them, so use it only for non-critical work.
- `LogGuiWarning` is useful for client-side GUI code where a failure should be visible in the client log.
