---
layout: default
title: ModeIconBuilder
---

# ModeIconBuilder

## What is it for?

`ModeIconBuilder` provides factory methods for creating tool-mode and skill-bar icon entries without repeating boilerplate. It supports existing in-game icon codes, letter icons, and live `ItemStack` rendering. The returned objects are Vintage Story `SkillItem` instances.

## When to use it

- Building a tool-mode or skill selection bar.
- Reusing an existing in-game icon by its icon code.
- Showing a single letter or symbol as a mode icon.
- Displaying the rendered image of an `ItemStack`.
- Cycling through several stacks as a single animated icon.

## Quick example

```csharp
using ArcanumLib.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

var modes = new SkillItem[]
{
    ModeIconBuilder.WithIcon(capi, new AssetLocation("mywand:select"), "Select", "select"),
    ModeIconBuilder.WithLetter(capi, new AssetLocation("mywand:clear"), "Clear", "X"),
    ModeIconBuilder.WithItemStack(capi, new AssetLocation("mywand:reward"), "Reward", rewardStack)
};
```

## API overview

```csharp
public static class ModeIconBuilder
{
    public static SkillItem WithIcon(ICoreClientAPI capi, AssetLocation code, string name, string iconCode, ...);
    public static SkillItem WithCustomIcon(ICoreClientAPI capi, AssetLocation code, string name, DrawSkillIconDelegate onDrawIcon, ...);
    public static SkillItem WithLetter(ICoreClientAPI capi, AssetLocation code, string name, string letter, ...);
    public static SkillItem WithItemStack(ICoreClientAPI capi, AssetLocation code, string name, ItemStack? stack, ...);
    public static SkillItem WithItemSlot(ICoreClientAPI capi, AssetLocation code, string name, ItemSlot? slot, ...);
    public static SkillItem WithItemStackCycle(ICoreClientAPI capi, AssetLocation code, string name, ItemStack[]? stacks, int cycleMs = 1000, ...);
}
```

| Method | Use it to... |
|--------|--------------|
| `WithIcon` | Create a mode using an existing icon code. |
| `WithCustomIcon` | Create a mode that draws its icon through a custom delegate. |
| `WithLetter` | Create a mode that displays a single letter or symbol. |
| `WithItemStack` | Create a mode that renders a specific `ItemStack`. |
| `WithItemSlot` | Create a mode that renders the stack currently in an `ItemSlot`. |
| `WithItemStackCycle` | Create a mode that cycles through several stacks. |

All methods accept `description`, `linebreak`, and `enabled` parameters. The `ItemStack` and `ItemSlot` variants also accept a `fallbackIcon` / `fallbackLetter` that is used when the stack is `null`.

## Notes

- The `...` in the signatures above stands for shared optional parameters such as `description`, `linebreak`, and `enabled`. The `ItemStack` and `ItemSlot` variants additionally accept `fallbackIcon` / `fallbackLetter`. Refer to the source or IntelliSense for the full signature.
- `WithItemStackCycle` defaults to a 1000ms cycle; change `cycleMs` to speed up or slow down the animation.
