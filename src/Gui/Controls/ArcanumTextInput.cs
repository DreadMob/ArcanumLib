using System;
using ArcanumLib.Diagnostics;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed text input: recessed dark field, brass rim, blinking caret.
/// Single-line by default; <paramref name="multiline" /> gives a textarea.
/// Same call surface as vanilla: <see cref="SetValue" /> / <see cref="GetText" />.
/// </summary>
public class ArcanumTextInput : GuiElement
{
    private readonly bool multiline;
    private readonly Action<string>? onChanged;
    private readonly CairoFont font;
    private readonly int maxLength;    // 0 = unlimited

    private string text = "";
    private int caret;                 // flat index into text
    private int selStart;              // selection anchor; selStart < selEnd = active selection
    private int selEnd;                // selection end; the caret sits at selEnd while selecting
    private bool hovered;
    private LoadedTexture tex;
    private LoadedTexture caretTex;
    private string? cacheKey;
    private double blinkAccum;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">Field bounds.</param>
    /// <param name="onChanged">Called with the new text on every edit.</param>
    /// <param name="font">Text font (defaults to detail text).</param>
    /// <param name="multiline">Allow newlines (textarea mode).</param>
    /// <param name="maxLength">Max characters; 0 = unlimited.</param>
    public ArcanumTextInput(ICoreClientAPI capi, ElementBounds bounds,
        Action<string>? onChanged = null, CairoFont? font = null,
        bool multiline = false, int maxLength = 0)
        : base(capi, bounds)
    {
        this.onChanged = onChanged;
        this.font = font ?? CairoFont.WhiteDetailText();
        this.multiline = multiline;
        this.maxLength = maxLength;
        tex = new LoadedTexture(capi);
        caretTex = new LoadedTexture(capi);
        GuiTextureTracker.Register(nameof(ArcanumTextInput));
        GenCaretTexture();
    }

    private void GenCaretTexture()
    {
        using var surf = new ImageSurface(Format.Argb32, 2, 10);
        using var ctx = new Context(surf);
        ctx.SetSourceRGBA(1, 1, 1, 1);
        ctx.Paint();
        generateTexture(surf, ref caretTex);
    }

    /// <inheritdoc />
    public override bool Focusable => true;

    /// <summary>Current text.</summary>
    public string GetText() => text;

