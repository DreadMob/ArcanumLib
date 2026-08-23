using Cairo;

namespace ArcanumLib.Gui.Icons
{
    /// <summary>
    /// Renders a custom Cairo-drawn icon centered at (cx, cy) with the given radius.
    /// Implementations should use the provided Cairo Context directly.
    /// </summary>
    public interface ICustomIconRenderer
    {
        /// <summary>
        /// Draw the icon at the given center coordinates and radius.
        /// </summary>
        /// <param name="ctx">Cairo context to draw with.</param>
        /// <param name="cx">Center X in surface pixels.</param>
        /// <param name="cy">Center Y in surface pixels.</param>
        /// <param name="radius">Radius of the icon area.</param>
        void Draw(Context ctx, double cx, double cy, double radius);
    }
}
