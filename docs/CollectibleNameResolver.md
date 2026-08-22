---
layout: default
title: CollectibleNameResolver
parent: "ApiExtensions"
nav_order: 1
---

# CollectibleNameResolver

Resolves human-readable names and icon codes for items, blocks, and entities.

## What is it for?

Vintage Story collectibles are identified by asset codes like `game:ingot-iron`. Displaying these raw codes to players is ugly. `CollectibleNameResolver` resolves codes to localized display names using lang keys, `ItemStack.GetName()`, and pretty-printed fallbacks. Wildcard patterns (e.g. `game:flower-*`) are supported.

## When to use it

- Display a readable name for an item/block/entity code in a GUI.
- Resolve a wildcard pattern to a concrete collectible name for tooltips or logs.
- Find an icon code for a wildcard pattern.

## Quick example

```csharp
using ArcanumLib.Helpers;

// Resolve a single code.
string name = CollectibleNameResolver.GetDisplayName(api, "game:ingot-iron");
// → "Iron Ingot"

// Resolve a wildcard.
string name = CollectibleNameResolver.GetDisplayName(api, "game:flower-*");
// → "Flower" (or the first matching flower's name)

// Resolve an icon code for a wildcard.
string? iconCode = CollectibleNameResolver.ResolveIconCode(api, "game:flower-*");
// → "game:flower-bluepoppy"
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `GetDisplayName(api, code, mobNameResolver)` | `string` | Resolves a code or wildcard to a display name. |
| `GetCollectibleDisplayName(obj, tryItemStackName, api)` | `string` | Display name for a specific collectible. |
| `ResolveFirstMatchingName(api, prefix, mobNameResolver)` | `string?` | First collectible matching a wildcard prefix with a valid name. |
| `ResolveNameFromLangKey(code, prefix)` | `string?` | Tries generic lang keys like `game:item-flower-*`. |
| `ResolveIconCode(api, code)` | `string?` | Resolves a wildcard to a concrete code for icon rendering. |
| `IsValidDisplayName(obj, name)` | `bool` | True if the name is a real display name, not a code fallback. |
| `MatchesPattern(code, pattern)` | `bool` | Matches a code against a wildcard pattern. |
| `Clear()` | `void` | Clears all caches. Intended for world unload. |

## Notes

- Results are cached per language. The cache is cleared automatically when the language changes.
- A prefix index is built lazily from the world registry to avoid full scans on wildcard lookups.
- Uses `Pretty` and `Wildcard` internally for fallbacks and pattern matching.
- Call `Clear()` on world unload to prevent stale entries from leaking.