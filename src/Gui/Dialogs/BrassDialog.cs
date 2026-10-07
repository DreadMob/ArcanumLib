#nullable enable
using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using ArcanumLib.Gui.Controls;

using ArcanumLib.Gui.Theme;

namespace ArcanumLib.Gui.Dialogs;

/// <summary>
/// Brass-and-walnut skin for the Arcanum GUI toolkit (shared by AlegacyContraptions and AlegacyAttire): dark iron + walnut surfaces,
/// brass rims and rivets, parchment text. The palette lives in ArcanumLib
/// (<see cref="GuiThemePalette" />); this file is only the mod's paint job.
/// </summary>
public static class BrassTheme
{
    /// <summary>The mod's palette — handed to each <see cref="BrassDialog"/>
    /// via <c>DialogPalette</c>; never installed on the global theme.</summary>
    public static readonly GuiThemePalette Palette = new()
    {
        // Deep lacquered wood / blackened iron surfaces.
        SurfaceDeepest    = RGBA.From(0x14, 0x0F, 0x0A, 0.97),
        SurfaceBase       = RGBA.From(0x21, 0x19, 0x12, 0.96),
        SurfaceElevated   = RGBA.From(0x31, 0x27, 0x1C, 0.98),
        SurfaceCard       = RGBA.From(0x3E, 0x31, 0x23, 0.94),
        SurfaceCardHover  = RGBA.From(0x4E, 0x3D, 0x2B, 0.97),
        SurfaceCardActive = RGBA.From(0x5E, 0x4A, 0x33, 0.98),

        // Brass rims instead of silver.
        BorderShadow       = RGBA.From(0x08, 0x05, 0x03, 0.70),
        BorderSubtle       = RGBA.From(0xE0, 0xB4, 0x66, 0.10),
        BorderDefault      = RGBA.From(0xE0, 0xB4, 0x66, 0.20),
        BorderStrong       = RGBA.From(0xE0, 0xB4, 0x66, 0.38),
        BorderSilver       = RGBA.From(0xC9, 0x97, 0x3F, 0.55),
        BorderSilverBright = RGBA.From(0xEA, 0xC9, 0x7E, 0.85),

        // Brass accent.
        Accent       = RGBA.From(0xD9, 0xA4, 0x41, 1.00),
        AccentSoft   = RGBA.From(0xD9, 0xA4, 0x41, 0.40),
        AccentDim    = RGBA.From(0xD9, 0xA4, 0x41, 0.18),
        AccentBright = RGBA.From(0xF2, 0xC9, 0x7E, 1.00),
        Highlight    = RGBA.From(0xB8, 0x7A, 0x3C, 1.00),

        // Status: brass available, amber running, green lit, rust fail.
        StatusAvailable = RGBA.From(0xD9, 0xA4, 0x41, 1.00),
        StatusActive    = RGBA.From(0x8F, 0xC9, 0x5C, 1.00),
        StatusComplete  = RGBA.From(0x8F, 0xC9, 0x5C, 1.00),
        StatusLocked    = RGBA.From(0x8A, 0x7C, 0x68, 1.00),
        StatusCooldown  = RGBA.From(0x8A, 0x7C, 0x68, 1.00),
        StatusFailed    = RGBA.From(0xD0, 0x55, 0x45, 1.00),

        // Warm parchment text.
        TextPrimary   = RGBA.From(0xEF, 0xE3, 0xC8, 1.00),
        TextSecondary = RGBA.From(0xCB, 0xB8, 0x96, 1.00),
        TextMuted     = RGBA.From(0x96, 0x82, 0x5F, 1.00),
        TextDisabled  = RGBA.From(0x5A, 0x4C, 0x38, 1.00),
    };

