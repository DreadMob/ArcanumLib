using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic named HUD theme: frame style, colours, fonts and dimensions.
/// Specific mod themes can inherit or wrap this class and add built-in resolution logic.
/// </summary>
[Serializable]
public class HudTheme
{
    /// <summary>Frame renderer name, e.g. "cartouche", "bone", "glass", "none".</summary>
    public required string frame;

    /// <summary>Corner symbol name, e.g. "ankh", "crossbone", "void", "none".</summary>
    public required string frameSymbol;

    /// <summary>If true, the four corner frame symbols are not drawn (L-shaped corners remain).</summary>
    public bool? hideCornerSymbols;

    /// <summary>Panel width in pixels.</summary>
    public int? panelWidth;

    /// <summary>Maximum panel width in pixels.</summary>
    public int? maxPanelWidth;

    /// <summary>Inner padding in pixels.</summary>
    public double? padding;

    /// <summary>If true, text is drawn with a 1px dark shadow offset.</summary>
    public bool? textShadow;

    /// <summary>Colour palette. Individual fields are null when not specified in JSON.</summary>
    public required HudThemeColors colors;

    /// <summary>Font size overrides. Individual fields are null when not specified.</summary>
    public required HudThemeFonts fonts;

    /// <summary>Layout spacing overrides. Individual fields are null when not specified.</summary>
    public required HudThemeLayout layout;

    /// <summary>Create a copy of this theme with non-null fields from <paramref name="overlay"/> applied.</summary>
    public virtual HudTheme Merge(HudTheme overlay)
    {
        if (overlay == null) return this;
        return new HudTheme
        {
            frame = overlay.frame ?? frame,
            frameSymbol = overlay.frameSymbol ?? frameSymbol,
            hideCornerSymbols = overlay.hideCornerSymbols ?? hideCornerSymbols,
            panelWidth = overlay.panelWidth ?? panelWidth,
            maxPanelWidth = overlay.maxPanelWidth ?? maxPanelWidth,
            padding = overlay.padding ?? padding,
            textShadow = overlay.textShadow ?? textShadow,
            colors = colors?.Merge(overlay.colors) ?? overlay.colors,
            fonts = fonts?.Merge(overlay.fonts) ?? overlay.fonts,
            layout = layout?.Merge(overlay.layout) ?? overlay.layout
        };
    }

    /// <summary>Returns a default generic HUD theme.</summary>
    public static HudTheme CreateDefault()
    {
        return new HudTheme
        {
            frame = "cartouche",
            frameSymbol = "ankh",
            panelWidth = 260,
            maxPanelWidth = 600,
            padding = 10,
            textShadow = false,
            colors = new HudThemeColors
            {
                bgTop = "#2E2419", bgBottom = "#1F1810", bgAlpha = 0.95,
                border = "#A86E3C", borderAlpha = 0.90,
                textPrimary = "#E9DDCE", textSecondary = "#C9B79C",
                accent = "#F0C86A", accentSecondary = "#D49D5A",
                danger = "#CD665C", success = "#6EC86E", pending = "#808080",
                title = "#CD665C",
                barBg = "#1A1208", barFill = "#C58948", barFillLow = "#CD665C",
                parchment = "#E9DDCE"
            },
            fonts = new HudThemeFonts { label = 12, value = 13, timer = 14, title = 14 },
            layout = new HudThemeLayout()
        };
    }
}
