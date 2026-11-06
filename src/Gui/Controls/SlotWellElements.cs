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
/// One interactive slot that renders its stack fitted INTO the cell by the
/// size of its real model (<see cref="GuiItemFit"/>), so multi-block machines
/// and flat plates both fill the cell and nothing spills out of it.
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
    private readonly int _quota;
    private LoadedTexture? _countTex;
    private string _countText = "";

    /// <param name="inv">Session inventory the slot lives in.</param>
    /// <param name="slotId">Slot index inside <paramref name="inv"/>.</param>
    /// <param name="sendPacket">Forwards inventory packets (BE packet send).</param>
    /// <param name="ghost">Stack drawn dimmed while the slot is empty (the
    /// recipe's required component), or null for a plain slot.</param>
    /// <param name="iconFrac">Icon size as a fraction of the cell (0..1).</param>
    /// <param name="quota">How many the recipe wants here: the count reads "have/quota". 0 = plain count.</param>
    public GuiElementFittedSlot(ICoreClientAPI capi, ElementBounds bounds,
        InventoryBase inv, int slotId, Action<object> sendPacket,
        ItemStack? ghost = null, double iconFrac = 0.62, int quota = 0) : base(capi, bounds)
    {
        _quota = quota;
        _inv = inv;
        _slotId = slotId;
        _sendPacket = sendPacket;
        _ghost = ghost;
        _iconFrac = iconFrac;
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        var slot = _inv[_slotId];
        Bounds.CalcWorldBounds();
        // Hover highlight, like vanilla slot grids.
        if (_hover)
            api.Render.RenderRectangle((float)Bounds.absX, (float)Bounds.absY, 50,
                (float)Bounds.OuterWidth, (float)Bounds.OuterHeight, HoverTint);

        if (slot.Itemstack != null)
        {
            GuiItemFit.Render(api, slot, Bounds, _iconFrac, -1, deltaTime, showStackSize: false);
        }
        else if (_ghost?.Collectible != null)
        {
            _ghostSlot ??= new DummySlot(_ghost);
            GuiItemFit.Render(api, _ghostSlot, Bounds, _iconFrac, GhostTint, deltaTime, shading: false, showStackSize: false);
        }
        RenderCount(slot.Itemstack?.StackSize ?? 0);
    }

    /// <summary>The count sits in the cell's own corner: the game's stack number follows the fitted icon and
    /// falls outside the clip of a small well.</summary>
    private void RenderCount(int have)
    {
        if (GuiOccluders.Covers(Bounds)) return;
        string text = _quota > 0 ? $"{have}/{_quota}" : have > 1 ? have.ToString() : "";
        if (text.Length == 0) return;
        if (text != _countText || _countTex == null)
        {
            _countTex?.Dispose();
            var font = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold);
            if (_quota > 0 && have < _quota) font = font.WithColor(new[] { 0.95, 0.72, 0.45, 1.0 });
            _countTex = api.Gui.TextTexture.GenUnscaledTextTexture(text, font);
            _countText = text;
        }
        api.Render.Render2DLoadedTexture(_countTex,
            (float)(Bounds.absX + Bounds.OuterWidth - _countTex.Width - scaled(2)),
            (float)(Bounds.absY + Bounds.OuterHeight - _countTex.Height), 400);
    }

    public override void Dispose()
    {
        base.Dispose();
        _countTex?.Dispose();
        _countTex = null;
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
