using System;
using Cairo;
using ArcanumLib.Gui.Theme;

namespace ArcanumLib.Gui.Icons
{
    /// <summary>
    /// Simple <see cref="ICustomIconRenderer"/> wrapper around a stateless delegate
    /// that draws an icon with an explicit color. Useful for registering vector
    /// icons without creating a dedicated class.
    /// </summary>
    public sealed class VectorIcon : ICustomIconRenderer
    {
        private readonly Action<Context, double, double, double, RGBA> _draw;

        /// <summary>Create a vector icon backed by the given draw delegate.</summary>
        public VectorIcon(Action<Context, double, double, double, RGBA> draw)
        {
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius, RGBA color)
            => _draw(ctx, cx, cy, radius, color);

        /// <inheritdoc />
        public void Draw(Context ctx, double cx, double cy, double radius)
            => _draw(ctx, cx, cy, radius, default);
    }
}
