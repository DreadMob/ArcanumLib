---
layout: default
title: Custom Icons
nav_order: 15
parent: Arcanum GUI Toolkit
---

# Custom Icons

## What is it for?

ArcanumLib provides two systems for custom Cairo-drawn GUI icons:

1. **`ICustomIconRenderer`** — an interface for drawing a single icon at a given position and radius.
2. **`CustomIconRegistry`** — a global string-keyed registry of `ICustomIconRenderer` instances.
3. **`CustomTabIconRenderer`** — a static library of decorative Cairo icons (dividers, bullets, stars, shields, crowns, skulls, etc.) used by `GuiElementCustomTabContent`.

## When to use it

- You need vector-drawn icons that scale cleanly at any size.
- You want to register custom icons by string key and look them up at compose/render time.
- You are building tab content and want decorative section dividers and entry bullets.

## Quick example

### Registering a custom icon

```csharp
using ArcanumLib.Gui.Icons;
using Cairo;

public class MyStarIcon : ICustomIconRenderer
{
    public void Draw(Context ctx, double cx, double cy, double radius)
    {
        ctx.SetSourceRGBA(1.0, 0.8, 0.2, 1.0);
        // Draw a 5-pointed star at (cx, cy) with the given radius.
        for (int j = 0; j < 10; j++)
        {
            double angle = j * Math.PI / 5 - Math.PI / 2;
            double r = (j % 2 == 0) ? radius : radius * 0.42;
            double px = cx + Math.Cos(angle) * r;
            double py = cy + Math.Sin(angle) * r;
            if (j == 0) ctx.MoveTo(px, py);
            else ctx.LineTo(px, py);
        }
        ctx.ClosePath();
        ctx.Fill();
    }
}

// At startup:
CustomIconRegistry.Register("mydomain:star", new MyStarIcon());
```

### Using a registered icon

```csharp
if (CustomIconRegistry.TryGet("mydomain:star", out var renderer))
{
    renderer?.Draw(ctx, centerX, centerY, 24.0);
}
```

### Using CustomTabIconRenderer

```csharp
using ArcanumLib.Gui.Icons;

// Draw a decorative section divider
CustomTabIconRenderer.DrawSectionDivider(ctx, x, y, width, ArcanumGuiTheme.BorderSilver);

// Draw a section header icon
CustomTabIconRenderer.DrawSectionHeaderIcon(ctx, cx, cy, size, ArcanumGuiTheme.Accent);

// Draw an entry bullet
CustomTabIconRenderer.DrawEntryBullet(ctx, cx, cy, size, ArcanumGuiTheme.Accent);

// Draw an active star
CustomTabIconRenderer.DrawActiveStar(ctx, cx, cy, radius, ArcanumGuiTheme.AccentBright);
```

## API overview

### ICustomIconRenderer

```csharp
public interface ICustomIconRenderer
{
    void Draw(Context ctx, double cx, double cy, double radius);
}
```

### CustomIconRegistry

| Method | Description |
|--------|-------------|
| `Register(string key, ICustomIconRenderer renderer)` | Register a renderer. Overwrites existing. |
| `TryGet(string key, out ICustomIconRenderer? renderer)` | Look up a renderer by key. |
| `Has(string key)` | Check if a renderer is registered. |
| `Unregister(string key)` | Remove a renderer. |
| `Clear()` | Remove all renderers. |

### CustomTabIconRenderer

Static methods for decorative Cairo icons:

| Method | Description |
|--------|-------------|
| `DrawSectionDivider` | Horizontal line with a centered diamond ornament. |
| `DrawSectionHeaderIcon` | Small diamond symbol for section headers. |
| `DrawEntryBullet` | Chevron arrow for normal entries. |
| `DrawActiveStar` | Five-pointed star for active entries. |
| `DrawSubDot` | Small dot for sub-item indentation. |
| `DrawRiftIcon` | Jagged vertical crack. |
| `DrawShieldIcon` | Shield outline with a vertical line. |
| `DrawCrownIcon` | Simple crown shape. |
| `DrawSkullIcon` | Skull and eye sockets. |
| `DrawHourglassIcon` | Hourglass with falling sand. |
| `DrawSwordIcon` | Sword with crossguard and pommel. |

## Notes

- All icons are drawn using Cairo paths — no texture loading required.
- Colours are passed as `RGBA` values from `ArcanumLib.Gui.Theme`.
- `CustomIconRegistry` is a global static registry; register icons once at mod startup.
- `ItemListElement` checks `CustomIconRegistry` for `CustomIconKey` before falling back to item-stack icons.
