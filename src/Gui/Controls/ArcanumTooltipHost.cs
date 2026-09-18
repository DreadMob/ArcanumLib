using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Invisible hover-tracker element: its <see cref="GuiElement.Bounds" /> wrap the target
/// area and, once the cursor has rested inside for <see cref="DelayMs" />, a themed
/// tooltip card is drawn near the cursor (clamped to the window). The element never
/// consumes input — no mouse/wheel handler marks args as handled — so it can be layered
/// on top of any interactive control.
/// </summary>
/// <remarks>
/// Add via <see cref="ArcanumTooltipComposer.AddTooltip" />. The tooltip text supports
/// hard line breaks (<c>\n</c>) and is word-wrapped to <see cref="WrapChars" /> characters
/// per line. The card is baked once into a cached texture (cache-keyed on text + GUI
/// scale, same idiom as <see cref="ArcanumSlider" />), so per-frame cost is a single
/// <c>Render2DLoadedTexture</c> call.
/// </remarks>
public class GuiElementTooltip : GuiElement
{
    /// <summary>Default hover delay before the tooltip appears, in milliseconds.</summary>
    public const double DefaultDelayMs = 500.0;

    /// <summary>Default maximum characters per line before word-wrapping.</summary>
    public const int DefaultWrapChars = 40;

    private string _text;
    private double _delayMs;
    private int _wrapChars;
    private readonly CairoFont _font;

    private float _hoverAccum;

    private LoadedTexture _tex;
    private string? _cacheKey;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">The target hover area (usually a copy of another element's bounds).</param>
    /// <param name="text">Tooltip text. <c>\n</c> inserts a hard line break.</param>
    /// <param name="font">Text font; defaults to a small <see cref="ArcanumGuiTheme.TextPrimary" /> face.</param>
    /// <param name="delayMs">Hover delay before the tooltip shows. Default 500.</param>
    /// <param name="wrapChars">Max characters per line before word-wrap. Default 40.</param>
    public GuiElementTooltip(
        ICoreClientAPI capi,
        ElementBounds bounds,
        string text,
        CairoFont? font = null,
        double delayMs = DefaultDelayMs,
        int wrapChars = DefaultWrapChars)
        : base(capi, bounds)
    {
        _text = text ?? "";
        _font = font ?? ArcanumFont.Colored(ArcanumGuiTheme.TextPrimary, 12.0);
        _delayMs = Math.Max(0.0, delayMs);
        _wrapChars = Math.Max(8, wrapChars);
        _tex = new LoadedTexture(capi);
    }

    /// <summary>Tooltips draw in the last interactive pass so they overlay other controls.</summary>
    public override double DrawOrder => 1.0;

    /// <summary>Current tooltip text.</summary>
    public string Text => _text;

    /// <summary>Hover delay in milliseconds before the tooltip appears.</summary>
    public double DelayMs
    {
        get => _delayMs;
        set => _delayMs = Math.Max(0.0, value);
    }

    /// <summary>Maximum characters per line before word-wrapping kicks in.</summary>
    public int WrapChars
    {
        get => _wrapChars;
        set
        {
            int v = Math.Max(8, value);
            if (_wrapChars != v) { _wrapChars = v; _cacheKey = null; }
        }
    }

    /// <summary>Replaces the tooltip text and invalidates the cached card texture.</summary>
    /// <param name="text">The new text value.</param>
    /// <returns>This tooltip, for fluent chaining.</returns>
    public GuiElementTooltip SetText(string text)
    {
        text ??= "";
        if (!string.Equals(_text, text, StringComparison.Ordinal))
        {
            _text = text;
            _cacheKey = null;
            _hoverAccum = 0f;
        }
        return this;
    }

    /// <summary>Skips static composition; the card is baked on demand during interactive render.</summary>
    /// <param name="ctxStatic">The ctx static value.</param>
    /// <param name="surfaceStatic">The surface static value.</param>
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
    {
        // Tooltip texture is generated lazily on first hover in RenderInteractiveElements.
    }

    /// <summary>Tracks hover time and, once past <see cref="DelayMs" />, draws the tooltip card.</summary>
    /// <param name="deltaTime">The delta time value.</param>
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        if (api?.Input == null || api.Render == null) return;
        if (!Bounds.Initialized) Bounds.CalcWorldBounds();

        int mx = api.Input.MouseX;
        int my = api.Input.MouseY;

