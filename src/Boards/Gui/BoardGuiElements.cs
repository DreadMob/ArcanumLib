using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Boards.Gui
{
    /// <summary>A small rounded label with a coloured outline ("Доставка", "открыт").</summary>
    public readonly record struct BoardPill(string Text, double[] Color);

    /// <summary>One contract row as the board list draws it.</summary>
    public sealed class BoardRowView
    {
        public string Id = "";
        public BoardPill Kind;
        public string Title = "";
        public string Sub = "";
        public string Reward = "";
        public BoardPill State;
        public bool Selected;
    }

    /// <summary>Colours of the board windows (vanilla-like brown dialog).</summary>
    public static class BoardColors
    {
        public static readonly double[] Orange = { 0.91, 0.62, 0.36, 1 };
        public static readonly double[] Blue = { 0.53, 0.70, 0.88, 1 };
        public static readonly double[] Green = { 0.58, 0.82, 0.44, 1 };
        public static readonly double[] Red = { 0.90, 0.40, 0.33, 1 };
        public static readonly double[] Grey = { 0.60, 0.56, 0.50, 1 };
        public static readonly double[] Violet = { 0.74, 0.58, 0.88, 1 };
        public static readonly double[] Text = { 0.92, 0.88, 0.80, 1 };
        public static readonly double[] Dim = { 0.70, 0.65, 0.58, 1 };

        private static readonly double[][] KindColors = { Orange, Blue, Green, Violet };

        /// <summary>Colour of a goal kind by its place on the board.</summary>
        public static double[] ForKind(int index) => KindColors[Math.Abs(index) % KindColors.Length];
    }

    internal static class BoardDraw
    {
        public static void RoundRect(Context ctx, double x, double y, double w, double h, double r)
        {
            ctx.NewPath();
            ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
            ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
            ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
            ctx.Arc(x + r, y + r, r, Math.PI, 3 * Math.PI / 2);
            ctx.ClosePath();
        }

        public static void SetColor(Context ctx, double[] c, double alpha = 1) => ctx.SetSourceRGBA(c[0], c[1], c[2], c[3] * alpha);

        public static void Font(Context ctx, double size, bool bold)
        {
            ctx.SelectFontFace(GuiStyle.StandardFontName, FontSlant.Normal, bold ? FontWeight.Bold : FontWeight.Normal);
            ctx.SetFontSize(GuiElement.scaled(size));
        }

        /// <summary>Text cut with "…" to fit <paramref name="maxW" />.</summary>
        public static string Fit(Context ctx, string text, double maxW)
        {
            if (maxW <= 0) return "";
            if (GlyphText.Extents(ctx, text).Width <= maxW) return text;
            for (int len = text.Length - 1; len > 0; len--)
            {
                string cut = text.Substring(0, len).TrimEnd() + "…";
                if (GlyphText.Extents(ctx, cut).Width <= maxW) return cut;
            }
            return "…";
        }

        public static void Text(Context ctx, string text, double x, double baseline)
        {
            ctx.MoveTo(x, baseline);
            GlyphText.Show(ctx, text);
        }

        /// <summary>Draws a pill with its left edge at x (or right edge when <paramref name="alignRight" />); returns its width.</summary>
        public static double Pill(Context ctx, BoardPill pill, double x, double centerY, bool alignRight = false)
        {
            if (string.IsNullOrEmpty(pill.Text)) return 0;
            Font(ctx, 12, true);
            double tw = GlyphText.Extents(ctx, pill.Text).Width;
            double pad = GuiElement.scaled(7), h = GuiElement.scaled(19);
            double w = tw + 2 * pad;
            double left = alignRight ? x - w : x;
            RoundRect(ctx, left, centerY - h / 2, w, h, h / 2);
            SetColor(ctx, pill.Color, 0.18);
            ctx.FillPreserve();
            SetColor(ctx, pill.Color, 0.9);
            ctx.LineWidth = GuiElement.scaled(1.2);
            ctx.Stroke();
            SetColor(ctx, pill.Color);
            Text(ctx, pill.Text, left + pad, centerY + GuiElement.scaled(4.5));
            return w;
        }
    }

    /// <summary>
    /// The board's contract rows: goal kind and state pills, title, author · time and the reward. A click selects a
    /// row. Drawn once per compose (the window recomposes on every change).
    /// </summary>
    public class GuiElementBoardRows : GuiElement
    {
        private readonly List<BoardRowView> rows;
        private readonly double rowH, gap;
        private readonly Action<string> onClick;

        public GuiElementBoardRows(ICoreClientAPI capi, ElementBounds bounds, List<BoardRowView> rows, double rowH, double gap, Action<string> onClick)
            : base(capi, bounds)
        {
            this.rows = rows;
            this.rowH = rowH;
            this.gap = gap;
            this.onClick = onClick;
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            double x = Bounds.drawX, w = Bounds.InnerWidth;
            double h = scaled(rowH), pad = scaled(10);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                double y = Bounds.drawY + i * scaled(rowH + gap);

                BoardDraw.RoundRect(ctx, x, y, w, h, scaled(3));
                ctx.SetSourceRGBA(row.Selected ? 0.45 : 0.30, row.Selected ? 0.33 : 0.22, row.Selected ? 0.22 : 0.15, 0.95);
                ctx.FillPreserve();
                ctx.SetSourceRGBA(0, 0, 0, 0.45);
                ctx.LineWidth = scaled(1.5);
                ctx.Stroke();

                double top = y + h * 0.31, bottom = y + h * 0.74;
                double kindW = BoardDraw.Pill(ctx, row.Kind, x + pad, top);
                double stateW = BoardDraw.Pill(ctx, row.State, x + w - pad, top, alignRight: true);

                BoardDraw.Font(ctx, 15, true);
                BoardDraw.SetColor(ctx, BoardColors.Text);
                double titleX = x + pad + (kindW > 0 ? kindW + scaled(7) : 0);
                double titleMax = x + w - pad - stateW - scaled(8) - titleX;
                BoardDraw.Text(ctx, BoardDraw.Fit(ctx, row.Title, titleMax), titleX, top + scaled(5.5));

                BoardDraw.Font(ctx, 15, true);
                double rewardW = Math.Min(GlyphText.Extents(ctx, row.Reward).Width, w * 0.5);
                string reward = BoardDraw.Fit(ctx, row.Reward, rewardW);
                BoardDraw.SetColor(ctx, BoardColors.Orange);
                BoardDraw.Text(ctx, reward, x + w - pad - GlyphText.Extents(ctx, reward).Width, bottom + scaled(5));

                BoardDraw.Font(ctx, 13, false);
                BoardDraw.SetColor(ctx, BoardColors.Dim);
                BoardDraw.Text(ctx, BoardDraw.Fit(ctx, row.Sub, w - 3 * pad - rewardW), x + pad, bottom + scaled(4.5));
            }
        }

        public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
        {
            base.OnMouseDownOnElement(capi, args);
            double local = args.Y - Bounds.absY;
            int index = (int)(local / scaled(rowH + gap));
            if (index < 0 || index >= rows.Count || local - index * scaled(rowH + gap) > scaled(rowH)) return;
            args.Handled = true;
            onClick(rows[index].Id);
        }
    }

    /// <summary>A step strip ("Взят → Выполнено → Принят"): dots joined by a line, steps up to <c>reached</c> lit.</summary>
    public class GuiElementBoardSteps : GuiElement
    {
        private readonly string[] steps;
        private readonly int reached;
        private readonly double[] litColor;

        public GuiElementBoardSteps(ICoreClientAPI capi, ElementBounds bounds, string[] steps, int reached, double[] litColor) : base(capi, bounds)
        {
            this.steps = steps;
            this.reached = reached;
            this.litColor = litColor;
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            if (steps.Length == 0) return;
            double w = Bounds.InnerWidth, x0 = Bounds.drawX, dotY = Bounds.drawY + scaled(10), r = scaled(7);
            double slot = w / steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                double cx = x0 + slot * (i + 0.5);
                if (i + 1 < steps.Length)
                {
                    ctx.NewPath();
                    ctx.MoveTo(cx + r, dotY);
                    ctx.LineTo(cx + slot - r, dotY);
                    ctx.LineWidth = scaled(2.5);
                    if (i + 1 <= reached) BoardDraw.SetColor(ctx, litColor); else ctx.SetSourceRGBA(0.35, 0.30, 0.25, 1);
                    ctx.Stroke();
                }
                ctx.NewPath();
                ctx.Arc(cx, dotY, r, 0, 2 * Math.PI);
                if (i <= reached) BoardDraw.SetColor(ctx, litColor); else ctx.SetSourceRGBA(0.35, 0.30, 0.25, 1);
                ctx.Fill();

                BoardDraw.Font(ctx, 13, true);
                BoardDraw.SetColor(ctx, i <= reached ? BoardColors.Text : BoardColors.Grey);
                double tw = GlyphText.Extents(ctx, steps[i]).Width;
                BoardDraw.Text(ctx, steps[i], cx - tw / 2, dotY + scaled(28));
            }
        }
    }

    public static class BoardGuiComposerExtensions
    {
        public static GuiComposer AddBoardRows(this GuiComposer composer, ElementBounds bounds, List<BoardRowView> rows, double rowH, double gap, Action<string> onClick, string key)
        {
            if (!composer.Composed) composer.AddInteractiveElement(new GuiElementBoardRows(composer.Api, bounds, rows, rowH, gap, onClick), key);
            return composer;
        }

        public static GuiComposer AddBoardSteps(this GuiComposer composer, ElementBounds bounds, string[] steps, int reached, double[] litColor)
        {
            if (!composer.Composed) composer.AddStaticElement(new GuiElementBoardSteps(composer.Api, bounds, steps, reached, litColor));
            return composer;
        }
    }
}
