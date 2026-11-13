using System;
using System.Globalization;
using System.Linq;
using ArcanumLib.Gui.Controls;
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
        private const double W = 480, RowH = 30, LabelW = 120, Gap = 8;

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
            var dim = CairoFont.WhiteDetailText().WithColor(new double[] { 0.75, 0.7, 0.62, 1 });
            double fieldW = W - LabelW;

            // Keep what was typed when the goal changes.
            string? keepTitle = SingleComposer?.GetTextInput("title")?.GetText();
            string? keepDesc = SingleComposer?.GetTextArea("description")?.GetText();
            string? keepTarget = goalChanged ? null : SingleComposer?.GetTextInput("target")?.GetText();
            float? keepDemand = goalChanged ? null : SingleComposer?.GetNumberInput("demand")?.GetValue();
            var keepRewards = new System.Collections.Generic.Dictionary<int, float>();
            for (int i = 0; i < packet.RewardFields.Count; i++)
                if (SingleComposer?.GetNumberInput("reward" + i) is { } input) keepRewards[i] = input.GetValue();
            goalChanged = false;

            var composer = capi.Gui.CreateCompo("arcanumlib-board-post", dialog)
                .AddShadedDialogBG(bg)
                .AddDialogTitleBar(Lang.Get("arcanumlib:board-post-title"), () => TryClose());

            double y = 40;
            ElementBounds Label(double at) => ElementBounds.Fixed(0, at + 6, LabelW, RowH);
            ElementBounds Field(double at) => ElementBounds.Fixed(LabelW, at, fieldW, RowH);

            // Goal: toggle buttons while they fit, a list otherwise.
            if (packet.Goals.Count is > 1 and <= 4)
            {
                double bw = (W - Gap * (packet.Goals.Count - 1)) / packet.Goals.Count;
                for (int i = 0; i < packet.Goals.Count; i++)
                {
                    int index = i;
                    composer.AddSmallButton(Lang.Get(packet.Goals[i].NameKey), () => { SelectGoal(index); return true; },
                        ElementBounds.Fixed(i * (bw + Gap), y, bw, RowH), i == goalIndex ? EnumButtonStyle.MainMenu : EnumButtonStyle.Normal, "goal" + i);
                }
                y += RowH + Gap * 2;
            }
            else if (packet.Goals.Count > 4)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-post-goal"), font, Label(y));
                var kinds = packet.Goals.Select(g => g.Kind).ToArray();
                composer.AddDropDown(kinds, packet.Goals.Select(g => Lang.Get(g.NameKey)).ToArray(), Math.Max(0, goalIndex),
                    (code, _) => SelectGoal(Array.IndexOf(kinds, code)), Field(y), "goal");
                y += RowH + Gap;
            }

            var goal = Goal;
            string targetLabel = goal != null && Lang.HasTranslation(goal.NameKey + "-target") ? Lang.Get(goal.NameKey + "-target") : Lang.Get("arcanumlib:board-post-target");
            composer.AddStaticText(targetLabel, font, Label(y));
            if (goal != null && goal.Targets.Count > 0)
            {
                var targets = goal.Targets.ToArray();
                var names = goal.TargetNames.Count == targets.Length ? goal.TargetNames.ToArray() : targets.Select(TargetName).ToArray();
                composer.AddDropDown(targets, names, 0, (_, _) => { }, Field(y), "target");
                y += RowH + Gap;
            }
            else if (goal?.TargetIsItem == true)
            {
                const double handW = 90;
                composer.AddTextInput(ElementBounds.Fixed(LabelW, y, fieldW - handW - Gap, RowH), OnTargetTyped, font, "target");
                composer.AddSmallButton(Lang.Get("arcanumlib:board-post-fromhand"), OnFromHand, ElementBounds.Fixed(W - handW, y, handW, RowH), EnumButtonStyle.Normal, "fromhand");
                y += RowH + 2;
                composer.AddDynamicText("", dim, ElementBounds.Fixed(LabelW, y, fieldW, 20), "targetinfo");
                y += 20 + Gap;
            }
            else
            {
                composer.AddTextInput(Field(y), null, font, "target");
                y += RowH + Gap;
            }

            if (goal?.HasDemand != false)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-post-demand"), font, Label(y));
                composer.AddNumberInput(ElementBounds.Fixed(LabelW, y, 110, RowH), null, font, "demand");
                y += RowH + Gap;
            }

            // Run-out: "[7] days [∞] 1–30"; ∞ = no run-out (0), the field is then off.
            composer.AddStaticText(Lang.Get("arcanumlib:board-post-expirelabel"), font, Label(y));
            composer.AddNumberInput(ElementBounds.Fixed(LabelW, y, 80, RowH), null, font, "expire");
            composer.AddStaticText(Lang.Get("arcanumlib:board-post-days"), font, ElementBounds.Fixed(LabelW + 88, y + 6, 50, RowH));
            // The game font has no ∞: the Arcanum button draws it as a shape.
            composer.AddArcanumButton("∞", () => { ToggleNoExpiry(); return true; }, ElementBounds.Fixed(LabelW + 140, y, 36, RowH),
                noExpiry ? ArcanumButtonStyle.Primary : ArcanumButtonStyle.Default, "noexpiry");
            composer.AddHoverText(Lang.Get("arcanumlib:board-post-noexpiry"), font, 220, ElementBounds.Fixed(LabelW + 140, y, 36, RowH));
            composer.AddStaticText($"{packet.ExpireDaysMin:0.#}–{packet.ExpireDaysMax:0.#}", dim, ElementBounds.Fixed(LabelW + 186, y + 8, 80, RowH));
            y += RowH + Gap;

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-titlefield"), font, Label(y));
            composer.AddTextInput(Field(y), null, font, "title");
            y += RowH + Gap;

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-description"), font, Label(y));
            composer.AddTextArea(ElementBounds.Fixed(LabelW, y, fieldW, RowH * 3), null, font, "description");
            y += RowH * 3 + Gap;

            composer.AddStaticText(Lang.Get("arcanumlib:board-post-reward"), font, Label(y));
            // Amount fields of reward sources (money from an account…), then the item slots.
            for (int i = 0; i < packet.RewardFields.Count; i++)
            {
                var field = packet.RewardFields[i];
                int fi = i;
                if (i > 0) composer.AddStaticText(Lang.Get(field.LabelKey), font, Label(y));
                composer.AddNumberInput(ElementBounds.Fixed(LabelW, y, 110, RowH), _ => UpdateRewardHint(fi), font, "reward" + i);
                if (field.Choices.Count > 1)
                    composer.AddDropDown(field.Choices.ToArray(), field.ChoiceNames.ToArray(), 0, (_, _) => UpdateRewardHint(fi),
                        ElementBounds.Fixed(LabelW + 118, y, fieldW - 118, RowH), "rewardchoice" + i);
                else
                    composer.AddStaticText(field.ChoiceNames.FirstOrDefault() ?? "", font, ElementBounds.Fixed(LabelW + 118, y + 6, fieldW - 118, RowH));
                y += RowH + 2;
                composer.AddDynamicText("", dim, ElementBounds.Fixed(LabelW, y, fieldW, 18), "rewardhint" + i);
                y += 18 + Gap;
            }
            if (packet.RewardFields.Count > 0)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-post-and-items"), dim, ElementBounds.Fixed(LabelW, y, fieldW, 18));
                y += 20;
            }
            int cols = Math.Min(6, inventory.Count);
            int slotRows = (inventory.Count + cols - 1) / cols;
            composer.AddItemSlotGrid(inventory, SendInvPacket, cols, ElementBounds.Fixed(LabelW, y, fieldW, 50 * slotRows), "slots");
            y += 52 * slotRows + 2;
            composer.AddStaticText(Lang.Get("arcanumlib:board-post-escrow"), dim, ElementBounds.Fixed(LabelW, y, fieldW, 36));
            y += 36 + Gap;

            if (goal != null && goal.ConfirmDays > 0)
            {
                var box = ElementBounds.Fixed(0, y, W, 72);
                composer.AddInset(box, 2, 0.75f);
                composer.AddStaticText(Lang.Get("arcanumlib:board-post-confirm-hint", DaysText(goal.ConfirmDays)), font, ElementBounds.Fixed(8, y + 6, W - 16, 64));
                y += 72 + Gap;
            }

            if (packet.CooldownRemainingHours > 0)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-post-cooldown", packet.CooldownRemainingHours.ToString("0.#")),
                    font.Clone().WithColor(new double[] { 0.9, 0.5, 0.4, 1 }), ElementBounds.Fixed(0, y, W, RowH));
                y += RowH;
            }

            composer.AddSmallButton(Lang.Get("arcanumlib:board-post-cancel"), () => TryClose(), ElementBounds.Fixed(0, y, (W - Gap) / 2, RowH));
            composer.AddSmallButton(Lang.Get("arcanumlib:board-post-submit"), OnSubmit, ElementBounds.Fixed((W + Gap) / 2, y, (W - Gap) / 2, RowH), EnumButtonStyle.Normal, "submit");

            SingleComposer = composer.Compose();
            if (goal?.HasDemand != false) SingleComposer.GetNumberInput("demand").SetValue(keepDemand ?? 1);
            if (keepTarget != null) SingleComposer.GetTextInput("target")?.SetValue(keepTarget);
            SingleComposer.GetNumberInput("expire").SetValue((float)(expireDays ?? packet.ExpireDaysDefault));
            UpdateExpire();
            var title = SingleComposer.GetTextInput("title");
            title?.SetMaxLength(Math.Max(1, packet.MaxTitleLength));
            if (keepTitle != null) title?.SetValue(keepTitle);
            if (goal != null) title?.SetPlaceHolderText(Lang.Get(goal.NameKey));
            if (keepDesc != null) SingleComposer.GetTextArea("description")?.SetValue(keepDesc);
            SingleComposer.GetButton("submit").Enabled = packet.Goals.Count > 0 && packet.CooldownRemainingHours <= 0;
            for (int i = 0; i < packet.RewardFields.Count; i++)
            {
                if (keepRewards.TryGetValue(i, out float v)) SingleComposer.GetNumberInput("reward" + i)?.SetValue(v);
                UpdateRewardHint(i);
            }
            UpdateTargetInfo();
        }

        private string? RewardChoice(int i)
        {
            var field = packet.RewardFields[i];
            return field.Choices.Count > 1 ? SingleComposer?.GetDropDown("rewardchoice" + i)?.SelectedValue : field.Choices.FirstOrDefault();
        }

        private void UpdateRewardHint(int i)
        {
            var hint = SingleComposer?.GetDynamicText("rewardhint" + i);
            if (hint == null) return;
            var field = packet.RewardFields[i];
            int c = Math.Max(0, field.Choices.IndexOf(RewardChoice(i) ?? ""));
            long max = c < field.Max.Count ? field.Max[c] : 0;
            long amount = (long)Math.Round(SingleComposer!.GetNumberInput("reward" + i)?.GetValue() ?? 0);
            hint.SetNewText(amount > max ? Lang.Get("arcanumlib:board-post-reward-toomuch", max) : Lang.Get("arcanumlib:board-post-reward-have", max));
        }

        private bool noExpiry;
        private bool goalChanged;
        private double? expireDays;
        private string? pickedCode;

        private void SelectGoal(int index)
        {
            if (index < 0 || index == goalIndex) return;
            if (SingleComposer?.GetNumberInput("expire") is { } e && !noExpiry) expireDays = e.GetValue();
            goalIndex = index;
            goalChanged = true;
            pickedCode = null;
            Compose();
        }

        private void ToggleNoExpiry()
        {
            if (!noExpiry && SingleComposer?.GetNumberInput("expire") is { } e) expireDays = e.GetValue();
            noExpiry = !noExpiry;
            Compose();
        }

        private void UpdateExpire()
        {
            var input = SingleComposer?.GetNumberInput("expire");
            if (input != null) input.Enabled = !noExpiry;
        }

        private bool OnFromHand()
        {
            var stack = capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (stack == null)
            {
                SingleComposer.GetDynamicText("targetinfo")?.SetNewText(Lang.Get("arcanumlib:board-post-hand-empty"));
                return true;
            }
            pickedCode = stack.Collectible.Code.ToString();
            SingleComposer.GetTextInput("target")?.SetValue(stack.GetName());
            UpdateTargetInfo();
            return true;
        }

        private void OnTargetTyped(string text)
        {
            if (pickedCode != null && text != TargetName(pickedCode)) pickedCode = null;
            UpdateTargetInfo();
        }

        private void UpdateTargetInfo()
        {
            var info = SingleComposer?.GetDynamicText("targetinfo");
            if (info == null) return;
            string text = SingleComposer!.GetTextInput("target")?.GetText() ?? "";
            string? code = ResolveItemCode(text);
            info.SetNewText(string.IsNullOrWhiteSpace(text) ? Lang.Get("arcanumlib:board-post-target-hint")
                : code != null ? Lang.Get("arcanumlib:board-post-target-found", TargetName(code))
                : Lang.Get("arcanumlib:board-post-target-unknown"));
        }

        /// <summary>The item code for what was typed: the held item picked, a code, or an exact item name.</summary>
        private string? ResolveItemCode(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return null;
            if (pickedCode != null) return pickedCode;
            if (text.Contains(':') || text.Contains('*'))
            {
                if (text.Contains('*')) return text;
                var loc = new AssetLocation(text);
                return capi.World.GetItem(loc) != null || capi.World.GetBlock(loc) != null ? loc.ToString() : null;
            }
            foreach (var obj in capi.World.Collectibles)
            {
                if (obj?.Code == null) continue;
                string name;
                try { name = new ItemStack(obj).GetName(); }
                catch { continue; }
                if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase)) return obj.Code.ToString();
            }
            return null;
        }

        private static string DaysText(double days) => days == Math.Floor(days)
            ? Lang.Get("arcanumlib:board-days", (int)days)
            : Lang.Get("arcanumlib:board-hours", (int)Math.Round(days * 24));

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

            string? typed = SingleComposer.GetTextInput("target")?.GetText();
            string? target = goal.Targets.Count > 0
                ? SingleComposer.GetDropDown("target")?.SelectedValue
                : goal.TargetIsItem ? ResolveItemCode(typed ?? "") ?? typed : typed;

            capi.ModLoader.GetModSystem<BoardRegistry>()?.SendPost(new BoardPostMessage
            {
                BoardKey = packet.BoardKey,
                Kind = goal.Kind,
                TargetCode = target,
                Demand = goal.HasDemand ? (int)Math.Round(SingleComposer.GetNumberInput("demand").GetValue()) : 1,
                ExpireDays = noExpiry ? 0 : SingleComposer.GetNumberInput("expire").GetValue(),
                Title = SingleComposer.GetTextInput("title")?.GetText(),
                Description = SingleComposer.GetTextArea("description")?.GetText(),
                Rewards = packet.RewardFields.Select((f, i) => new BoardRewardFieldValue
                {
                    Source = f.Source,
                    Choice = RewardChoice(i),
                    Amount = (long)Math.Round(SingleComposer.GetNumberInput("reward" + i)?.GetValue() ?? 0)
                }).Where(v => v.Amount > 0).ToList()
            });
            return true;
        }
    }
}
