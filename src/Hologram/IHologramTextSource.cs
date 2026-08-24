using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Hologram;

/// <summary>
/// Describes a block entity (or other world object) that provides hologram text.
/// Implement this on any block entity that should render floating label text above it.
/// </summary>
public interface IHologramTextSource
{
    /// <summary>The block position the hologram floats above.</summary>
    BlockPos Position { get; }

    /// <summary>Returns the multi-line text to display, or null to hide.</summary>
    /// <returns>The hologram text, or null if none is found.</returns>
    string? GetHologramText();

    /// <summary>Returns the RGBA text color (0-1 range), or null for the renderer default.</summary>
    /// <returns>A collection of hologram color values, or null if none is found.</returns>
    double[]? GetHologramColor();

    /// <summary>
    /// Returns a value that changes whenever the text or style changes.
    /// The renderer uses this to invalidate cached textures.
    /// </summary>
    /// <returns>The hologram version.</returns>
    long GetHologramVersion();

    /// <summary>Maximum render distance in blocks.</summary>
    /// <returns>The hologram range.</returns>
    float GetHologramRange();

    /// <summary>Vertical offset in blocks above the block position.</summary>
    /// <returns>The hologram height offset.</returns>
    float GetHologramHeightOffset();

    /// <summary>Whether the hologram should be rendered at all.</summary>
    /// <returns>true if hologram visible; otherwise, false.</returns>
    bool IsHologramVisible();

    /// <summary>Whether the hologram is visible through solid blocks.</summary>
    /// <returns>true if hologram visible through walls; otherwise, false.</returns>
    bool IsHologramVisibleThroughWalls();
}
