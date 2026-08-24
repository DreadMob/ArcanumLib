using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic colour palette for a data-driven HUD. Null fields fall back to the default theme during merge.
/// </summary>
[Serializable]
public class HudThemeColors
{
    public string bgTop;
    public string bgBottom;
    public double? bgAlpha;
    public string border;
    public double? borderAlpha;
    public string textPrimary;
    public string textSecondary;
    public string accent;
    public string accentSecondary;
    public string danger;
    public string success;
    public string barBg;
    public string barFill;
    public string barFillLow;
    public string parchment;
    public string bossName;
    public string challengePassed;
    public string challengePending;

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
            bossName = other.bossName ?? bossName,
            challengePassed = other.challengePassed ?? challengePassed,
            challengePending = other.challengePending ?? challengePending
        };
    }
}
