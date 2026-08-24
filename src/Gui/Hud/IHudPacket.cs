namespace ArcanumLib.Gui.Hud;

/// <summary>
/// A network packet that carries a list of HUD entries to display.
/// Used by <see cref="PacketIconHud{TPacket, TEntry}"/>.
/// </summary>
/// <typeparam name="TEntry">Entry type contained in the packet.</typeparam>
public interface IHudPacket<TEntry>
{
    /// <summary>Entries to display, or null/empty when the HUD should close.</summary>
    TEntry[]? Entries { get; }
}
