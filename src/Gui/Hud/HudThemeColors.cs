using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic colour palette for a data-driven HUD. Null fields fall back to the default theme during merge.
/// </summary>
[Serializable]
public class HudThemeColors
{
    /// <summary>Top gradient colour for panel backgrounds.</summary>
    public required string bgTop;
    /// <summary>Bottom gradient colour for panel backgrounds.</summary>
    public required string bgBottom;
    /// <summary>Background alpha multiplier.</summary>
    public double? bgAlpha;
    /// <summary>Border colour.</summary>
    public required string border;
    /// <summary>Border alpha multiplier.</summary>
    public double? borderAlpha;
    /// <summary>Primary text colour.</summary>
    public required string textPrimary;
    /// <summary>Secondary text colour.</summary>
    public required string textSecondary;
    /// <summary>Primary accent colour.</summary>
    public required string accent;
    /// <summary>Secondary accent colour.</summary>
    public required string accentSecondary;
    /// <summary>Danger / failure colour.</summary>
    public required string danger;
    /// <summary>Success / positive colour.</summary>
    public required string success;
    /// <summary>Progress bar background colour.</summary>
    public required string barBg;
    /// <summary>Progress bar fill colour.</summary>
    public required string barFill;
    /// <summary>Progress bar fill colour for low values.</summary>
    public required string barFillLow;
    /// <summary>Parchment / paper colour.</summary>
    public required string parchment;
    /// <summary>Title / heading colour.</summary>
    public required string title;
    /// <summary>Pending / in-progress colour.</summary>
    public required string pending;

    /// <summary>Merges the other colours over this instance, returning a new palette.</summary>
    /// <param name="other">The colours to merge.</param>
    /// <returns>A new <see cref="HudThemeColors" /> with merged values.</returns>
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
