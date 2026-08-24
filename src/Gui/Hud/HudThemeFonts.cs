using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic font size overrides for a data-driven HUD. Null fields fall back to the default theme during merge.
/// </summary>
[Serializable]
public class HudThemeFonts
{
    /// <summary>Label font size.</summary>
    public double? label;
    /// <summary>Value font size.</summary>
    public double? value;
    /// <summary>Timer font size.</summary>
    public double? timer;
    /// <summary>Title font size.</summary>
    public double? title;

    /// <summary>Merges the other font sizes over this instance, returning a new set.</summary>
    /// <param name="other">The font sizes to merge.</param>
    /// <returns>A new <see cref="HudThemeFonts" /> with merged values.</returns>
    public HudThemeFonts Merge(HudThemeFonts other)
    {
        if (other == null) return this;
        return new HudThemeFonts
        {
            label = other.label ?? label,
            value = other.value ?? value,
            timer = other.timer ?? timer,
            title = other.title ?? title
        };
    }
}
