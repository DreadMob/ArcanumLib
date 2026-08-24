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

        /// <summary>Performs the draw operation.</summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="cx">The cx value.</param>
        /// <param name="cy">The cy value.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="color">The color value.</param>
        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius, RGBA color)
            => _draw(ctx, cx, cy, radius, color);

        /// <summary>Performs the draw operation.</summary>
        /// <param name="ctx">The ctx value.</param>
        /// <param name="cx">The cx value.</param>
        /// <param name="cy">The cy value.</param>
        /// <param name="radius">The radius.</param>
        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius)
            => _draw(ctx, cx, cy, radius, default);
    }
}
