using System;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// A single data-driven element inside a <see cref="HudDefinition{TElement}" />.
/// Contains the common layout and styling fields; specific mod elements can inherit
/// and add their own typed options.
/// </summary>
[Serializable]
public class HudElementDefinition
{
    /// <summary>If false, this element is skipped.</summary>
    public bool enabled = true;

    /// <summary>Element type name (e.g. "title", "bar", "timer", "icon-text").</summary>
    public string? type { get; init; }

    /// <summary>Screen position anchor: "top-center", "top-left", "top-right", "bottom-center".</summary>
    public string position = "top-center";

    /// <summary>Vertical offset in pixels from the anchor position.</summary>
    public int offsetY;

    /// <summary>Horizontal offset in pixels from the anchor position.</summary>
    public int offsetX;

    /// <summary>Format string for dynamic elements (e.g. "{0}/{1}", "{0}:{1:00}").</summary>
    public string? format { get; init; }

    /// <summary>Localization key for static text elements (e.g. title).</summary>
    public string? textKey { get; init; }

    /// <summary>Font size multiplier (1.0 = default).</summary>
    public float fontScale = 1.0f;

    /// <summary>Icon identifier to override the default icon for this type.</summary>
    public string? icon { get; init; }

    /// <summary>Hex color override for this element's text (e.g. "F0C86A"). Empty = use theme default.</summary>
    public string? textColor { get; init; }

    /// <summary>Hex color override for this element's icon (e.g. "D49D5A"). Empty = use theme default.</summary>
    public string? iconColor { get; init; }

    /// <summary>If true, draw a decorative horizontal bar to the right of the text for this element.</summary>
    public bool showBar;

    /// <summary>Alias for <see cref="textColor" />; used if <see cref="textColor" /> is not set.</summary>
    public string? color { get; init; }

    /// <summary>
    /// Conditional visibility: comma-separated conditions.
    /// The exact condition names are up to the consuming mod.
    /// </summary>
    public string? showIf;
}