    /// <summary>
    /// Deep recessed well — near-black pit, lit brass rim, a top inner
    /// shadow and four corner screws: the "sunk into the plate" housing
    /// shared by the recorder deck's slot wells and the console monitor's
    /// readout panel. <paramref name="p"/> is the caller's ambient palette
    /// (compose scopes read <see cref="ArcanumGuiTheme.Palette"/>).
    /// </summary>
    public static void DrawRecessedWell(Context ctx, double x, double y, double w, double h, GuiThemePalette p)
    {
        double sc = GuiElement.scaled(1.0);
        ArcanumGuiTheme.FillRoundedRectVerticalGradient(ctx, x, y, w, h, sc * 6,
            RGBA.From(0x0A, 0x07, 0x04, 0.98), p.SurfaceDeepest);
        ArcanumGuiTheme.StrokeRoundedRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, sc * 6,
            p.BorderSilver.WithAlpha(0.6), sc * 1.4);
        // Top inner shadow — makes the well read as sunk into the plate.
        ctx.SetSourceRGBA(p.BorderShadow.R, p.BorderShadow.G, p.BorderShadow.B, 0.5);
        ctx.Rectangle(x + sc * 4, y + sc * 4, w - sc * 8, sc * 2);
        ctx.Fill();
        ArcanumGuiTheme.DrawCornerRivets(ctx, x, y, w, h, sc * 6, sc * 1.5,
            p.Accent.WithAlpha(0.75));
    }

}

/// <summary>
/// Base for the mod's dialogs: Arcanum dialog skeleton + ornate background
/// (gradient, brass rim) + rivet strips. Subclasses implement
/// <see cref="ArcanumGuiDialog.BuildComposer" /> and call
/// <see cref="BeginBrassDialog" />.
/// </summary>
public abstract class BrassDialog : ArcanumGuiDialog
{
    /// <summary>
    /// Creates a dialog bound to the client API. U02: the steampunk palette is
    /// a per-dialog property (<see cref="DialogPalette"/>), installed only for
    /// the duration of a compose or render pass — the global
    /// <see cref="ArcanumGuiTheme.Palette"/> is never touched, so overlapping
    /// dialogs and non-LIFO close order can't leak it into other windows.
    /// </summary>
    protected BrassDialog(ICoreClientAPI capi) : base(capi) { }

    /// <inheritdoc />
    protected override GuiThemePalette? DialogPalette => BrassTheme.Palette;

    /// <summary>No hotkey toggles these config dialogs.</summary>
    public override string ToggleKeyCombinationCode => null!;

    /// <inheritdoc />
    public override void OnGuiClosed()
    {
        if (_pendingPollId != 0)
        {
            capi.Event.UnregisterGameTickListener(_pendingPollId);
            _pendingPollId = 0;
        }
        _pendingProbe = null;
        // A reopened sub-dialog re-anchors at the new cursor position.
        _popupAnchor = null;
        base.OnGuiClosed();
    }

    // ── U01: pending server confirmation ─────────────────────────────────
    // A sent change is "pending" until its probe — normally "the synced block
    // entity (or item stack) now carries the requested values" — reports the
    // server applied it. While pending, the dialog renders the last CONFIRMED
    // values with instant-apply controls disabled and a status line; a probe
    // that outlives its deadline leaves a "no confirmation" note instead of
    // silently pretending the change landed.
    private Func<bool>? _pendingProbe;
    private long _pendingDeadlineMs;
    private long _pendingPollId;
    private bool _pendingTimedOut;

    /// <summary>Lang key of the "applying…" line (the "-timeout" suffix key is the timed-out note); mods override with their own domain.</summary>
    protected virtual string PendingLangKey => "arcanumlib:gui-pending";

    /// <summary>True while a sent change awaits the server's synced state.</summary>
    protected bool HasPendingChange => _pendingProbe != null;

    /// <summary>Status line: "applying…" while pending, a timeout note after the
    /// deadline, null when the last send was confirmed (or nothing was sent).</summary>
    protected string? PendingStatus =>
        _pendingProbe != null
            ? Vintagestory.API.Config.Lang.Get(PendingLangKey)
            : _pendingTimedOut
                ? Vintagestory.API.Config.Lang.Get(PendingLangKey + "-timeout")
                : null;

    /// <summary>
    /// Mark a just-sent change as awaiting confirmation. <paramref name="confirmed"/>
    /// is polled every 250 ms (typically it re-reads the synced BE/item fields);
    /// when it reports true the pending state clears, after
    /// <paramref name="timeoutMs"/> it flips to the timed-out note. Recompose is
    /// queued so controls redraw disabled immediately.
    /// </summary>
    protected void ExpectServerConfirm(Func<bool> confirmed, long timeoutMs = 3000)
    {
        _pendingProbe = confirmed;
        _pendingTimedOut = false;
        _pendingDeadlineMs = capi.ElapsedMilliseconds + timeoutMs;
        if (_pendingPollId == 0)
            _pendingPollId = capi.Event.RegisterGameTickListener(PollPendingConfirm, 250);
        if (IsOpened()) RequestRecompose();
    }

