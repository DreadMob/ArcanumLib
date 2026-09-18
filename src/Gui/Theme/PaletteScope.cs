using System;
using System.Collections.Generic;

namespace ArcanumLib.Gui.Theme
{
    /// <summary>
    /// RAII scope that temporarily replaces <see cref="ArcanumGuiTheme.Palette" /> and
    /// restores the previous palette when disposed. Enables per-dialog or per-control
    /// theme overrides without permanently mutating the global palette:
    /// <code>
    /// using (ArcanumGuiTheme.WithPalette(myPalette))
    /// {
    ///     composer.Compose();
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// GUI code is main-thread affine, so the backing store is a simple static stack
    /// with no locking. Scopes should be disposed in LIFO order; out-of-order disposal
    /// still restores a consistent palette but may skip intermediate pushes.
    /// </remarks>
    public sealed class PaletteScope : IDisposable
    {
        private static readonly Stack<GuiThemePalette> _previous = new();
        private bool _disposed;

        /// <summary>
        /// Pushes the current palette and installs <paramref name="palette" /> as
        /// <see cref="ArcanumGuiTheme.Palette" /> until <see cref="Dispose" /> is called.
        /// </summary>
        /// <param name="palette">The palette to make active.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="palette" /> is <see langword="null" />.</exception>
        public PaletteScope(GuiThemePalette palette)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            _previous.Push(ArcanumGuiTheme.Palette);
            ArcanumGuiTheme.Palette = palette;
        }

        /// <summary>The number of currently active (undisposed) palette scopes.</summary>
        public static int Depth => _previous.Count;

        /// <summary>
        /// Restores the palette that was active when this scope was created.
        /// Safe to call multiple times; subsequent calls are no-ops. When the scope
        /// stack is already empty the default <see cref="GuiThemePalette.Vanilla" />
        /// palette is restored.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ArcanumGuiTheme.Palette = _previous.Count > 0 ? _previous.Pop() : GuiThemePalette.Vanilla;
        }
    }
}
