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

`ArcanumLib.Common.ApiExtensions` Ú¿‘ `IsClient()` / `IsServer()` checks for `ICoreAPI`, `ICoreClientAPI`, `ICoreServerAPI`, and `IWorldAccessor`. Sugar for `api.Side == EnumAppSide.Client`.

```csharp
using ArcanumLib.Common;

if (api.IsServer()) { /* server-only logic */ }
if (world.IsClient()) { /* client-only logic */ }
```

All overloads are null-safe and return `false` when the target is `null`.

---

## RGBA

`ArcanumLib.Gui.Theme.RGBA` Ú¿‘ immutable color struct normalized to `0..1`, designed for `Cairo.Context`.

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

`ArcanumLib.Text.Pretty` Ú¿‘ converts raw asset codes into human-readable strings.

```csharp
using ArcanumLib.Text;

Pretty.Readable("metalbit-uranium");    // Ú∆“ "Metalbit Uranium"
Pretty.TargetCode("game:flower-*");     // Ú∆“ "Flower"
Pretty.LastSegment("game:creature:bear"); // Ú∆“ "Bear"
Pretty.Sanitize("Hello<br>World\n");    // Ú∆“ "Hello World"
```

All methods accept `null` and return an empty string.

---

## Wildcard

`ArcanumLib.Text.Wildcard` Ú¿‘ case-insensitive `*` / `?` matching for asset codes.

```csharp
using ArcanumLib.Text;

Wildcard.Match("game:flower-bluepoppy", "game:flower-*"); // Ú∆“ true
Wildcard.Match("game:ingot-iron", "game:ingot-???n");     // Ú∆“ true
Wildcard.IsSimplePrefix("game:flower-*");                  // Ú∆“ true
```

Iterative, no allocations. `IsSimplePrefix` is useful for fast-path registry scans where `StartsWith` suffices.

---

## CollectibleNameResolver

`ArcanumLib.Helpers.CollectibleNameResolver` Ú¿‘ resolves item/block/entity codes to localized display names with wildcard support.

```csharp
using ArcanumLib.Helpers;

CollectibleNameResolver.GetDisplayName(api, "game:ingot-iron");   // Ú∆“ "Iron Ingot"
CollectibleNameResolver.GetDisplayName(api, "game:flower-*");     // Ú∆“ "Flower"
CollectibleNameResolver.ResolveIconCode(api, "game:flower-*");    // Ú∆“ "game:flower-bluepoppy"
```

Results are cached per language. Call `Clear()` on world unload.

---

## EntityHealthExtensions

`ArcanumLib.Common.EntityHealthExtensions` Ú¿‘ read and scale entity health through `WatchedAttributes`.

```csharp
using ArcanumLib.Common;

if (entity.TryGetHealthFraction(out float frac)) { /* 0.0..1.0 */ }
entity.ScaleHealth(1.5f); // +50% max/current health
```

`maxhealth` is used when present; otherwise `basemaxhealth` is the fallback.

---

## PlayerExtensions

`ArcanumLib.Common.PlayerExtensions` Ú¿‘ filter online players to those with living, positioned entities.

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

`ArcanumLib.Geometry.ShapeCloner` Ú¿‘ deep-clones `Shape` objects so they can be safely modified without sharing mutable state with the cached original.

```csharp
using ArcanumLib.Geometry;

Shape clone = ShapeCloner.DeepClone(source);
clone.Textures["main"] = new AssetLocation("mydomain", "texture.png");
```

`Shape.Clone()` only shallow-copies `Textures`, `TextureSizes`, `FacesResolved`, and `AttachmentPoints`. Mutating those on a vanilla clone leaks back into the cached shape. `ShapeCloner` deep-copies all of them.

Also: `LoadAndClone(api, path)` loads and clones in one call.

## ChatFormatUtil

### What is it for?

`ChatFormatUtil` provides helpers for formatting chat and HUD text with Vintage Story `<font color="...">` tags.

### When to use it

- You need to colorize chat messages or HUD text.
- You want alert-prefixed messages with consistent styling.

### Quick example

