using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic font size overrides for a data-driven HUD. Null fields fall back to the default theme during merge.
/// </summary>
[Serializable]
public class HudThemeFonts
{
    public double? label;
    public double? value;
    public double? timer;
    public double? title;

    /// <summary>Merges the other font sizes over this instance, returning a new set.</summary>
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
