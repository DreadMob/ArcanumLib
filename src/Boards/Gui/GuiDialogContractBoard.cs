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
        private const int RowsPerPage = 7;
        private const double ListW = 380, DetailW = 420, RowH = 52, RowGap = 6, BtnH = 30, Gap = 10;

        /// <summary>Filter value of "only my contracts".</summary>
        private const string MineFilter = "@mine";

        public static GuiDialogContractBoard? Current { get; private set; }

        private BoardPagePacket page;
        private string? selectedId;
        private int pageIndex;
        private string? filter;
        private bool disputing;

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
                    Current.disputing = false;
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

        private bool IsMine(BoardContract c) => c.AuthorUid == MyUid || c.ClaimedByUid == MyUid;

        private List<BoardContract> Visible()
        {
            // Own contracts first, then open ones, newest first; finished or awaiting ones of others are not shown.
            return page.Contracts
                .Where(c => (c.State != BoardStates.Fulfilled && !BoardStates.IsAwaiting(c.State)) || IsMine(c))
                .OrderByDescending(IsMine)
                .ThenBy(c => c.State == BoardStates.Open ? 0 : 1)
                .ThenByDescending(c => c.CreatedTotalDays)
                .ToList();
        }

        private bool PassesFilter(BoardContract c) =>
            filter == null || (filter == MineFilter ? IsMine(c) : string.Equals(c.Kind, filter, StringComparison.OrdinalIgnoreCase));

        private BoardGoalInfo? GoalInfo(string? kind) => page.Goals.FirstOrDefault(g => string.Equals(g.Kind, kind, StringComparison.OrdinalIgnoreCase));

        private bool Confirms(BoardContract c) => c.Source == BoardSources.Player && (GoalInfo(c.Kind)?.ConfirmDays ?? 0) > 0;

        private void Compose()
        {
            var all = Visible();
            var contracts = all.Where(PassesFilter).ToList();
            int pages = Math.Max(1, (contracts.Count + RowsPerPage - 1) / RowsPerPage);
            pageIndex = Math.Clamp(pageIndex, 0, pages - 1);
            if (selectedId != null && contracts.All(c => c.Id != selectedId)) selectedId = null;
            selectedId ??= contracts.FirstOrDefault()?.Id;
            var selected = contracts.FirstOrDefault(c => c.Id == selectedId);

            double listH = RowsPerPage * (RowH + RowGap);
            var bg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bg.BothSizing = ElementSizing.FitToChildren;
            var dialog = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            var composer = capi.Gui.CreateCompo("arcanumlib-board-" + page.BoardKey, dialog)
                .AddShadedDialogBG(bg)
                .AddDialogTitleBar(Lang.Get(page.TitleKey), () => TryClose());

            double y = 40;
            AddFilters(composer, all, y);
            y += BtnH + Gap;

            var rows = contracts.Skip(pageIndex * RowsPerPage).Take(RowsPerPage).Select(RowOf).ToList();
            if (rows.Count == 0)
                composer.AddStaticText(Lang.Get("arcanumlib:board-empty"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 6, ListW, BtnH));
            else
                composer.AddBoardRows(ElementBounds.Fixed(0, y, ListW, listH), rows, RowH, RowGap, id => { selectedId = id; disputing = false; Compose(); }, "rows");

            double pagerY = y + listH + Gap;
            composer.AddSmallButton("<", () => { pageIndex--; Compose(); return true; }, ElementBounds.Fixed(0, pagerY, 40, BtnH), EnumButtonStyle.Normal, "prev");
            composer.AddStaticText($"{pageIndex + 1} / {pages}", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(50, pagerY + 6, ListW - 100, BtnH));
            composer.AddSmallButton(">", () => { pageIndex++; Compose(); return true; }, ElementBounds.Fixed(ListW - 40, pagerY, 40, BtnH), EnumButtonStyle.Normal, "next");
            double bottomY = pagerY + BtnH + Gap;
            if (page.CanPost)
            {
                composer.AddSmallButton(Lang.Get("arcanumlib:board-post"), () => { Registry?.SendAction(page.BoardKey, BoardActions.OpenPost); return true; },
                    ElementBounds.Fixed(0, bottomY, ListW, BtnH), EnumButtonStyle.Normal, "post");
            }

            double dx = ListW + 2 * Gap;
            double actionsY = bottomY;
            if (selected == null)
            {
                composer.AddStaticText(Lang.Get("arcanumlib:board-select"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(dx, 40, DetailW, BtnH));
            }
            else
            {
                AddDetail(composer, selected, dx, 40, actionsY - Gap - 40);
                AddActions(composer, selected, dx, actionsY);
            }

            SingleComposer = composer.Compose();
            SingleComposer.GetButton("prev").Enabled = pageIndex > 0;
            SingleComposer.GetButton("next").Enabled = pageIndex < pages - 1;
        }

        private void AddFilters(GuiComposer composer, List<BoardContract> all, double y)
        {
            var filters = new List<(string? value, string text)> { (null, Lang.Get("arcanumlib:board-filter-all", all.Count)) };
            // One button per goal when they fit; otherwise only "all" and "mine".
            if (page.Goals.Count is > 1 and <= 3)
                foreach (var g in page.Goals)
                    filters.Add((g.Kind, Lang.Get("arcanumlib:board-filter-kind", Lang.Get(g.NameKey), all.Count(c => string.Equals(c.Kind, g.Kind, StringComparison.OrdinalIgnoreCase)))));
            filters.Add((MineFilter, Lang.Get("arcanumlib:board-filter-mine", all.Count(IsMine))));

            double w = (ListW - 4 * (filters.Count - 1)) / filters.Count;
            for (int i = 0; i < filters.Count; i++)
            {
                var (value, text) = filters[i];
                composer.AddSmallButton(text, () => { filter = value; pageIndex = 0; selectedId = null; disputing = false; Compose(); return true; },
                    ElementBounds.Fixed(i * (w + 4), y, w, BtnH), filter == value ? EnumButtonStyle.MainMenu : EnumButtonStyle.Normal, "filter" + i);
            }
        }

        private BoardRowView RowOf(BoardContract c)
        {
            int kindIndex = page.Goals.FindIndex(g => string.Equals(g.Kind, c.Kind, StringComparison.OrdinalIgnoreCase));
            string who = c.Source == BoardSources.Server ? Lang.Get("arcanumlib:board-server")
                : c.AuthorUid == MyUid ? Lang.Get("arcanumlib:board-you") : c.AuthorName ?? "?";
            return new BoardRowView
            {
                Id = c.Id,
                Kind = new BoardPill(page.Goals.Count > 1 ? GoalName(c.Kind) : "", BoardColors.ForKind(kindIndex)),
                Title = TitleOf(c),
                Sub = who + " · " + WhenText(c),
                Reward = string.Join(", ", (c.Rewards ?? new List<BoardRewardEntry>()).Select(RewardText).Where(t => t != null)),
                State = StatePill(c),
                Selected = c.Id == selectedId
            };
        }

        /// <summary>Time shown in a row: days left to run, the author's answer, or the dispute.</summary>
        private string WhenText(BoardContract c)
        {
            if (c.State == BoardStates.Disputed) return Lang.Get("arcanumlib:board-row-disputed");
            if (c.State == BoardStates.Done)
                return Lang.Get("arcanumlib:board-row-answer-in", DaysHours(ContractBoard.ConfirmDue(c) - page.NowTotalDays));
            if (c.ExpireTotalDays > 0) return Lang.Get("arcanumlib:board-row-days", Math.Max(0, c.ExpireTotalDays - page.NowTotalDays).ToString("0.#"));
            return Lang.Get("arcanumlib:board-row-noexpiry");
        }

        private BoardPill StatePill(BoardContract c)
        {
            bool mine = c.AuthorUid == MyUid;
            return c.State switch
            {
                BoardStates.Open when mine => new BoardPill(Lang.Get("arcanumlib:board-state-own"), BoardColors.Blue),
                BoardStates.Open => new BoardPill(Lang.Get("arcanumlib:board-state-open"), BoardColors.Orange),
                BoardStates.Claimed when c.ClaimedByUid == MyUid => new BoardPill(Lang.Get("arcanumlib:board-state-mine"), BoardColors.Blue),
                BoardStates.Claimed => new BoardPill(Lang.Get("arcanumlib:board-state-claimed"), BoardColors.Grey),
                BoardStates.Done => new BoardPill(Lang.Get("arcanumlib:board-state-done"), BoardColors.Orange),
                BoardStates.Disputed => new BoardPill(Lang.Get("arcanumlib:board-state-disputed"), BoardColors.Red),
                BoardStates.Fulfilled => new BoardPill(Lang.Get("arcanumlib:board-state-collect"), BoardColors.Green),
                _ => new BoardPill(Lang.Get("arcanumlib:board-state-" + c.State), BoardColors.Grey)
            };
        }

        private void AddDetail(GuiComposer composer, BoardContract c, double x, double y, double h)
        {
            bool confirms = Confirms(c);
            double textH = confirms ? h - 170 : h;
            composer.AddRichtext(DetailText(c), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, DetailW, textH), "detail");
            if (!confirms) return;

            double sy = y + textH + Gap;
            int reached = c.State switch
            {
                BoardStates.Claimed => 0,
                BoardStates.Done or BoardStates.Disputed => 1,
                BoardStates.Fulfilled or BoardStates.Completed => 2,
                _ => -1
            };
            var steps = new[] { Lang.Get("arcanumlib:board-step-claimed"), Lang.Get("arcanumlib:board-step-done"), Lang.Get("arcanumlib:board-step-accepted") };
            composer.AddBoardSteps(ElementBounds.Fixed(x, sy, DetailW, 44), steps, reached, c.State == BoardStates.Disputed ? BoardColors.Red : BoardColors.Green);

            string? info = ConfirmInfo(c);
            if (info != null)
            {
                var box = ElementBounds.Fixed(x, sy + 54, DetailW, 100);
                composer.AddInset(box, 2, 0.75f);
                composer.AddRichtext(info, CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + 8, sy + 60, DetailW - 16, 88));
            }
        }

        /// <summary>What the confirmation step means for this player right now.</summary>
        private string? ConfirmInfo(BoardContract c)
        {
            bool author = c.AuthorUid == MyUid, claimer = c.ClaimedByUid == MyUid;
            string days = DaysHours(GoalInfo(c.Kind)?.ConfirmDays ?? 0);
            string left = "<strong>" + DaysHours(ContractBoard.ConfirmDue(c) - page.NowTotalDays) + "</strong>";
            string? reason = c.GetData(ContractBoard.DisputeKey);
            return c.State switch
            {
                BoardStates.Open => Lang.Get("arcanumlib:board-info-howto", days),
                BoardStates.Claimed when claimer => Lang.Get("arcanumlib:board-info-howto", days),
                BoardStates.Claimed when author => Lang.Get("arcanumlib:board-info-author-wait", Escape(c.ClaimedByName ?? "?")),
                BoardStates.Done when author => Lang.Get("arcanumlib:board-info-author-done", Escape(c.ClaimedByName ?? "?"), left),
                BoardStates.Done => Lang.Get("arcanumlib:board-info-claimer-done", Escape(c.AuthorName ?? "?"), left),
                BoardStates.Disputed => Lang.Get("arcanumlib:board-info-disputed", Escape(reason ?? "")),
                BoardStates.Fulfilled when claimer => Lang.Get("arcanumlib:board-info-accepted"),
                _ => null
            };
        }

        private void AddActions(GuiComposer composer, BoardContract c, double x, double y)
        {
            var actions = new List<(string text, Func<bool> onClick)>();
            bool mine = c.AuthorUid == MyUid;
            string id = c.Id;
            Func<bool> Send(string action) => () => { Registry?.SendAction(page.BoardKey, action, id); return true; };

            if (c.State == BoardStates.Open && !mine) actions.Add(("arcanumlib:board-accept", Send(BoardActions.Accept)));
            if (c.State == BoardStates.Open && mine && c.Source == BoardSources.Player) actions.Add(("arcanumlib:board-cancel", Send(BoardActions.Cancel)));
            if (c.State == BoardStates.Claimed && c.ClaimedByUid == MyUid)
            {
                actions.Add(("arcanumlib:board-abandon", Send(BoardActions.Abandon)));
                actions.Add((Confirms(c) ? "arcanumlib:board-markdone" : "arcanumlib:board-turnin", Send(BoardActions.TurnIn)));
            }
            if (c.State == BoardStates.Done && mine)
            {
                if (disputing)
                {
                    composer.AddTextInput(ElementBounds.Fixed(x, y - BtnH - Gap, DetailW, BtnH), null, CairoFont.WhiteSmallText(), "reason");
                    actions.Add(("arcanumlib:board-post-cancel", () => { disputing = false; Compose(); return true; }));
                    actions.Add(("arcanumlib:board-dispute-send", () =>
                    {
                        Registry?.SendAction(page.BoardKey, BoardActions.Dispute, id, SingleComposer.GetTextInput("reason")?.GetText());
                        return true;
                    }));
                }
                else
                {
                    actions.Add(("arcanumlib:board-confirm", Send(BoardActions.Confirm)));
                    actions.Add(("arcanumlib:board-dispute", () => { disputing = true; Compose(); return true; }));
                }
            }
            if (c.State == BoardStates.Fulfilled && (mine || c.ClaimedByUid == MyUid)) actions.Add(("arcanumlib:board-collect", Send(BoardActions.Collect)));

            double w = actions.Count > 0 ? (DetailW - Gap * (actions.Count - 1)) / actions.Count : DetailW;
            for (int i = 0; i < actions.Count; i++)
            {
                var (text, onClick) = actions[i];
                composer.AddSmallButton(Lang.Get(text), () => onClick(), ElementBounds.Fixed(x + i * (w + Gap), y, w, BtnH), EnumButtonStyle.Normal, "act" + i);
            }
        }

        /// <summary>"2 дн. 14 ч" from a span in days.</summary>
        private static string DaysHours(double days)
        {
            days = Math.Max(0, days);
            int d = (int)Math.Floor(days);
            int h = (int)Math.Floor((days - d) * 24);
            if (d == 0) return Lang.Get("arcanumlib:board-hours", Math.Max(h, days > 0 ? 1 : 0));
            return h == 0 ? Lang.Get("arcanumlib:board-days", d) : Lang.Get("arcanumlib:board-days-hours", d, h);
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

        private string DetailText(BoardContract c)
        {
            var sb = new StringBuilder();
            if (page.Goals.Count > 1)
                sb.Append("<font color=\"#a8c4e0\">").Append(Escape(GoalName(c.Kind))).Append("</font><br>");
            sb.Append("<font size=\"18\"><strong>").Append(Escape(TitleOf(c))).Append("</strong></font><br>");
            if (!string.IsNullOrWhiteSpace(c.CustomTitle))
                sb.Append(Escape(Lang.Get("arcanumlib:board-detail-goal", GoalName(c.Kind), TargetText(c), Math.Max(1, c.Demand)))).Append("<br>");
            sb.Append(Escape(Lang.Get("arcanumlib:board-detail-author", c.Source == BoardSources.Server ? Lang.Get("arcanumlib:board-server") : c.AuthorName ?? "?"))).Append("<br>");
            if (!string.IsNullOrEmpty(c.ClaimedByName))
                sb.Append(Escape(Lang.Get("arcanumlib:board-detail-claimer", c.ClaimedByUid == MyUid ? Lang.Get("arcanumlib:board-you") : c.ClaimedByName!))).Append("<br>");
            if (c.ExpireTotalDays > 0 && BoardStates.IsActive(c.State))
                sb.Append(Escape(Lang.Get("arcanumlib:board-detail-expires", Math.Max(0, c.ExpireTotalDays - page.NowTotalDays).ToString("0.#")))).Append("<br>");

            var rewards = (c.Rewards ?? new List<BoardRewardEntry>()).Select(RewardText).Where(t => t != null).ToList();
            if (rewards.Count > 0)
                sb.Append("<font color=\"#e89e5c\"><strong>").Append(Escape(Lang.Get("arcanumlib:board-detail-reward"))).Append(' ')
                  .Append(Escape(string.Join(", ", rewards!))).Append("</strong></font><br>");

            if (!string.IsNullOrWhiteSpace(c.CustomDescription))
                sb.Append("<br><i>").Append(Escape(c.CustomDescription!)).Append("</i>");
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

        private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
