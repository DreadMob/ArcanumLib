using System;
using System.Collections.Generic;
using Cairo;

namespace ArcanumLib.Gui.RadialMenu;

/// <summary>
/// A single item in a radial menu. Each item maps to a sector wedge with an
/// icon, label, description, and an optional action callback. Items can also
/// contain nested <see cref="SubItems" /> to open a sub-menu on click.
/// </summary>
public class RadialMenuItem
{
    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Gets or sets the icon.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// If set, this callback is used to draw the icon instead of the string-based Icon switch.
    /// Parameters: (Context ctx, float cx, float cy, float size)
    /// </summary>
    public Action<Context, float, float, float>? CustomIconDraw { get; set; }

    /// <summary>Gets or sets the action.</summary>
    public Action? Action { get; set; }

    /// <summary>Gets or sets a value indicating whether the close after click is enabled.</summary>
    public bool CloseAfterClick { get; set; } = true;

    /// <summary>
    /// When true, the sector is drawn with an active/toggled highlight color.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When true, the sector is drawn grayed out (e.g. on cooldown).
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// If set, clicking this item opens a nested radial menu with these items
    /// instead of firing the Action.
    /// </summary>
    public List<RadialMenuItem> SubItems { get; set; } = new();
}
