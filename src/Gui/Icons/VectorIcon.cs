using System;
using Cairo;
using ArcanumLib.Gui.Theme;

namespace ArcanumLib.Gui.Icons
{
    /// <summary>
    /// Simple <see cref="ICustomIconRenderer" /> wrapper around a stateless delegate
    /// that draws an icon with an explicit color. Useful for registering vector
    /// icons without creating a dedicated class.
    /// </summary>
    public sealed class VectorIcon : ICustomIconRenderer
    {
        private readonly Action<Context, double, double, double, RGBA> _draw;

        /// <summary>Create a vector icon backed by the given draw delegate.</summary>
        /// <param name="draw">The callback to invoke.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draw" /> is <see langword="null" />.</exception>
        public VectorIcon(Action<Context, double, double, double, RGBA> draw)
        {
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        /// <summary>Draws the icon with an explicit color at the given center and radius.</summary>
        /// <param name="ctx">The Cairo context to draw with.</param>
        /// <param name="cx">Center X in pixels.</param>
        /// <param name="cy">Center Y in pixels.</param>
        /// <param name="radius">Icon radius in pixels.</param>
        /// <param name="color">Color to draw with.</param>
        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius, RGBA color)
            => _draw(ctx, cx, cy, radius, color);

        /// <summary>Draws the icon with the default color at the given center and radius.</summary>
        /// <param name="ctx">The Cairo context to draw with.</param>
        /// <param name="cx">Center X in pixels.</param>
        /// <param name="cy">Center Y in pixels.</param>
        /// <param name="radius">Icon radius in pixels.</param>
        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius)
            => _draw(ctx, cx, cy, radius, default);
    }
}
