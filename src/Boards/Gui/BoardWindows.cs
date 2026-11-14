using System;
using ArcanumLib.Gui.Theme;

namespace ArcanumLib.Boards.Gui
{
    /// <summary>
    /// Look of the board windows: a mod gives its boards its own palette (client side), the rest use the default one.
    /// </summary>
    public static class BoardWindows
    {
        /// <summary>Palette of a board by its key; null = the default palette.</summary>
        public static Func<string, GuiThemePalette?>? PaletteFor { get; set; }

        /// <summary>The board's palette for one compose or render call; null when the board has none.</summary>
        internal static IDisposable? Scope(string boardKey) =>
            PaletteFor?.Invoke(boardKey) is { } palette ? ArcanumGuiTheme.WithPalette(palette) : null;
    }
}
