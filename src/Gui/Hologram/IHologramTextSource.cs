using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Hologram;

/// <summary>
/// Describes a block entity (or other world object) that provides hologram text.
/// Implement this on any block entity that should render floating label text above it.
/// </summary>
public interface IHologramTextSource
{
    /// <summary>The block position the hologram floats above.</summary>
    BlockPos Position { get; }

    /// <summary>Returns the multi-line text to display, or null to hide.</summary>
    string? GetHologramText();

    /// <summary>Returns the RGBA text color (0-1 range), or null for the renderer default.</summary>
    double[]? GetHologramColor();

    /// <summary>
    /// Returns a value that changes whenever the text or style changes.
    /// The renderer uses this to invalidate cached textures.
    /// </summary>
    long GetHologramVersion();

    /// <summary>Maximum render distance in blocks.</summary>
    float GetHologramRange();

    /// <summary>Vertical offset in blocks above the block position.</summary>
    float GetHologramHeightOffset();

    /// <summary>Whether the hologram should be rendered at all.</summary>
    bool IsHologramVisible();

    /// <summary>Whether the hologram is visible through solid blocks.</summary>
    bool IsHologramVisibleThroughWalls();
}
