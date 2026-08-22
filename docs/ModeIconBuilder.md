# ModeIconBuilder

Factory methods for creating `SkillItem` tool-mode and skill-bar icons without
repeating boilerplate. Supports in-game icon codes, letter icons, and live
`ItemStack` rendering.

## Usage

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

## API

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

All methods accept `description`, `linebreak`, and `enabled` parameters and a
`fallbackIcon` / `fallbackLetter` for the `ItemStack` variants when the stack is
`null`.
