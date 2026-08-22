using System;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Visual style of an <see cref="ArcanumButton"/>.
/// </summary>
public enum ArcanumButtonStyle
{
    /// <summary>Neutral surface, typically used for dismiss / close actions.</summary>
    Default,

    /// <summary>Accent fill, typically used for accept / confirm actions.</summary>
    Primary,

    /// <summary>Ghost (no fill), typically used for tertiary actions.</summary>
    Subtle,

    /// <summary>Red tone, typically used for destructive actions.</summary>
    Danger
}

/// <summary>
/// Themed button for the Arcanum GUI toolkit. Manages its own hover / pressed state
/// and regenerates a Cairo surface lazily. Use the <c>AddArcanumButton</c> composer
/// extensions.
/// </summary>
public class ArcanumButton : GuiElement
{
    private readonly string text;
    private readonly Func<bool> onClick;
    private readonly ArcanumButtonStyle style;
    private readonly bool customStyle;
    private readonly CairoFont? customFont;
    private readonly int customBgColor;

    private bool hovered;
    private bool pressed;
    private bool enabled = true;
    private LoadedTexture cachedTexture;
    private string? cacheKey;

    /// <summary>
    /// Gets or sets whether the button accepts input. Disabled buttons are rendered
    /// with reduced opacity.
    /// </summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value) return;
            enabled = value;
            cacheKey = null;
        }
    }

    /// <summary>
    /// Creates a themed button with one of the standard <see cref="ArcanumButtonStyle"/> styles.
    /// </summary>
    public ArcanumButton(ICoreClientAPI capi, ElementBounds bounds, string text, Func<bool> onClick, ArcanumButtonStyle style)
        : base(capi, bounds)
    {
        this.text = text ?? "";
        this.onClick = onClick;
        this.style = style;
        this.customStyle = false;
        cachedTexture = new LoadedTexture(capi);
    }

    /// <summary>
    /// Creates a button with a custom background color and font for consumers that
    /// need their own visual treatment. The <paramref name="onClick"/> callback is
    /// an <see cref="Action"/> because the result is not used.
    /// </summary>
    public ArcanumButton(ICoreClientAPI capi, ElementBounds bounds, Action? onClick, string text, CairoFont font, int bgColor)
        : base(capi, bounds)
    {
        this.text = text ?? "";
        this.onClick = () => { onClick?.Invoke(); return true; };
        this.style = ArcanumButtonStyle.Default;
        this.customStyle = true;
        this.customFont = font;
        this.customBgColor = bgColor;
        cachedTexture = new LoadedTexture(capi);
    }

    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
    {
        // Don't regenerate during composition - the rendering context may not be ready.
        // The texture will be generated on first render instead.
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;

        bool nowHovered = false;
        try
        {
            nowHovered = enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumButton] Hover detection failed: {0}", ex);
        }

        if (nowHovered != hovered)
        {
            bool wasHovered = hovered;
            hovered = nowHovered;
            cacheKey = null;
            if (hovered && !wasHovered && enabled)
            {
                try
                {
                    api?.Gui?.PlaySound("menubutton", false, 0.12f);
                }
                catch (Exception ex)
                {
                    api?.Logger?.Warning("[ArcanumButton] Hover sound playback failed: {0}", ex);
                }
            }
        }

        RegenerateIfNeeded();

        if (cachedTexture?.TextureId > 0)
        {
            api?.Render?.Render2DLoadedTexture(cachedTexture, (float)Bounds.absX, (float)Bounds.absY);
        }
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null) return;
        if (api?.Render == null) return;
        if (cachedTexture == null) cachedTexture = new LoadedTexture(api!);

        string newKey = $"{enabled}|{hovered}|{pressed}|{(int)Bounds.OuterWidth}|{(int)Bounds.OuterHeight}";
        if (string.Equals(cacheKey, newKey, StringComparison.Ordinal) && cachedTexture?.TextureId > 0) return;

        cacheKey = newKey;

        int width = Math.Max(1, (int)Bounds.OuterWidth);
        int height = Math.Max(1, (int)Bounds.OuterHeight);

        ImageSurface? surface = null;
        Context? ctx = null;

        try
        {
            surface = new ImageSurface(Format.Argb32, width, height);
            ctx = new Context(surface);

            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            double r = scaled(ArcanumGuiTheme.Radius.Medium);
            (RGBA fill, RGBA border, RGBA textColor) = GetStyleColors();

            // Body fill. Hover is conveyed by the brighter fill and border.
            ArcanumGuiTheme.FillRoundedRect(ctx, 0, 0, width, height, r, fill);

            // Border.
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0, 0, width, height, r, border, scaled(1.0));

            // Top inner highlight.
            ArcanumGuiTheme.DrawInnerHighlight(ctx, 1, 1, width - 2, height - 2, r - 1, 0.08);

            // Label.
            ctx.SetSourceRGBA(textColor.R, textColor.G, textColor.B, enabled ? 1.0 : 0.45);
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
            ctx.SetFontSize(scaled(14.5));
            var ext = ctx.TextExtents(text);
            ctx.MoveTo((width - ext.Width) / 2.0 - ext.XBearing, (height - ext.Height) / 2.0 - ext.YBearing);
            ctx.ShowText(text);

            generateTexture(surface, ref cachedTexture);
        }
        catch (Exception ex)
        {
            cacheKey = null;
            cachedTexture?.Dispose();
            cachedTexture = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumButton] Texture generation failed: {0}", ex);
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
        }
    }

    private (RGBA fill, RGBA border, RGBA textColor) GetStyleColors()
    {
        if (customStyle)
        {
            var fill = RGBA.FromArgb(customBgColor);
            if (hovered)
            {
                fill = fill.Lerp(ArcanumGuiTheme.Accent, 0.25);
            }
            return (fill, fill.WithAlpha(0.85), ArcanumGuiTheme.TextPrimary);
        }

        const float disabledAlpha = 0.35f;
        switch (style)
        {
            case ArcanumButtonStyle.Primary:
                {
                    var fill = hovered
                        ? ArcanumGuiTheme.Accent.WithAlpha(0.95)
                        : ArcanumGuiTheme.AccentDim.Lerp(ArcanumGuiTheme.Accent, 0.45);
                    var border = ArcanumGuiTheme.Accent.WithAlpha(hovered ? 0.95 : 0.65);
                    if (!enabled)
                    {
                        fill = fill.WithAlpha(disabledAlpha);
                        border = border.WithAlpha(disabledAlpha);
                    }
                    return (fill, border, ArcanumGuiTheme.TextPrimary);
                }
            case ArcanumButtonStyle.Danger:
                {
                    var fill = hovered
                        ? ArcanumGuiTheme.StatusFailed.WithAlpha(0.85)
                        : ArcanumGuiTheme.StatusFailed.WithAlpha(0.35);
                    var border = ArcanumGuiTheme.StatusFailed.WithAlpha(hovered ? 0.95 : 0.55);
                    if (!enabled)
                    {
                        fill = fill.WithAlpha(disabledAlpha);
                        border = border.WithAlpha(disabledAlpha);
                    }
                    return (fill, border, ArcanumGuiTheme.TextPrimary);
                }
            case ArcanumButtonStyle.Subtle:
                {
                    var fill = ArcanumGuiTheme.SurfaceCard.WithAlpha(hovered ? 0.85 : 0.0);
                    var border = ArcanumGuiTheme.BorderSubtle.WithAlpha(hovered ? 0.65 : 0.4);
                    if (!enabled)
                    {
                        fill = fill.WithAlpha(0.15);
                        border = border.WithAlpha(0.2);
                    }
                    return (fill, border, ArcanumGuiTheme.TextSecondary);
                }
            default:
                {
                    var fill = hovered ? ArcanumGuiTheme.SurfaceCardHover : ArcanumGuiTheme.SurfaceCard;
                    var border = hovered ? ArcanumGuiTheme.BorderStrong : ArcanumGuiTheme.BorderDefault;
                    if (!enabled)
                    {
                        fill = fill.WithAlpha(disabledAlpha);
                        border = border.WithAlpha(disabledAlpha);
                    }
                    return (fill, border, ArcanumGuiTheme.TextPrimary);
                }
        }
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled) return;
        if (!Bounds.PointInside(args.X, args.Y)) return;

        pressed = true;
        cacheKey = null;
        args.Handled = true;
    }

    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled) return;
        bool wasPressed = pressed;
        pressed = false;
        cacheKey = null;

        if (!wasPressed) return;
        if (!Bounds.PointInside(args.X, args.Y)) return;

        try
        {
            if (onClick?.Invoke() == true)
            {
                api.Gui.PlaySound("menubutton_press", false, 0.25f);
            }
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumButton] Click handler failed: {0}", ex);
        }
    }

    public override void Dispose()
    {
        cachedTexture?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Composer extension methods for adding <see cref="ArcanumButton"/> elements.
/// </summary>
public static class ArcanumButtonComposerHelpers
{
    /// <summary>
    /// Adds a standard themed button to the composer.
    /// </summary>
    public static GuiComposer AddArcanumButton(
        this GuiComposer composer,
        string text,
        Func<bool> onClick,
        ElementBounds bounds,
        ArcanumButtonStyle style = ArcanumButtonStyle.Default,
        string? key = null)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(new ArcanumButton(composer.Api, bounds, text, onClick, style), key);
        }
        return composer;
    }

    /// <summary>
    /// Adds a themed button with a custom font and background color to the composer.
    /// </summary>
    public static GuiComposer AddArcanumButton(
        this GuiComposer composer,
        string text,
        Action onClick,
        ElementBounds bounds,
        CairoFont font,
        int bgColor,
        string? key = null)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(new ArcanumButton(composer.Api, bounds, onClick, text, font, bgColor), key);
        }
        return composer;
    }
}
