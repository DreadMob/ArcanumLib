---
layout: default
title: CommandBuilder
nav_order: 51
---

# CommandBuilder

Fluent command framework for Vintage Story server commands.

## What is it for?

Vintage Story commands require manual argument parsing, permission checks, and autocomplete wiring. `CommandBuilder` provides a fluent API with typed arguments, permission gating, autocomplete, and a clean handler signature.

## When to use it

- Register admin or player-facing commands without manual `CmdArgs` parsing.
- Add typed arguments (string, int, float, bool) with autocomplete.
- Gate commands behind permission levels.

## Quick example

```csharp
using ArcanumLib.Commands;

CommandBuilder.Create(sapi, "mymod.give")
    .WithDescription("Give an item to a player")
    .WithPermission("admin")
    .Arg<string>("item", (api, player) => new[] { "game:ingot-iron", "game:ingot-gold" })
    .Arg("count", 1)
    .OnExecute((api, player, args) =>
    {
        string itemCode = args.String("item");
        int count = args.IntOr("count", 1);
        // ... give item
        player.SendMessage(GlobalConstants.GeneralChatGroup,
            $"Gave {count}x {itemCode}", EnumChatType.CommandSuccess);
    })
    .Register();
```

## API overview

### `CommandBuilder.Create(ICoreServerAPI sapi, string name)`

Creates a new builder. Command names should include a mod prefix, e.g. `mymod.give`.

### `.WithDescription(string)`

Sets the command description shown in help.

### `.WithPermission(string)`

Sets the required permission. Empty string means no special permission.

### `.Arg(string name, autocomplete?)`

Adds a required string argument. Optional autocomplete function returns candidate values.

### `.Arg<T>(string name, autocomplete?)`

Adds a required argument of type `T` (`string`, `int`, `float`, `bool`).

### `.Arg<T>(string name, T defaultValue, autocomplete?)`

Adds an optional argument with a default value.

### `.OnExecute(Action<ICoreServerAPI, IServerPlayer, CommandArgs>)`

Sets the handler invoked when the command runs with valid arguments.

### `.Register()`

Registers the command with the server API. Throws if called twice or without a handler.

## CommandArgs

| Method | Returns | Description |
|--------|---------|-------------|
| `String(name)` | `string` | Required string argument. |
| `Int(name)` | `int` | Required int argument. |
| `Float(name)` | `float` | Required float argument. |
| `Bool(name)` | `bool` | Required bool argument. |
| `StringOr(name, fallback)` | `string` | Optional string with fallback. |
| `IntOr(name, fallback)` | `int` | Optional int with fallback. |
| `FloatOr(name, fallback)` | `float` | Optional float with fallback. |
| `BoolOr(name, fallback)` | `bool` | Optional bool with fallback. |
| `Has(name)` | `bool` | True if the argument was provided. |

## Notes

- Arguments are parsed from `CmdArgs.PopWord()` in declaration order.
- Failed parsing sends a command error to the player and aborts.
- Autocomplete exceptions are logged and swallowed.
- The handler runs inside a try/catch; errors are logged and sent to the player.
