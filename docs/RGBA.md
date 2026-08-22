---
layout: default
title: RGBA
---

# RGBA

## What is it for?

`RGBA` is a small immutable color struct with values normalized to `0..1`. It is designed to be used directly with `Cairo.Context` and to simplify color conversion from 8-bit RGB, hex strings, or packed ARGB values.

## When to use it

- Setting colors for ArcanumLib GUI theme or custom drawing.
- Converting 8-bit RGB components or `#RRGGBB` / `#RGB` hex colors into a Cairo source color.
- Applying a packed `0xAARRGGBB` integer color.
- Fading or blending two colors with `WithAlpha` or `Lerp`.
- Applying a color as the current Cairo source.

## Quick example

```csharp
using ArcanumLib.Gui.Theme;
using Cairo;

var color = new RGBA(0.2, 0.5, 0.8, 1.0);
color.Apply(ctx);
ctx.Paint();
```

## API overview

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `R` | `double` | Red channel, `0..1`. |
| `G` | `double` | Green channel, `0..1`. |
| `B` | `double` | Blue channel, `0..1`. |
| `A` | `double` | Alpha channel, `0..1`. |

### `RGBA From(int r, int g, int b, double a)`

Creates an `RGBA` from 8-bit RGB components and a `0..1` alpha value.

```csharp
var color = RGBA.From(255, 128, 64, 0.9);
```

### `RGBA? ParseHexColor(string hex)`

Parses a `#RRGGBB` or `#RGB` hex string. Returns `null` if the string is invalid.

```csharp
var color = RGBA.ParseHexColor("#4ADE80");
if (color == null) { /* invalid */ }
```

### `RGBA FromArgb(int argb)`

Converts a packed `0xAARRGGBB` integer to an `RGBA`. The alpha channel is read from the highest byte.

```csharp
var color = RGBA.FromArgb(0xFFFF8040);
```

### `RGBA WithAlpha(double a)`

Returns a new `RGBA` with the same color and the given alpha.

```csharp
var faded = color.WithAlpha(0.5);
```

### `RGBA Lerp(RGBA other, double t)`

Linearly interpolates between this color and `other`. `t` is clamped to `[0, 1]`.

```csharp
var middle = color1.Lerp(color2, 0.5);
```

### `void Apply(Context ctx)`

Sets the Cairo context source to this color.

```csharp
using (Cairo.Context ctx = ...)
{
    color.Apply(ctx);
    ctx.Paint();
}
```

## Notes

- `RGBA` is immutable. Methods that appear to modify a color (`WithAlpha`, `Lerp`) return a new instance.
- `ParseHexColor` returns `null` when the input is not a valid `#RRGGBB` or `#RGB` string.
- `Lerp` clamps `t` to `[0, 1]`.