    /// <summary>Hook fired once when the pending probe resolves.
    /// <paramref name="confirmed"/> is false on timeout.</summary>
    protected virtual void OnServerConfirmResolved(bool confirmed) { }

    private void PollPendingConfirm(float dt)
    {
        var probe = _pendingProbe;
        if (probe == null) return;
        bool confirmed;
        try { confirmed = probe(); }
        catch { confirmed = true; }   // entity/slot vanished mid-flight — stop waiting
        if (confirmed)
        {
            _pendingProbe = null;
        }
        else
        {
            if (capi.ElapsedMilliseconds < _pendingDeadlineMs) return;
            _pendingProbe = null;
            _pendingTimedOut = true;
        }
        OnServerConfirmResolved(confirmed);
        if (IsOpened()) RequestRecompose();
    }

    /// <summary>Re-fetch a block entity by position — the client instance may
    /// have been recreated by an on/off state swap since the dialog opened, so
    /// a cached reference can go stale.</summary>
    protected T? FreshBe<T>(BlockPos pos) where T : BlockEntity
        => capi.World.BlockAccessor.GetBlockEntity(pos) as T;

    /// <summary>Enable/disable a composed control by key — the Arcanum controls
    /// used by these dialogs all expose an <c>Enabled</c> property.</summary>
    protected static void SetElementEnabled(GuiComposer? composer, string key, bool on)
    {
        switch (composer?.GetElement(key))
        {
            case ArcanumButton b: b.Enabled = on; break;
            case ArcanumToggle t: t.Enabled = on; break;
            case ArcanumSlider s: s.Enabled = on; break;
            case ArcanumDropdown d: d.Enabled = on; break;
        }
    }

    /// <summary>
    /// Standard dialog chrome: ornate background, rivet frame, title bar,
    /// then child bounds for the dialog content.
    /// </summary>
    /// <param name="composer">Composer to add to.</param>
    /// <param name="title">Title bar text.</param>
    /// <param name="bgBounds">Content bounds, returned for child layout.</param>
    /// <returns>The composer, inside child elements.</returns>
    protected GuiComposer BeginBrassDialog(GuiComposer composer, string title, out ElementBounds bgBounds)
    {
        bgBounds = ArcanumGuiTheme.ArcanumConfigBackgroundBounds();
        return composer
            .AddArcanumDialogBackground(bgBounds)
            .AddRivetFrame(bgBounds)
            .AddDialogTitleBar(title, () => TryClose())
            .BeginChildElements(bgBounds);
    }

    // ── Expandable sub-dialog anchor ─────────────────────────────────────
    // Pickers/menus spawned from another dialog must open beside the cursor
    // (where the user just clicked), not docked in the RightMiddle config
    // column. The anchor is captured on the first call so later recomposes
    // (pending notes, content refresh) don't chase the mouse.
    private Vec2d? _popupAnchor;

    /// <summary>
    /// Move the composed dialog beside the cursor, clamped fully inside the
    /// window. Call at the end of <see cref="ArcanumGuiDialog.BuildComposer"/>
    /// for expandable sub-dialogs. Mirrors the vanilla movable-dialog recipe
    /// (GuiElementDialogTitleBar): kill the alignment margin, write UNSCALED
    /// fixed coords — CalcWorldBounds multiplies by the GUI scale again —
    /// then rebuild the bounds tree so children follow.
    /// </summary>
    protected void AnchorPopupAtCursor()
    {
        var b = SingleComposer?.Bounds;
        if (b == null) return;
        _popupAnchor ??= new Vec2d(
            capi.Input.MouseX + GuiElement.scaled(14),
            capi.Input.MouseY + GuiElement.scaled(4));

        var win = capi.Gui.WindowBounds;
        win.CalcWorldBounds();
        double x = Math.Clamp(_popupAnchor.X, 0, Math.Max(0, win.OuterWidth - b.OuterWidth));
        double y = Math.Clamp(_popupAnchor.Y, 0, Math.Max(0, win.OuterHeight - b.OuterHeight));

        b.Alignment = EnumDialogArea.None;
        b.fixedOffsetX = 0;
        b.fixedOffsetY = 0;
        b.fixedX = x / RuntimeEnv.GUIScale;
        b.fixedY = y / RuntimeEnv.GUIScale;
        b.absMarginX = 0;
        b.absMarginY = 0;
        b.MarkDirtyRecursive();
        b.CalcWorldBounds();
    }
}