```csharp
using ArcanumLib.Common;

// Colorize text
string msg = ChatFormatUtil.Font("Hello!", "#4ADE80");

// Alert prefix: red [!] + white text
string alert = ChatFormatUtil.PrefixAlert("Enemy defeated!");

// Custom colors
string custom = ChatFormatUtil.PrefixAlert("Warning", "#ff5555", "#fbbf24");

// Custom prefix and colors
string full = ChatFormatUtil.PrefixAlert("Warning", "[?] ", "#fbbf24", "#ffffff");
```

### API overview

| Method | Description |
|--------|-------------|
| `Font(text, hexColor)` | Wraps text in a `<font color="...">` tag. |
| `PrefixAlert(text)` | Default alert: red `[!] ` prefix + white text. |
| `PrefixAlert(text, prefixColor, textColor)` | Custom colors, default `[!] ` prefix. |
| `PrefixAlert(text, prefix, prefixColor, textColor)` | Fully custom prefix and colors. |

## DamageHelper

### What is it for?

`DamageHelper` is a factory for `DamageSource` instances with the most common field combinations used across combat abilities, effects, and projectiles. Instead of writing 6-8 lines of object initializer each time, you call a single factory method.

### When to use it

- You need to apply damage from an entity, player, weather, or internal source.
- You want concise one-liners for `entity.ReceiveDamage(...)`.
- You need consistent defaults for `IgnoreInvFrames` across your combat code.

### Quick example

```csharp
using ArcanumLib.Common;

// Entity-sourced damage
target.ReceiveDamage(DamageHelper.Create(enemy, EnumDamageType.BluntAttack, 2), 50f);

// Player-sourced damage with tier
entity.ReceiveDamage(DamageHelper.CreatePlayer(player.Entity, EnumDamageType.SlashAttack, 3), 40f);

// Projectile damage (source entity + cause entity)
target.ReceiveDamage(DamageHelper.Create(enemy, projectile, EnumDamageType.PiercingAttack, 3), 80f);

// Weather damage (lightning)
entity.ReceiveDamage(DamageHelper.CreateWeather(pos, EnumDamageType.Lightning, 0.5f), 100f);

// Healing
entity.ReceiveDamage(DamageHelper.CreateHeal(), -30f);
```

### API overview

| Method | Description |
|--------|-------------|
| `Create(source, type, [ignoreInvFrames])` | Entity-sourced damage. |
| `Create(source, cause, type, [ignoreInvFrames])` | Entity-sourced with a cause entity (projectiles). |
| `Create(source, type, damageTier, [ignoreInvFrames])` | Entity-sourced with explicit tier. |
| `Create(source, type, knockbackStrength, [ignoreInvFrames])` | Entity-sourced with knockback. |
| `Create(source, cause, type, damageTier, [ignoreInvFrames])` | Entity-sourced with cause and tier. |
| `Create(source, cause, type, knockbackStrength, [ignoreInvFrames])` | Entity-sourced with cause and knockback. |
| `Create(source, cause, type, damageTier, knockbackStrength, [ignoreInvFrames])` | Entity-sourced with all fields. |
| `Create(source, cause, type, damageTier, sourcePos, hitPos, [ignoreInvFrames])` | Projectile-style with positions. |
| `CreatePlayer(source, type, [ignoreInvFrames])` | Player-sourced damage. |
| `CreatePlayer(source, type, damageTier, [ignoreInvFrames])` | Player-sourced with tier. |
| `CreatePlayer(source, type, knockbackStrength, [ignoreInvFrames])` | Player-sourced with knockback. |
| `CreatePlayer(source, type, damageTier, knockbackStrength, [ignoreInvFrames])` | Player-sourced with tier and knockback. |
| `CreatePlayer(source, cause, type, damageTier, knockbackStrength, [ignoreInvFrames])` | Player-sourced projectile/area damage. |
| `CreateWeather(sourcePos, type, knockbackStrength, [ignoreInvFrames])` | Weather-sourced damage. |
| `CreateInternal(type, [ignoreInvFrames])` | Internal damage (self-damage, costs). |
| `CreateInternal(type, damageTier, knockbackStrength, [ignoreInvFrames])` | Internal with tier and knockback. |
| `CreateHeal([ignoreInvFrames])` | Healing damage source. |