---
layout: default
title: Inventory / ItemStack helpers
---

# Inventory / ItemStack helpers

## What is it for?

`ArcanumLib.Inventory` contains extension methods for `IPlayer`, `IInventory`, and `ItemStack` that remove the small, repeated loops for giving, finding, counting, and consuming items.

## When to use it

- Give a stack to a player and drop the remainder on the ground if the inventory is full.
- Count how many matching items an inventory contains.
- Consume a specific quantity of an item.
- Find the first slot that matches a condition.
- Check whether two stacks are the same collectible or whether a stack is empty/null.

## Quick example

```csharp
using ArcanumLib.Inventory;

// Give a stack, dropping it at the player if the inventory is full.
player.TryGiveOrDrop(stack);

var inventory = player.InventoryManager.GetOwnInventory("character");

// Count and consume items.
bool hasEnough = inventory.HasAtLeast("game:ingot-iron", 4);
int consumed = inventory.ConsumeItems("game:ingot-iron", 4);

// Find the first matching slot.
var slot = inventory.FindFirst(s => s?.Itemstack?.HasCollectibleCode("game:gear-temporal") == true);
```

## API overview

| Method | Purpose |
|--------|---------|
| `IPlayer.TryGiveOrDrop(stack, world, dropPosition)` | Gives a stack to the player, dropping any overflow. |
| `IServerPlayer.TryGiveOrDrop(stack)` | Server-side overload. |
| `IInventory.CountItems(predicate)` | Counts slots matching a predicate. |
| `IInventory.CountItem(code)` | Counts items with the given collectible code. |
| `IInventory.FindFirst(predicate)` | Returns the first matching `ItemSlot`. |
| `IInventory.ConsumeItems(code, quantity)` | Removes up to `quantity` matching items and returns the amount consumed. |
| `IInventory.HasAtLeast(code, quantity)` | Returns `true` if the inventory contains at least `quantity` matching items. |
| `ItemStack.HasCollectibleCode(code)` | Returns `true` if the stack's collectible matches the code. |
| `ItemStack.IsSameCollectible(other)` | Returns `true` if two stacks share the same collectible. |
| `ItemStack.IsEmptyOrNull()` | Returns `true` for `null` or empty stacks. |

## Notes

- These are extension methods; add `using ArcanumLib.Inventory;` to use them.
- Item codes are standard Vintage Story collectible codes (e.g. `game:ingot-iron`).
- `TryGiveOrDrop` drops overflow at the player's position.
