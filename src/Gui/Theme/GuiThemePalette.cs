namespace ArcanumLib.Gui.Theme
{
    /// <summary>
    /// Immutable color palette for the Arcanum GUI toolkit.
    /// Swap <see cref="ArcanumGuiTheme.Palette" /> to retheme every control at once —
    /// buttons, lists, scrollbars, dialog backgrounds all read through it.
    /// </summary>
    public class GuiThemePalette
    {
        // Surfaces
        public RGBA SurfaceDeepest    { get; init; }
        public RGBA SurfaceBase       { get; init; }
        public RGBA SurfaceElevated   { get; init; }
        public RGBA SurfaceCard       { get; init; }
        public RGBA SurfaceCardHover  { get; init; }
        public RGBA SurfaceCardActive { get; init; }

        // Borders / dividers
        public RGBA BorderShadow       { get; init; }
        public RGBA BorderSubtle       { get; init; }
        public RGBA BorderDefault      { get; init; }
        public RGBA BorderStrong       { get; init; }
        public RGBA BorderSilver       { get; init; }
        public RGBA BorderSilverBright { get; init; }

        // Accent
        public RGBA Accent       { get; init; }
        public RGBA AccentSoft   { get; init; }
        public RGBA AccentDim    { get; init; }
        public RGBA AccentBright { get; init; }
        public RGBA Highlight    { get; init; }

        // Status
        public RGBA StatusAvailable { get; init; }
        public RGBA StatusActive    { get; init; }
        public RGBA StatusComplete  { get; init; }
        public RGBA StatusLocked    { get; init; }
        public RGBA StatusCooldown  { get; init; }
        public RGBA StatusFailed    { get; init; }

        // Text
        public RGBA TextPrimary   { get; init; }
        public RGBA TextSecondary { get; init; }
        public RGBA TextMuted     { get; init; }
        public RGBA TextDisabled  { get; init; }

        /// <summary>The classic vanilla-brown palette (the previous hardcoded look).</summary>
        public static readonly GuiThemePalette Vanilla = new()
        {
            SurfaceDeepest    = RGBA.From(0x1F, 0x18, 0x10, 0.96),
            SurfaceBase       = RGBA.From(0x2E, 0x24, 0x19, 0.95),
            SurfaceElevated   = RGBA.From(0x40, 0x35, 0x29, 0.98),
            SurfaceCard       = RGBA.From(0x4A, 0x3C, 0x2C, 0.92),
            SurfaceCardHover  = RGBA.From(0x5A, 0x47, 0x32, 0.96),
            SurfaceCardActive = RGBA.From(0x6E, 0x57, 0x3C, 0.98),

            BorderShadow       = RGBA.From(0x12, 0x0C, 0x07, 0.65),
            BorderSubtle       = RGBA.From(0xE9, 0xDD, 0xCE, 0.10),
            BorderDefault      = RGBA.From(0xE9, 0xDD, 0xCE, 0.18),
            BorderStrong       = RGBA.From(0xE9, 0xDD, 0xCE, 0.35),
            BorderSilver       = RGBA.From(0xC9, 0xB7, 0x8F, 0.55),
            BorderSilverBright = RGBA.From(0xE9, 0xDD, 0xCE, 0.85),

            Accent       = RGBA.From(0xC5, 0x89, 0x48, 1.00),
            AccentSoft   = RGBA.From(0xC5, 0x89, 0x48, 0.40),
            AccentDim    = RGBA.From(0xC5, 0x89, 0x48, 0.18),
            AccentBright = RGBA.From(0xE3, 0xA8, 0x6A, 1.00),
            Highlight    = RGBA.From(0xA8, 0x8B, 0x6C, 1.00),

            StatusAvailable = RGBA.From(0xC5, 0x89, 0x48, 1.00),
            StatusActive    = RGBA.From(0x9B, 0xC5, 0xE6, 1.00),
            StatusComplete  = RGBA.From(0x9F, 0xCB, 0x6E, 1.00),
            StatusLocked    = RGBA.From(0x8A, 0x7C, 0x68, 1.00),
            StatusCooldown  = RGBA.From(0x8A, 0x7C, 0x68, 1.00),
            StatusFailed    = RGBA.From(0xCD, 0x66, 0x5C, 1.00),

            TextPrimary   = RGBA.From(0xE9, 0xDD, 0xCE, 1.00),
            TextSecondary = RGBA.From(0xC9, 0xB7, 0x9C, 1.00),
            TextMuted     = RGBA.From(0x8F, 0x80, 0x6A, 1.00),
            TextDisabled  = RGBA.From(0x55, 0x47, 0x36, 1.00),
        };
    }
}
