using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ArcanumLib.Boards.Gui
{
    /// <summary>
    /// The ArcanumLib board window: a paged list of contracts, the selected contract's details and what the player
    /// can do with it (take, hand in, give up, take back, collect), plus "post". Boards with their own window set
    /// <see cref="ContractBoard.Opener" /> and never open this one.
    /// </summary>
    public class GuiDialogContractBoard : GuiDialog
    {
        private const int RowsPerPage = 10;
        private const double ListW = 300, DetailW = 420, RowH = 30, Gap = 10;

        public static GuiDialogContractBoard? Current { get; private set; }

        private BoardPagePacket page;
        private string? selectedId;
        private int pageIndex;

        public string BoardKey => page.BoardKey;
        public override string? ToggleKeyCombinationCode => null;

        public GuiDialogContractBoard(ICoreClientAPI capi, BoardPagePacket page) : base(capi)
        {
            this.page = page;
        }

        /// <summary>Opens the window for the page, or refreshes the open one.</summary>
        public static void Show(ICoreClientAPI capi, BoardPagePacket page)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (Current != null && Current.IsOpened() && Current.BoardKey == page.BoardKey)
                {
                    Current.page = page;
                    Current.Compose();
                    return;
                }
                Current?.TryClose();
                Current = new GuiDialogContractBoard(capi, page);
                Current.Compose();
                Current.TryOpen();
            }, "arcanumlib-board-page");
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            if (Current == this) Current = null;
        }

        private BoardRegistry? Registry => capi.ModLoader.GetModSystem<BoardRegistry>();
        private string MyUid => capi.World.Player.PlayerUID;

        private List<BoardContract> Visible()
        {
            // Own contracts first, then open ones, newest first; finished ones of others are not shown.
            return page.Contracts
                .Where(c => c.State != BoardStates.Fulfilled || c.AuthorUid == MyUid || c.ClaimedByUid == MyUid)
                .OrderByDescending(c => c.AuthorUid == MyUid || c.ClaimedByUid == MyUid)
                .ThenBy(c => c.State == BoardStates.Open ? 0 : 1)
                .ThenByDescending(c => c.CreatedTotalDays)
                .ToList();
        }

        private void Compose()
        {
            var contracts = Visible();
            int pages = Math.Max(1, (contracts.Count + RowsPerPage - 1) / RowsPerPage);
            pageIndex = Math.Clamp(pageIndex, 0, pages - 1);
            if (selectedId != null && contracts.All(c => c.Id != selectedId)) selectedId = null;
            selectedId ??= contracts.FirstOrDefault()?.Id;
            var selected = contracts.FirstOrDefault(c => c.Id == selectedId);

            double listH = RowsPerPage * (RowH + 4);
            var bg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bg.BothSizing = ElementSizing.FitToChildren;
            var dialog = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            var composer = capi.Gui.CreateCompo("arcanumlib-board-" + page.BoardKey, dialog)
                .AddShadedDialogBG(bg)
                .AddDialogTitleBar(Lang.Get(page.TitleKey), () => TryClose());

            double y = 40;
            var rows = contracts.Skip(pageIndex * RowsPerPage).Take(RowsPerPage).ToList();
            if (rows.Count == 0)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-empty"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, ListW, RowH));
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var c = rows[i];
                string id = c.Id;
                string label = Shorten(TitleOf(c), 34) + "  ·  " + StateText(c);
                composer.AddSmallButton(label, () => { selectedId = id; Compose(); return true; },
                    ElementBounds.Fixed(0, y + i * (RowH + 4), ListW, RowH),
                    c.Id == selectedId ? EnumButtonStyle.MainMenu : EnumButtonStyle.Normal, "row" + i);
            }

            double pagerY = y + listH + Gap;
            composer.AddSmallButton("<", () => { pageIndex--; Compose(); return true; }, ElementBounds.Fixed(0, pagerY, 40, RowH), EnumButtonStyle.Normal, "prev");
            composer.AddStaticText($"{pageIndex + 1} / {pages}", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(50, pagerY + 6, ListW - 100, RowH));
            composer.AddSmallButton(">", () => { pageIndex++; Compose(); return true; }, ElementBounds.Fixed(ListW - 40, pagerY, 40, RowH), EnumButtonStyle.Normal, "next");
            if (page.CanPost)
            {
                composer.AddSmallButton(Lang.Get("arcanumlib:board-post"), () => { Registry?.SendAction(page.BoardKey, BoardActions.OpenPost); return true; },
                    ElementBounds.Fixed(0, pagerY + RowH + Gap, ListW, RowH), EnumButtonStyle.Normal, "post");
            }

            double dx = ListW + 2 * Gap;
            double detailH = listH - RowH - Gap;
            composer.AddRichtext(selected != null ? DetailText(selected) : Lang.Get("arcanumlib:board-select"), CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(dx, y, DetailW, detailH), "detail");

            if (selected != null) AddActions(composer, selected, dx, y + detailH + Gap);

            SingleComposer = composer.Compose();
            SingleComposer.GetButton("prev").Enabled = pageIndex > 0;
            SingleComposer.GetButton("next").Enabled = pageIndex < pages - 1;
        }

        private void AddActions(GuiComposer composer, BoardContract c, double x, double y)
        {
            var actions = new List<(string text, string action)>();
            bool mine = c.AuthorUid == MyUid;
            if (c.State == BoardStates.Open && !mine) actions.Add(("arcanumlib:board-accept", BoardActions.Accept));
            if (c.State == BoardStates.Open && mine && c.Source == BoardSources.Player) actions.Add(("arcanumlib:board-cancel", BoardActions.Cancel));
            if (c.State == BoardStates.Claimed && c.ClaimedByUid == MyUid)
            {
                actions.Add(("arcanumlib:board-turnin", BoardActions.TurnIn));
                actions.Add(("arcanumlib:board-abandon", BoardActions.Abandon));
            }
            if (c.State == BoardStates.Fulfilled && (mine || c.ClaimedByUid == MyUid)) actions.Add(("arcanumlib:board-collect", BoardActions.Collect));

            double w = actions.Count > 0 ? (DetailW - Gap * (actions.Count - 1)) / actions.Count : DetailW;
            for (int i = 0; i < actions.Count; i++)
            {
                var (text, action) = actions[i];
                string id = c.Id;
                composer.AddSmallButton(Lang.Get(text), () => { Registry?.SendAction(page.BoardKey, action, id); return true; },
                    ElementBounds.Fixed(x + i * (w + Gap), y, w, RowH), EnumButtonStyle.Normal, "act" + i);
            }
        }

        private string GoalName(string? kind)
        {
            var info = page.Goals.FirstOrDefault(g => string.Equals(g.Kind, kind, StringComparison.OrdinalIgnoreCase));
            if (info != null) return Lang.Get(info.NameKey);
            string key = page.LangPrefix + "kind-" + kind;
            return Lang.HasTranslation(key) ? Lang.Get(key) : kind ?? "?";
        }

        private string TitleOf(BoardContract c) =>
            !string.IsNullOrWhiteSpace(c.CustomTitle) ? c.CustomTitle! : $"{GoalName(c.Kind)}: {TargetText(c)}";

        private string TargetText(BoardContract c)
        {
            if (!string.IsNullOrEmpty(c.TargetPlayerName)) return c.TargetPlayerName!;
            if (!string.IsNullOrEmpty(c.TargetName)) return c.TargetName!;
            return CodeName(c.TargetCode);
        }

        private string CodeName(string? code)
        {
            if (string.IsNullOrEmpty(code)) return "?";
            if (!code.Contains('*'))
            {
                var loc = new AssetLocation(code);
                CollectibleObject? obj = capi.World.GetItem(loc) ?? (CollectibleObject?)capi.World.GetBlock(loc);
                if (obj != null) return new ItemStack(obj).GetName();
                var entity = capi.World.GetEntityType(loc);
                if (entity != null) return Lang.Get(entity.Code.Domain + ":item-creature-" + entity.Code.Path);
            }
            return code;
        }

        private string StateText(BoardContract c)
        {
            if (c.State == BoardStates.Claimed && c.ClaimedByUid == MyUid) return Lang.Get("arcanumlib:board-state-mine");
            return Lang.Get("arcanumlib:board-state-" + c.State);
        }

        private string DetailText(BoardContract c)
        {
            var sb = new StringBuilder();
            sb.Append("<font size=\"18\"><strong>").Append(Escape(TitleOf(c))).Append("</strong></font><br>");
            sb.Append(Escape(Lang.Get("arcanumlib:board-detail-goal", GoalName(c.Kind), TargetText(c), Math.Max(1, c.Demand)))).Append("<br>");
            sb.Append(Escape(Lang.Get("arcanumlib:board-detail-author", c.Source == BoardSources.Server ? Lang.Get("arcanumlib:board-server") : c.AuthorName ?? "?"))).Append("<br>");
            if (!string.IsNullOrEmpty(c.ClaimedByName))
                sb.Append(Escape(Lang.Get("arcanumlib:board-detail-claimer", c.ClaimedByName!))).Append("<br>");
            sb.Append(Escape(Lang.Get("arcanumlib:board-detail-state", StateText(c)))).Append("<br>");
            if (c.ExpireTotalDays > 0)
                sb.Append(Escape(Lang.Get("arcanumlib:board-detail-expires", Math.Max(0, c.ExpireTotalDays - page.NowTotalDays).ToString("0.#")))).Append("<br>");

            var rewards = (c.Rewards ?? new List<BoardRewardEntry>()).Select(RewardText).Where(t => t != null).ToList();
            if (rewards.Count > 0)
                sb.Append("<br>").Append(Escape(Lang.Get("arcanumlib:board-detail-reward"))).Append("<br>")
                  .Append(string.Join("<br>", rewards.Select(r => "• " + Escape(r!)))).Append("<br>");

            if (!string.IsNullOrWhiteSpace(c.CustomDescription))
                sb.Append("<br>").Append(Escape(c.CustomDescription!));
            return sb.ToString();
        }

        private string? RewardText(BoardRewardEntry r)
        {
            if (!string.IsNullOrEmpty(r.ItemCode) && r.ItemAmount > 0) return $"{CodeName(r.ItemCode)} ×{r.ItemAmount}";
            if (!string.IsNullOrEmpty(r.Kind))
            {
                string key = r.Kind + "-reward";
                return Lang.HasTranslation(key) ? Lang.Get(key, r.Amount, r.Code ?? "") : $"{r.Code} ×{r.Amount}";
            }
            if (!string.IsNullOrEmpty(r.CurrencyKey) && r.CurrencyAmount > 0) return $"{r.CurrencyKey} ×{r.CurrencyAmount}";
            return null;
        }

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";

        private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
