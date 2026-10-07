using System;
using System.Text;
using Cairo;

namespace ArcanumLib.Gui.Theme;

/// <summary>
/// Text with inline vector glyphs. The game's GUI fonts lack many symbols
/// (★ ⚙ ● ○ ∞ ✓ ✕ −) and Cairo draws them as boxes; this draws those
/// characters as Cairo shapes in the current source colour, sized to the font,
/// and everything else with <c>ShowText</c>. Drop-in for
/// <c>ctx.TextExtents</c> / <c>ctx.ShowText</c> pairs.
/// </summary>
public static class GlyphText
{
    private const string Glyphs = "★⚙●○∞✓✕−";

    /// <summary>True when the text contains a glyph drawn as a shape.</summary>
    public static bool HasGlyphs(string text) => text.IndexOfAny(Glyphs.ToCharArray()) >= 0;

    /// <summary>Text with glyphs swapped for a same-size letter — feed this to <c>TextExtents</c> for layout.</summary>
    public static string Measurable(string text)
    {
        if (!HasGlyphs(text)) return text;
        var sb = new StringBuilder(text.Length);
        foreach (char ch in text) sb.Append(Glyphs.IndexOf(ch) >= 0 ? (ch == '∞' ? "MM" : "M") : ch.ToString());
        return sb.ToString();
    }

    /// <summary>Extents of <paramref name="text"/> as <see cref="Show"/> will draw it.</summary>
    public static TextExtents Extents(Context ctx, string text) => ctx.TextExtents(Measurable(text));

    /// <summary>Draw at the current point (baseline), like <c>ShowText</c>.</summary>
    public static void Show(Context ctx, string text)
    {
        if (!HasGlyphs(text)) { ctx.ShowText(text); return; }
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && Glyphs.IndexOf(text[i]) < 0) continue;
            if (i > start) ctx.ShowText(text[start..i]);
            if (i == text.Length) break;
            DrawGlyph(ctx, text[i]);
            start = i + 1;
        }
    }

    private static void DrawGlyph(Context ctx, char ch)
    {
        var p0 = ctx.CurrentPoint;
        double em = ctx.TextExtents("M").Width;
        double cap = -ctx.TextExtents("M").YBearing;   // cap height
        double adv = ch == '∞' ? em * 2 : em;
        double cx = p0.X + adv / 2, cy = p0.Y - cap / 2, r = cap / 2;
        ctx.Save();
        ctx.NewPath();
        ctx.LineCap = LineCap.Round;
        ctx.LineJoin = LineJoin.Round;
        switch (ch)
        {
            case '★':
                for (int k = 0; k < 10; k++)
                {
                    double a = -Math.PI / 2 + k * Math.PI / 5, rr = k % 2 == 0 ? r * 1.15 : r * 0.48;
                    if (k == 0) ctx.MoveTo(cx + rr * Math.Cos(a), cy + rr * Math.Sin(a)); else ctx.LineTo(cx + rr * Math.Cos(a), cy + rr * Math.Sin(a));
                }
                ctx.ClosePath(); ctx.Fill();
                break;
            case '⚙':
                for (int k = 0; k < 16; k++)
                {
                    double a0 = k * Math.PI / 8, a1 = (k + 1) * Math.PI / 8, rr = k % 2 == 0 ? r * 1.05 : r * 0.78;
                    if (k == 0) ctx.MoveTo(cx + rr * Math.Cos(a0), cy + rr * Math.Sin(a0)); else ctx.LineTo(cx + rr * Math.Cos(a0), cy + rr * Math.Sin(a0));
                    ctx.LineTo(cx + rr * Math.Cos(a1), cy + rr * Math.Sin(a1));
                }
                ctx.ClosePath();
                ctx.NewSubPath(); ctx.Arc(cx, cy, r * 0.32, 0, 2 * Math.PI);
                ctx.FillRule = FillRule.EvenOdd; ctx.Fill();
                break;
            case '●':
                ctx.Arc(cx, cy, r * 0.75, 0, 2 * Math.PI); ctx.Fill();
                break;
            case '○':
                ctx.LineWidth = Math.Max(1, r * 0.25);
                ctx.Arc(cx, cy, r * 0.7, 0, 2 * Math.PI); ctx.Stroke();
                break;
            case '∞':
                ctx.LineWidth = Math.Max(1, r * 0.28);
                ctx.Save(); ctx.Translate(cx - r * 0.8, cy); ctx.Scale(1, 0.75); ctx.Arc(0, 0, r * 0.75, 0, 2 * Math.PI); ctx.Restore(); ctx.Stroke();
                ctx.Save(); ctx.Translate(cx + r * 0.8, cy); ctx.Scale(1, 0.75); ctx.Arc(0, 0, r * 0.75, 0, 2 * Math.PI); ctx.Restore(); ctx.Stroke();
                break;
            case '✓':
                ctx.LineWidth = Math.Max(1, r * 0.35);
                ctx.MoveTo(cx - r * 0.8, cy); ctx.LineTo(cx - r * 0.2, cy + r * 0.7); ctx.LineTo(cx + r * 0.9, cy - r * 0.8); ctx.Stroke();
                break;
            case '✕':
                ctx.LineWidth = Math.Max(1, r * 0.3);
                ctx.MoveTo(cx - r * 0.7, cy - r * 0.7); ctx.LineTo(cx + r * 0.7, cy + r * 0.7);
                ctx.MoveTo(cx + r * 0.7, cy - r * 0.7); ctx.LineTo(cx - r * 0.7, cy + r * 0.7); ctx.Stroke();
                break;
            case '−':
                ctx.LineWidth = Math.Max(1, r * 0.3);
                ctx.MoveTo(cx - r * 0.75, cy); ctx.LineTo(cx + r * 0.75, cy); ctx.Stroke();
                break;
        }
        ctx.Restore();
        ctx.MoveTo(p0.X + adv, p0.Y);
    }
}
