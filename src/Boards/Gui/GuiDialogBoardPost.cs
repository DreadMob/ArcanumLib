using System;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ArcanumLib.Boards.Gui
{
    /// <summary>
    /// The ArcanumLib post window: goal, target, count, run-out, title and description, and the reward slots of the
    /// server's escrow inventory. Closing it without posting gives the items back (the server returns them).
    /// </summary>
    public class GuiDialogBoardPost : GuiDialog
    {
        private const double W = 460, RowH = 30, LabelW = 130, Gap = 8;

        public static GuiDialogBoardPost? Current { get; private set; }

        private readonly BoardOpenPostPacket packet;
        private readonly InventoryGeneric inventory;
        private int goalIndex;
        private bool posted;

        public override string? ToggleKeyCombinationCode => null;

        public GuiDialogBoardPost(ICoreClientAPI capi, BoardOpenPostPacket packet) : base(capi)
        {
            this.packet = packet;
            inventory = new InventoryGeneric(Math.Max(1, packet.SlotCount), packet.InventoryId, capi, (_, owner) => new BoardSlot(owner));
        }

        public static void Show(ICoreClientAPI capi, BoardOpenPostPacket packet)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                Current?.TryClose();
                Current = new GuiDialogBoardPost(capi, packet);
                Current.Compose();
                Current.TryOpen();
            }, "arcanumlib-board-post");
        }

        private BoardGoalInfo? Goal => goalIndex >= 0 && goalIndex < packet.Goals.Count ? packet.Goals[goalIndex] : null;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            capi.World.Player.InventoryManager.OpenInventory(inventory);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.World.Player.InventoryManager.CloseInventory(inventory);
            if (!posted) capi.ModLoader.GetModSystem<BoardRegistry>()?.SendAction(packet.BoardKey, BoardActions.ClosePost);
            if (Current == this) Current = null;
        }

        /// <summary>The server accepted the post.</summary>
        public void OnPosted()
        {
            posted = true;
            TryClose();
        }

        private void Compose()
        {
            var bg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bg.BothSizing = ElementSizing.FitToChildren;
            var dialog = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            var font = CairoFont.WhiteSmallText();
            double fieldW = W - LabelW;

            var composer = capi.Gui.CreateCompo("arcanumlib-board-post", dialog)
                .AddShadedDialogBG(bg)
                .AddDialogTitleBar(Lang.Get("arcanumlib:board-post-title"), () => TryClose());

            double y = 40;
            ElementBounds Label(double at) => ElementBounds.Fixed(0, at + 6, LabelW, RowH);
            ElementBounds Field(double at) => ElementBounds.Fixed(LabelW, at, fieldW, RowH);

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-goal"), font, Label(y));
            var kinds = packet.Goals.Select(g => g.Kind).ToArray();
            var names = packet.Goals.Select(g => Lang.Get(g.NameKey)).ToArray();
            composer.AddDropDown(kinds, names, Math.Max(0, goalIndex), (code, _) =>
            {
                goalIndex = Array.IndexOf(kinds, code);
                Compose();
            }, Field(y), "goal");
            y += RowH + Gap;

            var goal = Goal;
            composer.AddStaticText(Lang.Get("arcanumlib:board-post-target"), font, Label(y));
            if (goal != null && goal.Targets.Count > 0)
            {
                var targets = goal.Targets.ToArray();
                composer.AddDropDown(targets, targets.Select(TargetName).ToArray(), 0, (_, _) => { }, Field(y), "target");
            }
            else
            {
                composer.AddTextInput(Field(y), null, font, "target");
            }
            y += RowH + Gap;

            if (goal?.HasDemand != false)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-post-demand"), font, Label(y));
                composer.AddNumberInput(Field(y), null, font, "demand");
                y += RowH + Gap;
            }

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-expire", packet.ExpireDaysMin, packet.ExpireDaysMax), font, Label(y));
            composer.AddNumberInput(Field(y), null, font, "expire");
            y += RowH + Gap;

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-titlefield"), font, Label(y));
            composer.AddTextInput(Field(y), null, font, "title");
            y += RowH + Gap;

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-description"), font, Label(y));
            composer.AddTextArea(ElementBounds.Fixed(LabelW, y, fieldW, RowH * 3), null, font, "description");
            y += RowH * 3 + Gap;

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-reward"), font, Label(y));
            composer.AddItemSlotGrid(inventory, SendInvPacket, Math.Min(6, inventory.Count), ElementBounds.Fixed(LabelW, y, fieldW, 50), "slots");
            y += 50 + 20 * ((inventory.Count - 1) / 6) + Gap;

            if (packet.CooldownRemainingHours > 0)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-post-cooldown", packet.CooldownRemainingHours.ToString("0.#")), font, ElementBounds.Fixed(0, y, W, RowH));
                y += RowH;
            }

            composer.AddSmallButton(Lang.Get("arcanumlib:board-post-cancel"), () => TryClose(), ElementBounds.Fixed(0, y, (W - Gap) / 2, RowH));
            composer.AddSmallButton(Lang.Get("arcanumlib:board-post-submit"), OnSubmit, ElementBounds.Fixed((W + Gap) / 2, y, (W - Gap) / 2, RowH), EnumButtonStyle.Normal, "submit");

            SingleComposer = composer.Compose();
            if (goal?.HasDemand != false) SingleComposer.GetNumberInput("demand").SetValue(1);
            SingleComposer.GetNumberInput("expire").SetValue((float)packet.ExpireDaysDefault);
            SingleComposer.GetTextInput("title")?.SetMaxLength(Math.Max(1, packet.MaxTitleLength));
            SingleComposer.GetButton("submit").Enabled = packet.Goals.Count > 0;
        }

        private void SendInvPacket(object packetObj) => capi.Network.SendPacketClient(packetObj);

        private string TargetName(string code)
        {
            if (code.Contains('*')) return code;
            var loc = new AssetLocation(code);
            CollectibleObject? obj = capi.World.GetItem(loc) ?? (CollectibleObject?)capi.World.GetBlock(loc);
            return obj != null ? new ItemStack(obj).GetName() : code;
        }

        private bool OnSubmit()
        {
            var goal = Goal;
            if (goal == null) return true;

            string? target = goal.Targets.Count > 0
                ? SingleComposer.GetDropDown("target")?.SelectedValue
                : SingleComposer.GetTextInput("target")?.GetText();

            capi.ModLoader.GetModSystem<BoardRegistry>()?.SendPost(new BoardPostMessage
            {
                BoardKey = packet.BoardKey,
                Kind = goal.Kind,
                TargetCode = target,
                Demand = goal.HasDemand ? (int)Math.Round(SingleComposer.GetNumberInput("demand").GetValue()) : 1,
                ExpireDays = SingleComposer.GetNumberInput("expire").GetValue(),
                Title = SingleComposer.GetTextInput("title")?.GetText(),
                Description = SingleComposer.GetTextArea("description")?.GetText()
            });
            return true;
        }
    }
}
