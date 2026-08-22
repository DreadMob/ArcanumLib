---
layout: default
title: Pretty
nav_order: 90
has_children: true
---

# Pretty

Converts raw asset codes and identifiers into human-readable strings.

## What is it for?

When a collectible has no lang entry, the raw code (e.g. `game:metalbit-uranium`) needs to be turned into something readable (`Metalbit Uranium`). `Pretty` strips domains, wildcards, and separator characters, then title-cases the remaining tokens.

## When to use it

- Fallback display name when lang keys are missing.
- Sanitizing user-facing strings that may contain line breaks or VTML markers.
- Pretty-printing asset codes in logs or debug overlays.

## Quick example

```csharp
using ArcanumLib.Text;

string name = Pretty.Readable("metalbit-uranium");
// → "Metalbit Uranium"

string target = Pretty.TargetCode("game:flower-*");
// → "Flower"

string segment = Pretty.LastSegment("game:creature:bear");
// → "Bear"

string clean = Pretty.Sanitize("Hello<br>World\n");
// → "Hello World"
```

## API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `Readable(value)` | `string` | Splits on `-`, `_`, `:`, title-cases each token, joins with spaces. |
| `TargetCode(code)` | `string` | Strips domain, wildcards, and trailing dashes; returns a readable fallback. |
| `LastSegment(value)` | `string` | Returns the last `:`-separated segment, pretty-printed. |
| `Sanitize(value)` | `string` | Removes line breaks, `<br>`, `\n`, collapses spaces, trims. |

## Notes

- All methods accept `null` and return an empty string.
- `Sanitize` handles VTML `<br>` markers and literal `\n` escape sequences.
- `TargetCode` collapses runs of dashes and strips wildcard markers anywhere in the path.