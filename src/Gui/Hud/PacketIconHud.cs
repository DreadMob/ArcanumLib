using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic packet-driven icon HUD. Receives a packet containing entries,
/// preloads icons, shows/hides the HUD, and calls <see cref="OnDrawHud" />.
/// </summary>
/// <typeparam name="TPacket">The type of the tpacket value.</typeparam>
/// <typeparam name="TEntry">The type of the tentry value.</typeparam>
public abstract class PacketIconHud<TPacket, TEntry> : HudElement
    where TPacket : class, IHudPacket<TEntry>
{
    /// <summary>Dialog should not block the rest of the UI.</summary>
    public override string ToggleKeyCombinationCode => null!;

    /// <summary>Should not receive mouse events.</summary>
    /// <returns>true if the operation should receive mouse events; otherwise, false.</returns>
    public override bool ShouldReceiveMouseEvents() => false;

    /// <summary>Last received packet.</summary>
    protected TPacket? _lastPacket;

    /// <summary>Whether the composer has been built.</summary>
    protected bool _composed;

    /// <summary>User-driven visibility override.</summary>
    protected bool _userHidden;

    /// <summary>Client time of the last update/re-render.</summary>
    protected long _lastUpdateMs;

    /// <summary>Redraw interval in ms.</summary>
    protected virtual int UpdateIntervalMs => 200;

    /// <summary>Unique composer name. Override per mod.</summary>
    protected abstract string HudName { get; }

    /// <summary>Creates the icon HUD.</summary>
    /// <param name="capi">The client API instance.</param>
    protected PacketIconHud(ICoreClientAPI capi) : base(capi) { }

    /// <summary>Receives a packet and updates the HUD.</summary>
    /// <param name="packet">The packet value.</param>
    public virtual void UpdateFromPacket(TPacket? packet)
    {
        _lastPacket = packet;

        if (packet?.Entries != null)
        {
            foreach (var entry in packet.Entries)
            {
                if (entry != null)
                    PreloadIcon(entry);
            }
        }

        if (packet?.Entries == null || packet.Entries.Length == 0)
        {
            TryClose();
            return;
        }

        if (_userHidden)
            return;

        EnsureComposed();
        if (!IsOpened())
            TryOpen();
    }

    /// <summary>Preloads the icon for an entry. Called for every entry in a packet.</summary>
    /// <param name="entry">The entry value.</param>
    protected virtual void PreloadIcon(TEntry entry) { }

    /// <summary>Builds the single-composer with the dynamic custom draw callback.</summary>
    protected virtual void ComposeHud()
    {
        if (capi?.Gui == null) return;

        var (width, height) = MeasureHudBounds();
        var bounds = ElementBounds.Fixed(0, 0, width, height);
        var bgBounds = ElementBounds.Fixed(0, 0, width, height);

        SingleComposer = capi.Gui
            .CreateCompo(HudName, bounds)
            .AddDynamicCustomDraw(bgBounds, OnDrawHud, "background")
            .Compose();
        _composed = true;
    }

    /// <summary>Returns the desired composer width/height.</summary>
    /// <returns>The measure hud bounds.</returns>
    protected abstract (float width, float height) MeasureHudBounds();

    /// <summary>Recomposes the dialog. Override if layout changes at runtime.</summary>
    public virtual void Recompose()
    {
        _composed = false;
        TryClose();
        EnsureComposed();
        if (_lastPacket?.Entries != null && _lastPacket.Entries.Length > 0)
            TryOpen();
    }

    /// <summary>Ensures the composer has been created.</summary>
    protected virtual void EnsureComposed()
    {
        if (_composed && SingleComposer != null) return;
        ComposeHud();
    }

    /// <summary>Draws the full icon HUD.</summary>
    /// <param name="ctx">The ctx value.</param>
    /// <param name="surface">The surface value.</param>
    /// <param name="currentBounds">The current bounds value.</param>
    protected abstract void OnDrawHud(Context ctx, ImageSurface surface, ElementBounds currentBounds);

    /// <summary>Toggles user visibility without discarding the last packet.</summary>
    /// <param name="visible">The visible value.</param>
    public virtual void SetVisible(bool visible)
    {
        _userHidden = !visible;

        if (!visible)
        {
            base.TryClose();
            return;
        }

        if (_lastPacket?.Entries != null && _lastPacket.Entries.Length > 0)
        {
            EnsureComposed();
            if (!IsOpened())
                TryOpen();
        }
    }

    /// <summary>Periodically redraws the icon list.</summary>
    /// <param name="deltaTime">The delta time value.</param>
    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);

        if (_lastPacket?.Entries == null || _lastPacket.Entries.Length == 0 || _userHidden)
            return;

        long now = capi.World.ElapsedMilliseconds;
        if (now - _lastUpdateMs > UpdateIntervalMs)
        {
            _lastUpdateMs = now;
            SingleComposer?.GetCustomDraw("background")?.Redraw();
        }
    }

    /// <summary>Closes and clears the last packet.</summary>
    /// <returns>true if the operation succeeded; otherwise, false.</returns>
    public override bool TryClose()
    {
        _lastPacket = null;
        return base.TryClose();
    }

    /// <summary>Disposes and clears the composer flag.</summary>
    public override void Dispose()
    {
        _composed = false;
        base.Dispose();
    }
}
