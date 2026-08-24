using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic layout spacing for a data-driven HUD. Null fields fall back to the default theme during merge.
/// </summary>
[Serializable]
public class HudThemeLayout
{
    /// <summary>Standard line height for elements (px).</summary>
    public double? lineHeight;
    /// <summary>Title header height (px).</summary>
    public double? headerHeight;
    /// <summary>Primary bar height (px).</summary>
    public double? barHeight;
    /// <summary>Primary bar max width (px).</summary>
    public double? barWidth;
    /// <summary>Icon size for element icons (px).</summary>
    public double? iconSize;
    /// <summary>Gap between icon and text (px).</summary>
    public double? iconGap;
    /// <summary>Gap after the title header (px).</summary>
    public double? headerGap;
    /// <summary>Gap for dividers between element groups (px).</summary>
    public double? dividerGap;
    /// <summary>Gap before the player list divider (px).</summary>
    public double? playerListGap;
    /// <summary>Row height for each player list entry (px).</summary>
    public double? playerRowHeight;
    /// <summary>Height of a modifier element (px).</summary>
    public double? modifierHeight;
    /// <summary>Extra gap below a modifier element (px).</summary>
    public double? modifierGap;
    /// <summary>Height of a tier-label element (px).</summary>
    public double? tierLabelHeight;
    /// <summary>Extra gap below a tier-label element (px).</summary>
    public double? tierLabelGap;
    /// <summary>Line height for title text (px).</summary>
    public double? titleLineHeight;
    /// <summary>Extra gap below title (px).</summary>
    public double? titleGap;
    /// <summary>Line height for fight-timer text (px).</summary>
    public double? timerLineHeight;
    /// <summary>Extra gap below fight-timer (px).</summary>
    public double? timerGap;
    /// <summary>Row height for a single challenge-list entry (px). Falls back to lineHeight.</summary>
    public double? challengeRowHeight;

    /// <summary>Factory that returns the base layout dimensions for a generic HUD.</summary>
    public static HudThemeLayout Default => new()
    {
        lineHeight = 20, headerHeight = 24, barHeight = 12, barWidth = 180,
        iconSize = 14, iconGap = 6, headerGap = 6, dividerGap = 4,
        playerListGap = 8, playerRowHeight = 18,
        modifierHeight = 17, modifierGap = 4,
        tierLabelHeight = 16, tierLabelGap = 4,
        titleLineHeight = 16, titleGap = 4,
        timerLineHeight = 22, timerGap = 2,
        challengeRowHeight = 18
    };

    /// <summary>Merges the other spacing values over this instance, returning a new layout.</summary>
    public HudThemeLayout Merge(HudThemeLayout other)
    {
        if (other == null) return this;
        var d = Default;
        return new HudThemeLayout
        {
            lineHeight = other.lineHeight ?? lineHeight ?? d.lineHeight,
            headerHeight = other.headerHeight ?? headerHeight ?? d.headerHeight,
            barHeight = other.barHeight ?? barHeight ?? d.barHeight,
            barWidth = other.barWidth ?? barWidth ?? d.barWidth,
            iconSize = other.iconSize ?? iconSize ?? d.iconSize,
            iconGap = other.iconGap ?? iconGap ?? d.iconGap,
            headerGap = other.headerGap ?? headerGap ?? d.headerGap,
            dividerGap = other.dividerGap ?? dividerGap ?? d.dividerGap,
            playerListGap = other.playerListGap ?? playerListGap ?? d.playerListGap,
            playerRowHeight = other.playerRowHeight ?? playerRowHeight ?? d.playerRowHeight,
            modifierHeight = other.modifierHeight ?? modifierHeight ?? d.modifierHeight,
            modifierGap = other.modifierGap ?? modifierGap ?? d.modifierGap,
            tierLabelHeight = other.tierLabelHeight ?? tierLabelHeight ?? d.tierLabelHeight,
            tierLabelGap = other.tierLabelGap ?? tierLabelGap ?? d.tierLabelGap,
            titleLineHeight = other.titleLineHeight ?? titleLineHeight ?? d.titleLineHeight,
            titleGap = other.titleGap ?? titleGap ?? d.titleGap,
            timerLineHeight = other.timerLineHeight ?? timerLineHeight ?? d.timerLineHeight,
            timerGap = other.timerGap ?? timerGap ?? d.timerGap,
            challengeRowHeight = other.challengeRowHeight ?? challengeRowHeight ?? d.challengeRowHeight
        };
    }
}
