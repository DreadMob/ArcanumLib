---
layout: default
title: Wildcard
---

# Wildcard

Case-insensitive wildcard matching for asset codes and identifiers.

## What is it for?

Vintage Story asset codes often need to be matched against patterns like `game:flower-*` or `mymod:gear-*-temporal`. `Wildcard` provides fast, allocation-free pattern matching with `*` (any sequence) and `?` (any single character).

## When to use it

- Match collectible codes against wildcard patterns.
- Filter registry entries by prefix or partial patterns.
- Fast-path check: determine if a pattern is a simple `prefix*` before doing a full match.

## Quick example

```csharp
using ArcanumLib.Text;

bool match = Wildcard.Match("game:flower-bluepoppy", "game:flower-*");
// → true

bool match2 = Wildcard.Match("game:ingot-iron", "game:ingot-???n");
// → true

bool isPrefix = Wildcard.IsSimplePrefix("game:flower-*");
// → true (single star at the end)
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `Match(input, pattern)` | `bool` | Case-insensitive wildcard match. `*` = any sequence, `?` = any single char. |
| `IsSimplePrefix(pattern)` | `bool` | True when the pattern is exactly `prefix*` (single star at the end). |

## Notes

- `Match` returns `false` for `null` or empty inputs.
- `IsSimplePrefix` is useful for fast-path registry scans where `StartsWith` can be used instead of full wildcard matching.
- The matching algorithm is iterative (no recursion, no allocations).
