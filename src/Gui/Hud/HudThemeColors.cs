using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic colour palette for a data-driven HUD. Null fields fall back to the default theme during merge.
/// </summary>
[Serializable]
public class HudThemeColors
{
    public required string bgTop;
    public required string bgBottom;
    public double? bgAlpha;
    public required string border;
    public double? borderAlpha;
    public required string textPrimary;
    public required string textSecondary;
    public required string accent;
    public required string accentSecondary;
    public required string danger;
    public required string success;
    public required string barBg;
    public required string barFill;
    public required string barFillLow;
    public required string parchment;
    public required string title;
    public required string pending;

    /// <summary>Merges the other colours over this instance, returning a new palette.</summary>
    public HudThemeColors Merge(HudThemeColors other)
    {
        if (other == null) return this;
        return new HudThemeColors
        {
            bgTop = other.bgTop ?? bgTop,
            bgBottom = other.bgBottom ?? bgBottom,
            bgAlpha = other.bgAlpha ?? bgAlpha,
            border = other.border ?? border,
            borderAlpha = other.borderAlpha ?? borderAlpha,
            textPrimary = other.textPrimary ?? textPrimary,
            textSecondary = other.textSecondary ?? textSecondary,
            accent = other.accent ?? accent,
            accentSecondary = other.accentSecondary ?? accentSecondary,
            danger = other.danger ?? danger,
            success = other.success ?? success,
            barBg = other.barBg ?? barBg,
            barFill = other.barFill ?? barFill,
            barFillLow = other.barFillLow ?? barFillLow,
            parchment = other.parchment ?? parchment,
            title = other.title ?? title,
            pending = other.pending ?? pending
        };
    }
}
