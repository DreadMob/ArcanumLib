using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic colour palette for a data-driven HUD. Null fields fall back to the default theme during merge.
/// </summary>
[Serializable]
public class HudThemeColors
{
    public string bgTop = null!;
    public string bgBottom = null!;
    public double? bgAlpha;
    public string border = null!;
    public double? borderAlpha;
    public string textPrimary = null!;
    public string textSecondary = null!;
    public string accent = null!;
    public string accentSecondary = null!;
    public string danger = null!;
    public string success = null!;
    public string barBg = null!;
    public string barFill = null!;
    public string barFillLow = null!;
    public string parchment = null!;
    public string bossName = null!;
    public string challengePassed = null!;
    public string challengePending = null!;

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
