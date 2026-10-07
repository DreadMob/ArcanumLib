#nullable enable
using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using ArcanumLib.Gui.Dialogs;
using ArcanumLib.Gui.Theme;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Static recessed-brass well — the backing plate under the blueprint,
/// fuel and output <see cref="GuiElementFittedSlot"/>s.
/// </summary>
public class GuiElementSlotWell : GuiElement
{
    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">Well bounds.</param>
    public GuiElementSlotWell(ICoreClientAPI capi, ElementBounds bounds)
        : base(capi, bounds) { }

    /// <inheritdoc />
    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        BrassTheme.DrawRecessedWell(ctx, Bounds.bgDrawX, Bounds.bgDrawY,
            Bounds.OuterWidth, Bounds.OuterHeight, ArcanumGuiTheme.Palette);
    }
}

/// <summary>
/// One interactive slot that renders its stack fitted INTO the cell —
/// normalizes by the item's own guiTransform scale, so the huge vehicle-part
/// models (scale ≈2.8) and the tiny vanilla ones render the same size.
/// Can carry a "ghost" stack: while the slot is empty the required part is
/// drawn as a dimmed blueprint-line icon.
/// </summary>
public class GuiElementFittedSlot : GuiElement
{
    /// <summary>Blueprint-line tint for the ghost icon.</summary>
    private static readonly int GhostTint =
        ColorUtil.ColorFromRgba(150, 190, 230, 130);

    private readonly InventoryBase _inv;
    private readonly int _slotId;
    private readonly Action<object> _sendPacket;
    private readonly ItemStack? _ghost;
    private ItemSlot? _ghostSlot;
    private readonly double _iconFrac;

    /// <param name="inv">Session inventory the slot lives in.</param>
    /// <param name="slotId">Slot index inside <paramref name="inv"/>.</param>
    /// <param name="sendPacket">Forwards inventory packets (BE packet send).</param>
    /// <param name="ghost">Stack drawn dimmed while the slot is empty (the
    /// recipe's required component), or null for a plain slot.</param>
    /// <param name="iconFrac">Icon size as a fraction of the cell (0..1).</param>
    public GuiElementFittedSlot(ICoreClientAPI capi, ElementBounds bounds,
        InventoryBase inv, int slotId, Action<object> sendPacket,
        ItemStack? ghost = null, double iconFrac = 0.62) : base(capi, bounds)
    {
        _inv = inv;
        _slotId = slotId;
        _sendPacket = sendPacket;
        _ghost = ghost;
        _iconFrac = iconFrac;
    }

    /// <summary>Pixels the icon should occupy, normalized for the item's own
    /// guiTransform scale — RenderItemstackToGui multiplies size by it, so we
    /// divide it back out (worst axis wins).</summary>
    private static float FitSize(ItemStack stack, double targetPx)
    {
        var sc = stack.Collectible?.GuiTransform?.ScaleXYZ;
        float m = Math.Max(sc?.X ?? 1f, sc?.Y ?? 1f);
        return (float)(targetPx / Math.Max(0.01f, m));
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        var slot = _inv[_slotId];
        Bounds.CalcWorldBounds();
        double cx = Bounds.absX + Bounds.OuterWidth / 2.0;
        double cy = Bounds.absY + Bounds.OuterHeight / 2.0;
        double target = Bounds.OuterWidth * _iconFrac;

        // Hover highlight, like vanilla slot grids.
        if (_hover)
            api.Render.RenderRectangle((float)Bounds.absX, (float)Bounds.absY, 50,
                (float)Bounds.OuterWidth, (float)Bounds.OuterHeight, HoverTint);

        if (slot.Itemstack != null)
        {
            api.Render.RenderItemstackToGui(slot, cx, cy, 90,
                FitSize(slot.Itemstack, target), -1, deltaTime);
        }
        else if (_ghost?.Collectible != null)
        {
            _ghostSlot ??= new DummySlot(_ghost);
            api.Render.RenderItemstackToGui(_ghostSlot, cx, cy, 90,
                FitSize(_ghost, target), GhostTint, false, false, false);
        }
    }

    private static readonly int HoverTint = ColorUtil.ColorFromRgba(255, 255, 255, 50);
    private bool _hover;

    /// <summary>Tracks hover for the highlight and fires the vanilla slot enter/leave
    /// events so the item tooltip shows.</summary>
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        bool inside = Bounds.PointInside(args.X, args.Y);
        if (inside == _hover) return;
        _hover = inside;
        var slot = _inv[_slotId];
        if (inside) api.Input.TriggerOnMouseEnterSlot(slot);
        else api.Input.TriggerOnMouseLeaveSlot(slot);
    }

    /// <summary>Same flow as the vanilla slot-grid SlotClick, minus the
    /// drag-distribute bookkeeping a single well never needs.</summary>
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!Bounds.PointInside(args.X, args.Y)) return;
        args.Handled = true;

        var invMgr = api.World.Player.InventoryManager;
        var mouseInv = invMgr.GetOwnInventory("mouse");
        bool shift = api.Input.KeyboardKeyState[1] || api.Input.KeyboardKeyState[2];
        var op = new ItemStackMoveOperation(api.World, args.Button,
            (EnumModifierKey)((shift ? 2 : 0)
                | (api.Input.KeyboardKeyState[3] ? 1 : 0)
                | (api.Input.KeyboardKeyState[5] ? 4 : 0)),
            EnumMergePriority.AutoMerge)
        { ActingPlayer = api.World.Player };

        object? packet;
        var slot = _inv[_slotId];
        if (shift)
        {
            op.RequestedQuantity = slot.StackSize;
            packet = _inv.ActivateSlot(_slotId, slot, ref op);
        }
        else
        {
            op.CurrentPriority = EnumMergePriority.DirectMerge;
            bool empty = mouseInv.Empty;
            var heldType = mouseInv[0]?.Itemstack?.Collectible;
            packet = _inv.ActivateSlot(_slotId, mouseInv[0], ref op);
            if (empty && !mouseInv.Empty)
            {
                api.World.PlaySound(mouseInv[0]?.Itemstack?.Collectible?.HeldSounds?.InvPickup
                    ?? HeldSounds.InvPickUpDefault);
            }
            else if ((!empty && mouseInv.Empty)
                || heldType?.Id != mouseInv[0]?.Itemstack?.Collectible?.Id)
            {
                api.World.PlaySound(heldType?.HeldSounds?.InvPlace
                    ?? HeldSounds.InvPlaceDefault);
            }
        }
        if (packet is object[] arr)
            foreach (var p in arr) _sendPacket(p);
        else if (packet != null) _sendPacket(packet);
        api.Input.TriggerOnMouseClickSlot(slot);
    }
}
