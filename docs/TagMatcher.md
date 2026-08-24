---
layout: default
title: TagMatcher
parent: "ModAssetLoader"
nav_order: 2
---

# TagMatcher

Match collectibles and item stacks against include/exclude tag-sets and code patterns.

## What is it for?

`TagMatcher` provides a fluent, reusable way to filter collectibles by their Vintage Story tags and code patterns. It supports AND/OR semantics across multiple tag groups and wildcard code prefixes for fine-grained filtering.

## When to use it

- Filter items by tag (e.g. "all metal ingots").
- Exclude certain tags (e.g. "not raw ore").
- Combine tag matching with code wildcard patterns (e.g. `game:ingot-*`).
- Build loot filters, recipe validators, or target matchers.

## Quick example

```csharp
using ArcanumLib.Data;

var matcher = new TagMatcher()
    .AddInclude(api.ToCollectibleTagSet("metal"))
    .AddExclude(api.ToCollectibleTagSet("raw"))
    .AddCodePattern("game:ingot-*")
    .SetTagMode(TagMatcher.MatchMode.Any);

bool matches = matcher.Matches(someItemStack);
var filtered = matcher.Filter(allCollectibles);
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `AddInclude(TagSet)` | `TagMatcher` | Adds a required tag-set. Fluid. |
| `AddExclude(TagSet)` | `TagMatcher` | Adds an exclusion tag-set. |
| `AddCodePattern(string)` | `TagMatcher` | Adds a wildcard code pattern (OR-combined). |
| `SetTagMode(MatchMode)` | `TagMatcher` | `Any` (OR) or `All` (AND) for include sets. |
| `Matches(CollectibleObject)` | `bool` | Checks a collectible. |
| `Matches(ItemStack)` | `bool` | Checks a stack's collectible. |
| `Filter(IEnumerable<CollectibleObject>)` | `IEnumerable` | Filters collectibles. |
| `FilterStacks(IEnumerable<ItemStack>)` | `IEnumerable` | Filters stacks. |

### MatchMode

| Mode | Behaviour |
|------|-----------|
| `Any` | A collectible matches if it has at least one tag from any include set. |
| `All` | A collectible must have at least one tag from every include set. |

## Notes

- If no include sets are configured, tag matching is skipped (only code patterns and excludes apply).
- If no code patterns are configured, code matching is skipped.
- Exclude sets are always OR-combined: matching any exclude tag disqualifies the collectible.