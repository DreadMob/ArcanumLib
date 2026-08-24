using Cairo;

namespace ArcanumLib.Gui.Hologram;

/// <summary>
/// Custom renderer for a single line of a hologram texture.
/// Return <c>true</c> to prevent the default centered text draw for this line.
/// </summary>
public interface IHologramLineRenderer
{
    /// <summary>
    /// Draws one line of text.
    /// </summary>
    /// <param name="ctx">Cairo context for the texture.</param>
    /// <param name="lineIndex">Zero-based index of the line.</param>
    /// <param name="line">Trimmed text of the line.</param>
    /// <param name="x">Suggested drawing position or texture width (consumer-defined).</param>
    /// <param name="y">Baseline Y position for the line.</param>
    /// <param name="lineHeight">Height of one line in pixels.</param>
    /// <returns><c>true</c> when the line has been fully handled.</returns>
    bool RenderLine(Context ctx, int lineIndex, string line, double x, double y, double lineHeight);
}
