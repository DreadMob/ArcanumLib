using Cairo;
using ArcanumLib.Gui.Theme;

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
        /// The caller is responsible for setting the source color on the Cairo context
        /// before calling this method.
        /// </summary>
        void Draw(Context ctx, double cx, double cy, double radius);

        /// <summary>
        /// Draw the icon at the given center coordinates and radius with an explicit color.
        /// Default implementation forwards to the colorless overload so existing
        /// renderers continue to work without modification.
        /// </summary>
        void Draw(Context ctx, double cx, double cy, double radius, RGBA color)
            => Draw(ctx, cx, cy, radius);
    }

    /// <summary>
    /// Convenience base class for vector icons that always use the explicit color.
    /// Subclasses only need to override <see cref="DrawColored"/>.
    /// The colorless <see cref="Draw(Context, double, double, double)"/> overload
    /// forwards with <c>default(RGBA)</c>.
    /// </summary>
    public abstract class VectorIconBase : ICustomIconRenderer
    {
        /// <summary>Draw the icon using the provided color for strokes and fills.</summary>
        public abstract void DrawColored(Context ctx, double cx, double cy, double radius, RGBA color);

        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius, RGBA color)
            => DrawColored(ctx, cx, cy, radius, color);

        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius)
            => DrawColored(ctx, cx, cy, radius, default);
    }
}
