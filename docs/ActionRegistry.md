---
layout: default
title: ActionRegistry
---

# ActionRegistry

Register typed handlers and execute JSON action descriptors with validation, cooldowns, and permissions.

## What is it for?

The action system provides a reusable registry for executing JSON-declared actions through typed C# handlers. Mods register `IActionHandler` implementations during startup; JSON assets declare `ActionDescriptor` entries that reference the handler by id. The registry validates, executes, and reports outcomes.

This replaces the older `ItemAction` data-only descriptor with a full execution pipeline.

## When to use it

- Your items or blocks have JSON-declared actions that need typed C# execution.
- You want validation, cooldowns, and permission checks handled uniformly.
- You need to execute a sequence of actions with early-exit on failure.
- You want to migrate from `ItemAction` to a richer descriptor without breaking assets.

## Quick example

### Register a handler

```csharp
using ArcanumLib.Actions;

public class TeleportAction : IActionHandler
{
    public string Id => "teleport";

    public bool IsAvailable(ActionContext context)
    {
        return context.PlayerEntity != null;
    }

    public ActionResult Execute(ActionContext context)
    {
        var pos = context.TargetPos;
        if (pos == null) return ActionResult.Invalid("No target position.");
        context.PlayerEntity?.TeleportTo(pos);
        return ActionResult.Success();
    }
}

// In StartServerSide:
ActionRegistry.Register(new TeleportAction());
```

### Declare in JSON

```json
{
    "id": "teleport",
    "args": [],
    "cooldownMs": 5000,
    "requiredPermission": "build"
}
```

### Execute

```csharp
var descriptor = ActionDescriptor.FromJson(jsonString);
var context = new ActionContext(sapi, player, itemSlot, targetPos);
ActionResult result = ActionExecutor.Execute(descriptor, context);

if (result.IsSuccess)
    sapi.Logger.Notification("Action succeeded.");
else
    sapi.Logger.Warning("Action failed: {0}", result.Message);
```

## API overview

### ActionRegistry

| Method | Description |
|--------|-------------|
| `Register(IActionHandler)` | Registers a handler by its `Id`. |
| `RegisterAll(IEnumerable)` | Registers multiple handlers. |
| `Unregister(string)` | Removes a handler. |
| `GetHandler(string)` | Returns the handler or null. |
| `IsRegistered(string)` | Checks if a handler exists. |
| `Validate(descriptor, context)` | Checks availability without executing. |
| `Execute(descriptor, context)` | Validates and executes. |
| `ExecuteAll(descriptors, context, continueOnError)` | Executes a sequence. |
| `Clear()` | Clears all handlers. |

### ActionExecutor

| Method | Description |
|--------|---------|
| `Execute(ActionDescriptor, context)` | Executes with cooldown and permission checks. |
| `GetRemainingCooldown(entityId, actionId)` | Returns remaining cooldown in ms. |
| `ClearCooldowns(entityId)` | Clears cooldowns for a player. |

### ActionDescriptor

| Field | Description |
|-------|-------------|
| `Id` | Handler identifier. |
| `Args` | String arguments passed to the handler. |
| `CooldownMs` | Per-player cooldown in ms. |
| `RequiredPermission` | VS privilege required to execute. |

### ActionResult

| Property | Description |
|----------|-------------|
| `IsSuccess` | True when `Outcome == Success`. |
| `Outcome` | `Success`, `NotAvailable`, `Invalid`, `HandlerNotFound`, `Failed`. |
| `Message` | Optional human-readable detail. |

## Notes

- The registry is thread-safe (locked).
- Cooldowns are per-player per-action-id and tracked server-side.
- `ActionRegistryModSystem` clears state on world unload and player disconnect.
- Handlers should not throw; exceptions are caught and returned as `Failed`.
