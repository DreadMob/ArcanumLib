using System.Collections.Generic;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Renders a single <see cref="HudElementDefinition" /> type inside a HUD panel.
/// Implementations are registered per element <c>type</c> (e.g. "title", "bar", "icon").
/// Also provides height and minimum-width measuring for layout calculation.
/// </summary>
public interface IHudElementRenderer
{
    /// <summary>The element type(s) this renderer can draw.</summary>
    IReadOnlyList<string> SupportedTypes { get; }

    /// <summary>
    /// Draws the element and returns the total vertical height consumed,
    /// including any spacers or dividers the renderer added.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The draw.</returns>
    double Draw(HudElementRenderArgs args);

    /// <summary>
    /// Measures the vertical height the element will consume when drawn.
    /// Returns 0 if the element would not draw (e.g. empty content).
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The measure height.</returns>
    double MeasureHeight(HudElementMeasureArgs args);

    /// <summary>
    /// Measures the minimum content width required to draw the element without clipping.
    /// Returns 0 if the element would not draw.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The measure min width.</returns>
    double MeasureMinWidth(HudElementMeasureArgs args);
}