        bool inside = false;
        try
        {
            inside = Bounds.PointInside(mx, my);
            // Respect clip regions (e.g. trackers on elements inside a GuiElementClip container).
            if (inside && InsideClipBounds is { Initialized: true } clip)
            {
                inside = clip.PointInside(mx, my);
            }
        }
        catch (Exception ex)
        {
            api.Logger?.Warning("[ArcanumTooltip] Hover test failed: {0}", ex);
        }

        if (!inside || string.IsNullOrWhiteSpace(_text))
        {
            _hoverAccum = 0f;
            return;
        }

        _hoverAccum += deltaTime;
        if (_hoverAccum * 1000.0f < (float)_delayMs) return;

        RegenerateIfNeeded();
        if (_tex == null || _tex.TextureId <= 0) return;

        // Place below-right of the cursor, then clamp the whole card into the window.
        double margin = scaled(4);
        double x = mx + scaled(14);
        double y = my + scaled(16);
        double fw = api.Render.FrameWidth;
        double fh = api.Render.FrameHeight;

        if (fw > 0) x = Math.Min(x, fw - margin - _tex.Width);
        if (fh > 0) y = Math.Min(y, fh - margin - _tex.Height);
        x = Math.Max(margin, x);
        y = Math.Max(margin, y);

        // The tracker may sit inside a clip region (e.g. a clipped row container);
        // the tooltip must escape the scissor or it would be clipped away.
        bool scissorWasEnabled = api.Render.ScissorStack.Count > 0;
        if (scissorWasEnabled) api.Render.GlScissorFlag(false);
        try
        {
            api.Render.Render2DLoadedTexture(_tex, (float)x, (float)y);
        }
        finally
        {
            if (scissorWasEnabled) api.Render.GlScissorFlag(true);
        }
    }

    private void RegenerateIfNeeded()
    {
        if (api == null) return;
        _tex ??= new LoadedTexture(api);

        string key = $"{_text}|{_wrapChars}|{scaled(1.0):F3}";
        if (string.Equals(_cacheKey, key, StringComparison.Ordinal) && _tex.TextureId > 0) return;
        _cacheKey = key;

        var lines = WrapLines(_text, _wrapChars);
        if (lines.Count == 0)
        {
            _tex.Dispose();
            _tex = new LoadedTexture(api);
            return;
        }

        ImageSurface? measureSurface = null;
        Context? measureCtx = null;
        ImageSurface? surface = null;
        Context? ctx = null;

        try
        {
            double padX = scaled(10.0);
            double padY = scaled(6.0);
            double lineGap = scaled(2.0);

            // Measure pass: font extents + widest line decide the card size.
            measureSurface = new ImageSurface(Format.Argb32, 4, 4);
            measureCtx = new Context(measureSurface);
            _font.SetupContext(measureCtx);
            var fe = measureCtx.FontExtents;
            double lineH = Math.Max(fe.Height, scaled(13.0));
            double maxW = 1.0;
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                maxW = Math.Max(maxW, measureCtx.TextExtents(line).Width);
            }

            int w = Math.Max(4, (int)Math.Ceiling(maxW + padX * 2.0 + 1.0));
            int h = Math.Max(4, (int)Math.Ceiling(lineH * lines.Count + lineGap * (lines.Count - 1) + padY * 2.0 + 1.0));

            _tex.Dispose();
            _tex = new LoadedTexture(api);

            surface = new ImageSurface(Format.Argb32, w, h);
            ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            // Card: elevated surface fill, brass border, top inner highlight.
            double r = scaled(ArcanumGuiTheme.Radius.Medium);
            ArcanumGuiTheme.FillRoundedRect(ctx, 0, 0, w, h, r, ArcanumGuiTheme.SurfaceCard.WithAlpha(0.97));
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0.5, 0.5, w - 1, h - 1, r, ArcanumGuiTheme.Accent.WithAlpha(0.9), scaled(1.0));
            ArcanumGuiTheme.DrawInnerHighlight(ctx, 1, 1, w - 2, h - 2, Math.Max(1.0, r - 1), 0.08);

            // Text lines.
            _font.SetupContext(ctx);
            ArcanumGuiTheme.TextPrimary.Apply(ctx);
            double baseline = padY + fe.Ascent;
            foreach (var line in lines)
            {
                if (line.Length > 0)
                {
                    ctx.MoveTo(padX, baseline);
                    ctx.ShowText(line);
                }
                baseline += lineH + lineGap;
            }

            generateTexture(surface, ref _tex);
        }
        catch (Exception ex)
        {
            _cacheKey = null;
            _tex?.Dispose();
            _tex = new LoadedTexture(api);
            api.Logger?.Warning("[ArcanumTooltip] Texture generation failed: {0}", ex);
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
            measureCtx?.Dispose();
            measureSurface?.Dispose();
        }
    }

    /// <summary>
    /// Splits <paramref name="text" /> on hard newlines, then greedily word-wraps each
    /// segment to <paramref name="maxChars" /> characters. Overlong single words are
    /// hard-split; blank lines are preserved as empty strings.
    /// </summary>
    private static List<string> WrapLines(string text, int maxChars)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        foreach (var seg in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string rest = seg.TrimEnd();
            if (rest.Length == 0)
            {
                lines.Add("");
                continue;
            }

            while (rest.Length > maxChars)
            {
                int window = Math.Min(maxChars, rest.Length);
                int cut = rest.LastIndexOf(' ', window - 1, window);
                if (cut <= 0) cut = maxChars;
                lines.Add(rest.Substring(0, cut).TrimEnd());
                rest = rest.Substring(cut).TrimStart();
            }

            if (rest.Length > 0) lines.Add(rest);
        }

        return lines;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _tex?.Dispose();
        base.Dispose();
    }
}