    /// <summary>Set text without firing the change callback.</summary>
    public void SetValue(string newText)
    {
        text = newText ?? "";
        caret = Math.Min(caret, text.Length);
        selStart = selEnd = caret;
        cacheKey = null;
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <inheritdoc />
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!Bounds.PointInside(args.X, args.Y)) return;
        args.Handled = true;
        // Approximate caret from click x (monospace-ish guess).
        double relX = args.X - Bounds.absX - scaled(6);
        caret = CharIndexAtX(relX);
        ClearSelection();
        blinkAccum = 0;
    }

    private int CharIndexAtX(double relX)
    {
        if (text.Length == 0) return 0;
        using var surf = new ImageSurface(Format.Argb32, 1, 1);
        using var ctx = new Context(surf);
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(scaled(11));
        int best = text.Length;
        for (int i = 0; i <= text.Length; i++)
        {
            if (ctx.TextExtents(text[..i]).Width > relX) { best = i; break; }
        }
        return Math.Clamp(best, 0, text.Length);
    }

    /// <summary>True when a non-empty selection range is active.</summary>
    private bool HasSelection => selEnd > selStart;

    /// <summary>Collapses the selection to the caret position.</summary>
    private void ClearSelection() { selStart = selEnd = caret; }

    /// <summary>Removes the selected range, leaving the caret at its start.</summary>
    /// <returns>True when a selection was removed.</returns>
    private bool DeleteSelection()
    {
        if (!HasSelection) return false;
        text = text.Remove(selStart, selEnd - selStart);
        caret = selStart;
        selEnd = selStart;
        return true;
    }

    /// <inheritdoc />
    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (!HasFocus) { base.OnKeyPress(api, args); return; }
        if (args.KeyChar > 0 && !args.CtrlPressed && !args.CommandPressed)
        {
            char c = args.KeyChar;
            if (c == '\r' || c == '\n')
            {
                if (!multiline) { args.Handled = true; return; }
                c = '\n';
            }
            DeleteSelection();
            if (maxLength <= 0 || text.Length < maxLength)
            {
                text = text.Insert(caret, c.ToString());
                caret++;
                selStart = selEnd = caret;
                cacheKey = null;
                onChanged?.Invoke(text);
            }
            args.Handled = true;
            return;
        }
        base.OnKeyPress(api, args);
    }

    /// <inheritdoc />
    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!HasFocus) { base.OnKeyDown(api, args); return; }
        switch ((GlKeys)args.KeyCode)
        {
            case GlKeys.BackSpace:
                if (DeleteSelection()) { cacheKey = null; onChanged?.Invoke(text); }
                else if (caret > 0) { text = text.Remove(caret - 1, 1); caret--; cacheKey = null; onChanged?.Invoke(text); }
                break;
            case GlKeys.Delete:
                if (DeleteSelection()) { cacheKey = null; onChanged?.Invoke(text); }
                else if (caret < text.Length) { text = text.Remove(caret, 1); cacheKey = null; onChanged?.Invoke(text); }
                break;
            case GlKeys.Left:
                caret = HasSelection ? selStart : Math.Max(0, caret - 1);
                ClearSelection(); blinkAccum = 0; break;
            case GlKeys.Right:
                caret = HasSelection ? selEnd : Math.Min(text.Length, caret + 1);
                ClearSelection(); blinkAccum = 0; break;
            case GlKeys.Up when multiline:
                caret = MoveCaretLine(-1); ClearSelection(); blinkAccum = 0; break;
            case GlKeys.Down when multiline:
                caret = MoveCaretLine(1); ClearSelection(); blinkAccum = 0; break;
            case GlKeys.Home:
                caret = 0; ClearSelection(); blinkAccum = 0; break;
            case GlKeys.End:
                caret = text.Length; ClearSelection(); blinkAccum = 0; break;
            case GlKeys.A when args.CtrlPressed:
                selStart = 0; selEnd = caret = text.Length; blinkAccum = 0; break;
            case GlKeys.C when args.CtrlPressed:
                if (HasSelection) api.Input.ClipboardText = text[selStart..selEnd];
                break;
            case GlKeys.X when args.CtrlPressed:
                if (HasSelection)
                {
                    api.Input.ClipboardText = text[selStart..selEnd];
                    DeleteSelection();
                    cacheKey = null;
                    onChanged?.Invoke(text);
                }
                break;
            case GlKeys.V when args.CtrlPressed:
                {
                    string clip = api.Input.ClipboardText ?? "";
                    if (clip.Length > 0)
                    {
                        DeleteSelection();
                        int room = maxLength <= 0 ? clip.Length : Math.Max(0, maxLength - text.Length);
                        clip = clip[..Math.Min(clip.Length, room)];
                        text = text.Insert(caret, clip);
                        caret += clip.Length;
                        selStart = selEnd = caret;
                        cacheKey = null;
                        onChanged?.Invoke(text);
                    }
                    break;
                }
            default:
                base.OnKeyDown(api, args);
                return;
        }
        args.Handled = true;
    }

    /// <summary>Move the caret one line up/down, preserving the column.</summary>
    private int MoveCaretLine(int dir)
    {
        int lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1)) + 1;
        int col = caret - lineStart;
        if (dir < 0)
        {
            if (lineStart == 0) return 0;
            int prevStart = text.LastIndexOf('\n', Math.Max(0, lineStart - 2)) + 1;
            int prevLen = lineStart - 1 - prevStart;
            return prevStart + Math.Min(col, prevLen);
        }
        int nextStart = text.IndexOf('\n', caret) + 1;
        if (nextStart <= 0 || nextStart > text.Length) return text.Length;
        int nextEnd = text.IndexOf('\n', nextStart);
        if (nextEnd < 0) nextEnd = text.Length;
        return nextStart + Math.Min(col, nextEnd - nextStart);
    }

    /// <inheritdoc />
    public override void OnFocusLost()
    {
        cacheKey = null;
        base.OnFocusLost();
    }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        blinkAccum += deltaTime;
        bool nowHovered = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        if (nowHovered != hovered) { hovered = nowHovered; cacheKey = null; }

        RegenerateIfNeeded();
        if (tex?.TextureId > 0)
            api.Render.Render2DLoadedTexture(tex, (float)Bounds.absX, (float)Bounds.absY);

        // Blinking caret while focused.
        if (HasFocus && caretTex?.TextureId > 0 && (blinkAccum % 1.0) < 0.55)
        {
            var (cx, cy) = CaretScreenPos();
            api.Render.Render2DTexture(caretTex.TextureId,
                (float)(Bounds.absX + cx), (float)(Bounds.absY + cy),
                (float)scaled(2), (float)scaled(11), 60,
                new Vec4f(0.91f, 0.78f, 0.49f, 0.95f));
        }
    }

    /// <summary>Caret position in element-local px (approx via text extents).</summary>
    private (double cx, double cy) CaretScreenPos()
    {
        using var surf = new ImageSurface(Format.Argb32, 1, 1);
        using var ctx = new Context(surf);
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(scaled(11));
        double lineH = scaled(14);
        if (multiline)
        {
            string before = text[..caret];
            int line = 0; int li = before.LastIndexOf('\n');
            if (li >= 0) { line = before.Count(ch => ch == '\n'); before = before[(li + 1)..]; }
            return (scaled(6) + ctx.TextExtents(before).Width, scaled(5) + line * lineH);
        }
        return (scaled(6) + ctx.TextExtents(text[..caret]).Width,
                Bounds.OuterHeight / 2 - scaled(6));
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;
        string key = $"{text}|{hovered}|{HasFocus}|{selStart}:{selEnd}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
        if (string.Equals(cacheKey, key, StringComparison.Ordinal) && tex.TextureId > 0) return;
        cacheKey = key;

        int w = Math.Max(1, (int)Bounds.OuterWidth);
        int h = Math.Max(1, (int)Bounds.OuterHeight);
        ImageSurface? surface = null; Context? ctx = null;
        try
        {
            surface = new ImageSurface(Format.Argb32, w, h);
            ctx = new Context(surface);
            var p = ArcanumGuiTheme.Palette;
            double r = scaled(ArcanumGuiTheme.Radius.Small);

            // Recessed well — darker than cards, brass rim.
            ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, 0, 0, w, h, r,
                p.SurfaceDeepest, p.SurfaceBase);
            var rim = HasFocus ? p.Accent.WithAlpha(0.75)
                     : hovered ? p.BorderStrong : p.BorderDefault;
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0.5, 0.5, w - 1, h - 1, r, rim, scaled(1.0));

            // Text — clip to the field.
            ctx.Save();
            ArcanumGuiTheme.RoundedRectPath(ctx, 1, 1, w - 2, h - 2, r - 1);
            ctx.Clip();
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            ctx.SetFontSize(scaled(11));
            double lineH = scaled(14);
            DrawSelectionHighlight(ctx, h, lineH);
            ctx.SetSourceRGBA(p.TextPrimary.R, p.TextPrimary.G, p.TextPrimary.B, 0.95);
            if (multiline)
            {
                var lines = text.Split('\n');
                double ty = scaled(5) + scaled(11);
                foreach (var line in lines)
                {
                    ctx.MoveTo(scaled(6), ty);
                    ctx.ShowText(line);
                    ty += lineH;
                    if (ty > h) break;
                }
            }
            else
            {
                ctx.MoveTo(scaled(6), h / 2 + scaled(4));
                ctx.ShowText(text);
            }
            ctx.Restore();

            generateTexture(surface, ref tex);
            GuiTextureTracker.Regen(nameof(ArcanumTextInput));
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose(); tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumTextInput] Texture generation failed: {0}", ex);
        }
        finally { ctx?.Dispose(); surface?.Dispose(); }
    }

    /// <summary>Paints a translucent accent band behind the selected text range.
    /// Call after the font is set and the field clip is active; extents are measured
    /// the same way <see cref="CaretScreenPos" /> does.</summary>
    private void DrawSelectionHighlight(Context ctx, double h, double lineH)
    {
        if (!HasSelection) return;
        var p = ArcanumGuiTheme.Palette;
        ctx.SetSourceRGBA(p.Accent.R, p.Accent.G, p.Accent.B, 0.28);
        if (multiline)
        {
            double lineTop = scaled(5);
            for (int lineStart = 0; lineStart <= text.Length;)
            {
                int nl = text.IndexOf('\n', lineStart);
                int lineEnd = nl < 0 ? text.Length : nl;
                int s0 = Math.Max(selStart, lineStart);
                int s1 = Math.Min(selEnd, lineEnd);
                if (s1 > s0)
                {
                    double sx = scaled(6) + ctx.TextExtents(text[lineStart..s0]).Width;
                    double sw = ctx.TextExtents(text[s0..s1]).Width;
                    ctx.Rectangle(sx, lineTop, sw, lineH);
                }
                if (nl < 0) break;
                lineStart = nl + 1;
                lineTop += lineH;
                if (lineTop > h) break;
            }
        }
        else
        {
            double sx0 = scaled(6) + ctx.TextExtents(text[..selStart]).Width;
            double sx1 = scaled(6) + ctx.TextExtents(text[..selEnd]).Width;
            ctx.Rectangle(sx0, h / 2 - scaled(6), sx1 - sx0, scaled(12));
        }
        ctx.Fill();
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); caretTex?.Dispose(); GuiTextureTracker.Unregister(nameof(ArcanumTextInput)); base.Dispose(); }
}

/// <summary>Composer extensions for <see cref="ArcanumTextInput" />.</summary>
public static class ArcanumTextInputComposer
{
    /// <summary>Adds a themed single-line text input (vanilla AddTextInput shape).</summary>
    public static GuiComposer AddArcanumTextInput(
        this GuiComposer composer, ElementBounds bounds,
        Action<string>? onChanged = null, CairoFont? font = null, string? key = null,
        int maxLength = 0)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(
                new ArcanumTextInput(composer.Api, bounds, onChanged, font, multiline: false, maxLength), key);
        return composer;
    }

    /// <summary>Adds a themed multi-line text area (vanilla AddTextArea shape).</summary>
    public static GuiComposer AddArcanumTextArea(
        this GuiComposer composer, ElementBounds bounds,
        Action<string>? onChanged = null, CairoFont? font = null, string? key = null)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(
                new ArcanumTextInput(composer.Api, bounds, onChanged, font, multiline: true), key);
        return composer;
    }

    /// <summary>Retrieve an input/area added via the helpers above.</summary>
    public static ArcanumTextInput? GetArcanumTextInput(this GuiComposer composer, string key)
        => composer.GetElement(key) as ArcanumTextInput;
}
