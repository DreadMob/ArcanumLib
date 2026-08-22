# RGBA

`ArcanumLib.Gui.Theme.RGBA` is a small immutable color struct with values normalized to `0..1`. It is designed to be used directly with `Cairo.Context`.

## Construction

```csharp
using ArcanumLib.Gui.Theme;

var color = new RGBA(0.2, 0.5, 0.8, 1.0);
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `R` | `double` | Red channel, `0..1`. |
| `G` | `double` | Green channel, `0..1`. |
| `B` | `double` | Blue channel, `0..1`. |
| `A` | `double` | Alpha channel, `0..1`. |

## Methods

### `static RGBA From(int r, int g, int b, double a)`
Creates an RGBA value from 8-bit RGB components and a `0..1` alpha value.

```csharp
var color = RGBA.From(255, 128, 64, 0.9);
```

### `static RGBA? ParseHexColor(string hex)`
Parses a `#RRGGBB` or `#RGB` hex string. Returns `null` if the string is invalid.

```csharp
var color = RGBA.ParseHexColor("#4ADE80");
if (color == null) { /* invalid */ }
```

### `static RGBA FromArgb(int argb)`
Converts a packed `0xAARRGGBB` integer to an RGBA value. The alpha channel is read from the highest byte.

```csharp
var color = RGBA.FromArgb(0xFFFF8040);
```

### `RGBA WithAlpha(double a)`
Returns a new RGBA with the same color and the given alpha.

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