/// <summary>Composer extensions and a small key-based facade for <see cref="GuiElementTooltip" />.</summary>
public static class ArcanumTooltipComposer
{
    /// <summary>
    /// Adds an invisible tooltip tracker over <paramref name="targetBounds" />. Pass
    /// <c>someElement.Bounds.FlatCopy()</c> (or the same bounds object) of the control
    /// the tooltip describes.
    /// </summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="targetBounds">The bounds to watch for hovering.</param>
    /// <param name="text">Tooltip text (<c>\n</c> for line breaks, word-wrapped ~40 chars).</param>
    /// <param name="key">Optional element key for later <see cref="GetTooltip" /> lookups.</param>
    /// <param name="font">Optional font override.</param>
    /// <returns>The composer.</returns>
    public static GuiComposer AddTooltip(
        this GuiComposer composer,
        ElementBounds targetBounds,
        string text,
        string? key = null,
        CairoFont? font = null)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(new GuiElementTooltip(composer.Api, targetBounds, text, font), key);
        }
        return composer;
    }

    /// <summary>
    /// Adds an invisible tooltip tracker over <paramref name="target" />'s bounds.
    /// </summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="target">The element whose bounds should trigger the tooltip.</param>
    /// <param name="text">Tooltip text (<c>\n</c> for line breaks, word-wrapped ~40 chars).</param>
    /// <param name="key">Optional element key for later <see cref="GetTooltip" /> lookups.</param>
    /// <param name="font">Optional font override.</param>
    /// <returns>The composer.</returns>
    public static GuiComposer AddTooltip(
        this GuiComposer composer,
        GuiElement target,
        string text,
        string? key = null,
        CairoFont? font = null)
    {
        return AddTooltip(composer, target.Bounds.FlatCopy(), text, key, font);
    }

    /// <summary>Retrieves a tooltip added via <see cref="AddTooltip(GuiComposer, ElementBounds, string, string?, CairoFont?)" /> by key.</summary>
    /// <param name="composer">The composer value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The tooltip, or null if none is found.</returns>
    public static GuiElementTooltip? GetTooltip(this GuiComposer composer, string key)
        => composer.GetElement(key) as GuiElementTooltip;
}

/// <summary>Key-based facade over per-composer <see cref="GuiElementTooltip" /> instances.</summary>
public static class ArcanumTooltip
{
    /// <summary>Updates the text of a tooltip previously added under <paramref name="key" />.</summary>
    /// <param name="composer">The composer owning the tooltip.</param>
    /// <param name="key">The element key used in <see cref="ArcanumTooltipComposer.AddTooltip" />.</param>
    /// <param name="text">The new tooltip text.</param>
    /// <returns>True if a tooltip with that key exists and was updated.</returns>
    public static bool Set(GuiComposer composer, string key, string text)
    {
        var el = composer.GetElement(key) as GuiElementTooltip;
        if (el == null) return false;
        el.SetText(text);
        return true;
    }
}
