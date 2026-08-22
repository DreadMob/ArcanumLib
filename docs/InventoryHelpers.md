---
layout: default
title: Inventory / ItemStack helpers
---

# Inventory / ItemStack helpers

`ArcanumLib.Inventory` contains extension methods for `IPlayer`, `IInventory`, and `ItemStack` that remove the small, repeated loops for giving, finding, counting, and consuming items.

## Quick examples

```csharp
using ArcanumLib.Inventory;

// Give a stack, drop it at the player if the inventory is full.
player.TryGiveOrDrop(stack);

// Count and consume items.
bool hasEnough = player.Inventory.HasAtLeast("game:ingot-iron", 4);
int consumed = player.Inventory.ConsumeItems("game:ingot-iron", 4);

// Find the first matching slot.
var slot = player.Inventory.FindFirst(s => s?.Itemstack?.HasCollectibleCode("game:gear-temporal") == true);
```

## Available helpers

- `IPlayer.TryGiveOrDrop(stack, world, dropPosition)`
- `IServerPlayer.TryGiveOrDrop(stack)`
- `IInventory.CountItems(predicate)`
- `IInventory.CountItem(code)`
- `IInventory.FindFirst(predicate)`
- `IInventory.ConsumeItems(code, quantity)`
- `IInventory.HasAtLeast(code, quantity)`
- `ItemStack.HasCollectibleCode(code)`
- `ItemStack.IsSameCollectible(other)`
- `ItemStack.IsEmptyOrNull()`
