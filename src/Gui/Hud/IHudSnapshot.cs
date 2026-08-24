namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Marker interface for snapshots delivered to a <see cref="HudPanel"/>.
/// Snapshots carry the current state to be rendered on the client.
/// </summary>
public interface IHudSnapshot
{
    /// <summary>Returns true when the client should remove this HUD section.</summary>
    bool IsRemoved();

    /// <summary>Marks this snapshot as a removal request. The client should hide the HUD.</summary>
    void MarkRemoved();
}
