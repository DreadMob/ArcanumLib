---
layout: default
title: ItemStackBuilder
---

# ItemStackBuilder

Fluent builder for constructing `ItemStack` instances with attributes, durability, and stack size.

## What is it for?

`ItemStackBuilder` removes the boilerplate of creating `ItemStack` instances with specific collectible codes, attributes, durability, and stack sizes. It is useful for loot tables, quest rewards, test fixtures, and any code that constructs stacks programmatically.

## When to use it

- Build a stack with a specific code and attributes in one fluent chain.
- Seed an `ItemStack` from an existing stack and modify a few fields.
- Set durability, stack size, and custom attributes without verbose `TreeAttribute` calls.

## Quick example

```csharp
using ArcanumLib.Inventory;

var stack = new ItemStackBuilder()
    .Code("game:ingot-iron")
    .Count(4)
    .Attribute("custom", "value")
    .Durability(100)
    .Build(api);
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `Code(string)` | `ItemStackBuilder` | Sets the collectible code. |
| `Code(AssetLocation)` | `ItemStackBuilder` | Sets the collectible code. |
| `Count(int)` | `ItemStackBuilder` | Sets the stack size. |
| `Durability(int)` | `ItemStackBuilder` | Sets the durability attribute. |
| `ItemClass(EnumItemClass)` | `ItemStackBuilder` | Forces Item or Block lookup. |
| `Attribute(key, value)` | `ItemStackBuilder` | Sets a string/int/float/bool attribute. |
| `WatchedAttribute(key, value)` | `ItemStackBuilder` | Sets a watched attribute. |
| `RemoveAttribute(key)` | `ItemStackBuilder` | Removes an attribute. |
| `Build(ICoreAPI)` | `ItemStack?` | Builds the stack. Returns null if not found. |
| `BuildOrThrow(ICoreAPI)` | `ItemStack` | Builds, throwing if the collectible is not found. |
| `Clear()` | `ItemStackBuilder` | Resets the builder. |

### Seeding from an existing stack

```csharp
var builder = new ItemStackBuilder(existingStack)
    .Count(8)
    .Attribute("enchanted", true);
```

## Notes

- `Build` returns `null` if the collectible code is not found in the registry.
- The builder does not modify the source stack when seeded from one.
- Attributes are cloned on seed; modifying the builder does not affect the source.
