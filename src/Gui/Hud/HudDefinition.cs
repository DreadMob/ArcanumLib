using System;
using System.Collections.Generic;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic data-driven HUD layout definition.
/// Specific mod definitions can inherit and use their own element type via the generic argument.
/// </summary>
[Serializable]
public class HudDefinition<TElement> where TElement : HudElementDefinition
{
    /// <summary>If false, the HUD is not shown.</summary>
    public bool enabled = true;

    /// <summary>Layout identifier for grouping similar HUD layouts.</summary>
    public string layout = null!;

    /// <summary>Visual style or fallback theme name.</summary>
    public string style = null!;

    /// <summary>Theme name referencing an external theme source. Takes priority over <see cref="style"/>.</summary>
    public string theme = null!;

    /// <summary>Override the theme's panel width (in pixels). Null = use theme's panelWidth.</summary>
    public int? panelWidth;

    /// <summary>Maximum panel width in pixels.</summary>
    public int? maxPanelWidth;

    /// <summary>If true, show an optional player board below the elements.</summary>
    public bool showPlayerBoard;

    /// <summary>Player board placement: "bottom" (default) or "right".</summary>
    public string playerBoardPosition = "bottom";

    /// <summary>Ordered list of HUD elements to render.</summary>
    public List<TElement> elements = null!;

    /// <summary>Resolves the effective theme name: <see cref="theme"/> → <see cref="style"/> → "default".</summary>
    public string EffectiveTheme => !string.IsNullOrWhiteSpace(theme) ? theme
        : !string.IsNullOrWhiteSpace(style) ? style
        : "default";
}
