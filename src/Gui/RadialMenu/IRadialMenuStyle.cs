using Cairo;

namespace ArcanumLib.Gui.RadialMenu;

/// <summary>
/// Defines the visual appearance of a radial menu — sector backgrounds, borders,
/// center cancel button, and icon tint. Implementations are registered with
/// <see cref="RadialMenuStyleRegistry" /> and selected by string key.
/// </summary>
public interface IRadialMenuStyle
{
    /// <summary>Unique key for this style, e.g. <c>"default"</c>, <c>"mystic"</c>.</summary>
    string Key { get; }

    /// <summary>
    /// Draws a single sector wedge of the radial menu.
    /// </summary>
    /// <param name="ctx">Cairo context, already scaled to GUI scale.</param>
    /// <param name="cx">Center X of the radial circle.</param>
    /// <param name="cy">Center Y of the radial circle.</param>
    /// <param name="a0">Start angle of the sector (radians).</param>
    /// <param name="a1">End angle of the sector (radians).</param>
    /// <param name="hovered">Whether the mouse is currently over this sector.</param>
    /// <param name="isActive">Whether the sector is in a toggled/active state.</param>
    /// <param name="disabled">Whether the sector is greyed out (e.g. on cooldown).</param>
    /// <param name="outerRadius">Outer radius of the radial circle.</param>
    /// <param name="innerRadius">Inner radius (center button area).</param>
    void DrawSector(Context ctx, float cx, float cy, float a0, float a1,
        bool hovered, bool isActive, bool disabled,
        float outerRadius, float innerRadius);

    /// <summary>
    /// Draws the center cancel button inside the inner radius.
    /// </summary>
    /// <param name="ctx">Cairo context, already scaled to GUI scale.</param>
    /// <param name="cx">Center X of the radial circle.</param>
    /// <param name="cy">Center Y of the radial circle.</param>
    /// <param name="innerRadius">Inner radius of the center button area.</param>
    void DrawCenterButton(Context ctx, float cx, float cy, float innerRadius);

    /// <summary>
    /// Returns the RGBA tint to use for sector icons.
    /// </summary>
    /// <param name="disabled">Whether the icon is disabled (greyed out).</param>
    /// <returns>A tuple of (red, green, blue, alpha) in 0..1 range.</returns>
    (float r, float g, float b, float a) GetIconColor(bool disabled);
}
