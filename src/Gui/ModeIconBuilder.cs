using System;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui;

/// <summary>
/// Convenience factory for <see cref="SkillItem"/> tool-mode and skill bar entries.
/// Reduces boilerplate when creating mode icons that show an image, a letter,
/// or a live rendered item stack.
/// </summary>
public static class ModeIconBuilder
{
    private const int IconSize = 48;
    private const int IconPadding = 5;
    private const int InnerIconSize = IconSize - IconPadding * 2;

    /// <summary>
    /// Creates a mode entry with the given in-game icon code (e.g. "clear", "plus").
    /// </summary>
    public static SkillItem WithIcon(
        ICoreClientAPI capi,
        AssetLocation code,
        string name,
        string iconCode,
        string? description = null,
        bool linebreak = false,
        bool enabled = true)
    {
        if (capi == null) throw new ArgumentNullException(nameof(capi));

        var item = CreateBase(capi, code, name, description, linebreak, enabled);
        item.WithIcon(capi, iconCode);
        return item;
    }

    /// <summary>
    /// Creates a mode entry with a custom Cairo-drawn icon.
    /// </summary>
    public static SkillItem WithCustomIcon(
        ICoreClientAPI capi,
        AssetLocation code,
        string name,
        DrawSkillIconDelegate onDrawIcon,
        string? description = null,
        bool linebreak = false,
        bool enabled = true)
    {
        if (capi == null) throw new ArgumentNullException(nameof(capi));

        var item = CreateBase(capi, code, name, description, linebreak, enabled);
        item.WithIcon(capi, onDrawIcon);
        return item;
    }

    /// <summary>
    /// Creates a mode entry with a single letter rendered in the center of the icon.
    /// </summary>
    public static SkillItem WithLetter(
        ICoreClientAPI capi,
        AssetLocation code,
        string name,
        string letter,
        string? description = null,
        bool linebreak = false,
        bool enabled = true)
    {
        if (capi == null) throw new ArgumentNullException(nameof(capi));

        var item = CreateBase(capi, code, name, description, linebreak, enabled);
        item.WithLetterIcon(capi, letter);
        return item;
    }

    /// <summary>
    /// Creates a mode entry that renders the given item stack as the icon.
    /// </summary>
    public static SkillItem WithItemStack(
        ICoreClientAPI capi,
        AssetLocation code,
        string name,
        ItemStack? stack,
        string? fallbackIcon = null,
        string? fallbackLetter = null,
        string? description = null,
        bool linebreak = false,
        bool enabled = true)
    {
        if (capi == null) throw new ArgumentNullException(nameof(capi));

        if (stack == null)
        {
            if (!string.IsNullOrWhiteSpace(fallbackIcon))
            {
                return WithIcon(capi, code, name, fallbackIcon, description, linebreak, enabled);
            }

            return WithLetter(
                capi,
                code,
                name,
                string.IsNullOrWhiteSpace(fallbackLetter) ? "?" : fallbackLetter.Substring(0, 1).ToUpperInvariant(),
                description,
                linebreak,
                enabled);
        }

        var item = CreateBase(capi, code, name, description, linebreak, enabled);
        item.Data = new DummySlot(stack.Clone());
        item.RenderHandler = CreateItemStackRenderCallback(capi, (ItemSlot)item.Data, ColorUtil.WhiteArgb);
        return item;
    }

    /// <summary>
    /// Creates a mode entry that renders the given item slot as the icon.
    /// </summary>
    public static SkillItem WithItemSlot(
        ICoreClientAPI capi,
        AssetLocation code,
        string name,
        ItemSlot? slot,
        string? fallbackIcon = null,
        string? fallbackLetter = null,
        string? description = null,
        bool linebreak = false,
        bool enabled = true)
    {
        if (capi == null) throw new ArgumentNullException(nameof(capi));

        if (slot?.Itemstack == null)
        {
            if (!string.IsNullOrWhiteSpace(fallbackIcon))
            {
                return WithIcon(capi, code, name, fallbackIcon, description, linebreak, enabled);
            }

            return WithLetter(
                capi,
                code,
                name,
                string.IsNullOrWhiteSpace(fallbackLetter) ? "?" : fallbackLetter.Substring(0, 1).ToUpperInvariant(),
                description,
                linebreak,
                enabled);
        }

        var item = CreateBase(capi, code, name, description, linebreak, enabled);
        item.Data = slot;
        item.RenderHandler = CreateItemStackRenderCallback(capi, slot, ColorUtil.WhiteArgb);
        return item;
    }

    /// <summary>
    /// Creates a mode entry that renders the given item stacks as a carousel/cycle.
    /// The displayed stack changes every <paramref name="cycleMs"/> milliseconds.
    /// </summary>
    public static SkillItem WithItemStackCycle(
        ICoreClientAPI capi,
        AssetLocation code,
        string name,
        ItemStack[]? stacks,
        int cycleMs = 1000,
        string? fallbackIcon = null,
        string? fallbackLetter = null,
        string? description = null,
        bool linebreak = false,
        bool enabled = true)
    {
        if (capi == null) throw new ArgumentNullException(nameof(capi));

        if (stacks == null || stacks.Length == 0 || stacks.All(s => s == null))
        {
            if (!string.IsNullOrWhiteSpace(fallbackIcon))
            {
                return WithIcon(capi, code, name, fallbackIcon, description, linebreak, enabled);
            }

            return WithLetter(
                capi,
                code,
                name,
                string.IsNullOrWhiteSpace(fallbackLetter) ? "?" : fallbackLetter.Substring(0, 1).ToUpperInvariant(),
                description,
                linebreak,
                enabled);
        }

        var validStacks = stacks.Where(s => s != null).Select(s => s!.Clone()).ToArray();
        var slots = validStacks.Select(s => new DummySlot(s)).ToArray();

        var item = CreateBase(capi, code, name, description, linebreak, enabled);
        item.Data = slots;
        item.RenderHandler = (modeCode, dt, posX, posY) =>
        {
            long now = capi.World.ElapsedMilliseconds;
            int index = (int)((now / Math.Max(1, cycleMs)) % slots.Length);
            RenderSlot(capi, slots[index], posX, posY, ColorUtil.WhiteArgb);
        };
        return item;
    }

    private static SkillItem CreateBase(
        ICoreClientAPI capi,
        AssetLocation code,
        string name,
        string? description = null,
        bool linebreak = false,
        bool enabled = true)
    {
        return new SkillItem
        {
            Code = code ?? throw new ArgumentNullException(nameof(code)),
            Name = name,
            Description = description ?? name,
            Linebreak = linebreak,
            Enabled = enabled
        };
    }

    private static RenderSkillItemDelegate CreateItemStackRenderCallback(ICoreClientAPI capi, ItemSlot slot, int color)
    {
        if (slot?.Itemstack == null)
        {
            return (_, _, _, _) => { };
        }

        return (code, dt, posX, posY) => RenderSlot(capi, slot, posX, posY, color);
    }

    private static void RenderSlot(ICoreClientAPI capi, ItemSlot slot, double posX, double posY, int color)
    {
        double total = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
        double scaled = GuiElement.scaled(total - 5);
        double itemSize = GuiElement.scaled(GuiElementPassiveItemSlot.unscaledItemSize);

        capi.Render.RenderItemstackToGui(
            slot,
            posX + scaled / 2,
            posY + scaled / 2,
            100,
            (float)itemSize,
            color,
            showStackSize: true);
    }
}
