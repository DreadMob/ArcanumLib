---
layout: default
title: Misc Helpers
nav_order: 99
description: Small syntactic-sugar helpers that remove boilerplate but are not killer features on their own.
---

# Misc Helpers

Small helpers that remove boilerplate. Some are thin syntactic sugar over existing Vintage Story APIs. They are collected here so they do not dilute the main feature pages.

---

## ApiExtensions

`ArcanumLib.Common.ApiExtensions` — `IsClient()` / `IsServer()` checks for `ICoreAPI`, `ICoreClientAPI`, `ICoreServerAPI`, and `IWorldAccessor`. Sugar for `api.Side == EnumAppSide.Client`.

```csharp
using ArcanumLib.Common;

if (api.IsServer()) { /* server-only logic */ }
if (world.IsClient()) { /* client-only logic */ }
```

All overloads are null-safe and return `false` when the target is `null`.

---

## RGBA

`ArcanumLib.Gui.Theme.RGBA` — immutable color struct normalized to `0..1`, designed for `Cairo.Context`.

```csharp
using ArcanumLib.Gui.Theme;

var color = RGBA.From(255, 128, 64, 0.9);
var faded = color.WithAlpha(0.5);
var mid = color1.Lerp(color2, 0.5);
color.Apply(ctx); // sets Cairo source
```

Also: `ParseHexColor("#RRGGBB")`, `FromArgb(0xAARRGGBB)`.

---

## Pretty

`ArcanumLib.Text.Pretty` — converts raw asset codes into human-readable strings.

```csharp
using ArcanumLib.Text;

Pretty.Readable("metalbit-uranium");    // → "Metalbit Uranium"
Pretty.TargetCode("game:flower-*");     // → "Flower"
Pretty.LastSegment("game:creature:bear"); // → "Bear"
Pretty.Sanitize("Hello<br>World\n");    // → "Hello World"
```

All methods accept `null` and return an empty string.

---

## Wildcard

`ArcanumLib.Text.Wildcard` — case-insensitive `*` / `?` matching for asset codes.

```csharp
using ArcanumLib.Text;

Wildcard.Match("game:flower-bluepoppy", "game:flower-*"); // → true
Wildcard.Match("game:ingot-iron", "game:ingot-???n");     // → true
Wildcard.IsSimplePrefix("game:flower-*");                  // → true
```

Iterative, no allocations. `IsSimplePrefix` is useful for fast-path registry scans where `StartsWith` suffices.

---

## CollectibleNameResolver

`ArcanumLib.Helpers.CollectibleNameResolver` — resolves item/block/entity codes to localized display names with wildcard support.

```csharp
using ArcanumLib.Helpers;

CollectibleNameResolver.GetDisplayName(api, "game:ingot-iron");   // → "Iron Ingot"
CollectibleNameResolver.GetDisplayName(api, "game:flower-*");     // → "Flower"
CollectibleNameResolver.ResolveIconCode(api, "game:flower-*");    // → "game:flower-bluepoppy"
```

Results are cached per language. Call `Clear()` on world unload.

---

## EntityHealthExtensions

`ArcanumLib.Common.EntityHealthExtensions` — read and scale entity health through `WatchedAttributes`.

```csharp
using ArcanumLib.Common;

if (entity.TryGetHealthFraction(out float frac)) { /* 0.0..1.0 */ }
entity.ScaleHealth(1.5f); // +50% max/current health
```

`maxhealth` is used when present; otherwise `basemaxhealth` is the fallback.

---

## PlayerExtensions

`ArcanumLib.Common.PlayerExtensions` — filter online players to those with living, positioned entities.

```csharp
using ArcanumLib.Common;

if (player.HasValidPosition()) { /* spawned entity with valid position */ }

foreach (var (player, entity) in sapi.World.AllOnlinePlayers.GetAliveEntities())
{
    // player and entity are guaranteed non-null and alive
}
```

Lazily evaluated. Players are skipped when their entity is `null`, not alive, or has no position.

---

## ShapeCloner

`ArcanumLib.Geometry.ShapeCloner` — deep-clones `Shape` objects so they can be safely modified without sharing mutable state with the cached original.

```csharp
using ArcanumLib.Geometry;

Shape clone = ShapeCloner.DeepClone(source);
clone.Textures["main"] = new AssetLocation("mydomain", "texture.png");
```

`Shape.Clone()` only shallow-copies `Textures`, `TextureSizes`, `FacesResolved`, and `AttachmentPoints`. Mutating those on a vanilla clone leaks back into the cached shape. `ShapeCloner` deep-copies all of them.

Also: `LoadAndClone(api, path)` loads and clones in one call.
