using System;
using ArcanumLib.Diagnostics;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Themed dropdown: a brass plate showing the current choice; clicking opens a
/// small floating menu (<see cref="GuiDialogArcanumDropdown" />). Mirrors the
/// vanilla <c>AddDropDown(values, names, index, onSelected, bounds, font, key)</c>
/// call surface.
/// </summary>
public class ArcanumDropdown : GuiElement
{
    private readonly string[] values;
    private readonly string[] names;
    private readonly Action<string, bool> onSelected;
    private readonly CairoFont? font;

    private int selectedIndex;
    private bool hovered;
    private bool enabled = true;
    private LoadedTexture tex;
    private string? cacheKey;
    /// <summary>Theme override forwarded to the floating menu dialog — the
    /// parent's palette, so the spawned menu matches the dialog that owns the
    /// dropdown instead of reading whatever the ambient palette happens to be.</summary>
    private readonly GuiThemePalette? palette;

    /// <param name="capi">The client API.</param>
    /// <param name="bounds">Control bounds.</param>
    /// <param name="values">Machine values per option.</param>
    /// <param name="names">Display names per option.</param>
    /// <param name="selectedIndex">Initially selected index.</param>
    /// <param name="onSelected">Called (value, true) when the user picks an option.</param>
    /// <param name="font">Label font.</param>
    /// <param name="palette">Optional theme override for the spawned menu
    /// (pass the owning dialog's palette so theming stays dialog-local).</param>
    public ArcanumDropdown(ICoreClientAPI capi, ElementBounds bounds,
        string[] values, string[] names, int selectedIndex,
        Action<string, bool> onSelected, CairoFont? font = null,
        GuiThemePalette? palette = null)
        : base(capi, bounds)
    {
        this.values = values;
        this.names = names;
        this.selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, names.Length - 1));
        this.onSelected = onSelected;
        this.font = font;
        this.palette = palette;
        tex = new LoadedTexture(capi);
        GuiTextureTracker.Register(nameof(ArcanumDropdown));
    }

    /// <summary>Enable/disable the dropdown; disabled dropdowns render dimmed
    /// and ignore clicks.</summary>
    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; cacheKey = null; } }
    }

    /// <summary>Currently selected machine value.</summary>
    public string SelectedValue =>
        selectedIndex >= 0 && selectedIndex < values.Length ? values[selectedIndex] : "";

    /// <summary>Currently selected index.</summary>
    public int SelectedIndex => selectedIndex;

    /// <summary>Select an index silently (no callback).</summary>
    public void SetSelectedIndex(int index)
    {
        int nv = Math.Clamp(index, 0, Math.Max(0, names.Length - 1));
        if (nv != selectedIndex) { selectedIndex = nv; cacheKey = null; }
    }

    /// <summary>Select by value silently.</summary>
    public void SetSelectedValue(string code)
    {
        int idx = Array.IndexOf(values, code);
        if (idx >= 0) SetSelectedIndex(idx);
    }

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <inheritdoc />
    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || !Bounds.PointInside(args.X, args.Y)) return;
        args.Handled = true;
        api.Gui.PlaySound("menubutton_press", false, 0.2f);

        Bounds.CalcWorldBounds();
        double x = Bounds.renderX;
        double y = Bounds.renderY + Bounds.OuterHeight + scaled(2);
        var menu = new GuiDialogArcanumDropdown(api, x, y,
            Math.Max(Bounds.OuterWidth, scaled(140)), names, selectedIndex, i =>
            {
                selectedIndex = i;
                cacheKey = null;
                if (i >= 0 && i < values.Length)
                    onSelected?.Invoke(values[i], true);
            }, palette);
        menu.TryOpen();
    }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        bool nowHovered = enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        if (nowHovered != hovered) { hovered = nowHovered; cacheKey = null; }

        RegenerateIfNeeded();
        if (tex?.TextureId > 0)
            api.Render.Render2DLoadedTexture(tex, (float)Bounds.absX, (float)Bounds.absY);
    }

    private void RegenerateIfNeeded()
    {
        if (Bounds == null || api?.Render == null) return;
        tex ??= new LoadedTexture(api!);
        string key = $"{selectedIndex}|{hovered}|{enabled}|{(int)Bounds.OuterWidth}x{(int)Bounds.OuterHeight}";
        if (string.Equals(cacheKey, key, StringComparison.Ordinal) && tex.TextureId > 0) return;
        cacheKey = key;

        int w = Math.Max(1, (int)Bounds.OuterWidth);
        int h = Math.Max(1, (int)Bounds.OuterHeight);
        ImageSurface? surface = null; Context? ctx = null;
        try
        {
            surface = new ImageSurface(Format.Argb32, w, h);
            ctx = new Context(surface);
            var p = ArcanumGuiTheme.Palette;
            double r = scaled(ArcanumGuiTheme.Radius.Medium);

            var fill = hovered ? p.SurfaceCardHover : p.SurfaceCard;
            ArcanumGuiTheme.FillRoundedRect(ctx, 0, 0, w, h, r, fill.WithAlpha(enabled ? fill.A : fill.A * 0.5));
            ArcanumGuiTheme.StrokeRoundedRect(ctx, 0, 0, w, h, r,
                (hovered ? p.BorderStrong : p.BorderDefault).WithAlpha(enabled ? 1 : 0.4), scaled(1.0));
            ArcanumGuiTheme.DrawInnerHighlight(ctx, 1, 1, w - 2, h - 2, r - 1, 0.08);

            // Label.
            string label = selectedIndex >= 0 && selectedIndex < names.Length ? names[selectedIndex] : "—";
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            ctx.SetFontSize(scaled(11));
            var ext = ctx.TextExtents(label);
            double maxW = w - scaled(30);
            while (ext.Width > maxW && label.Length > 4)
            {
                label = label[..^2];
                ext = ctx.TextExtents(label + "…");
            }
            if (ext.Width > maxW) label += "…";
            ctx.SetSourceRGBA(p.TextPrimary.R, p.TextPrimary.G, p.TextPrimary.B, enabled ? 0.95 : 0.45);
            ctx.MoveTo(scaled(9), (h - ext.Height) / 2 - ext.YBearing);
            ctx.ShowText(label);

            // Chevron.
            double cx = w - scaled(16), cy = h / 2;
            double cs = scaled(4.5);
            ctx.NewPath();
            ctx.MoveTo(cx - cs, cy - cs * 0.55);
            ctx.LineTo(cx + cs, cy - cs * 0.55);
            ctx.LineTo(cx, cy + cs * 0.65);
            ctx.ClosePath();
            ctx.SetSourceRGBA(p.Accent.R, p.Accent.G, p.Accent.B, hovered ? 1.0 : 0.7);
            ctx.Fill();

            generateTexture(surface, ref tex);
            GuiTextureTracker.Regen(nameof(ArcanumDropdown));
        }
        catch (Exception ex)
        {
            cacheKey = null;
            tex?.Dispose(); tex = new LoadedTexture(api!);
            api?.Logger?.Warning("[ArcanumDropdown] Texture generation failed: {0}", ex);
        }
        finally { ctx?.Dispose(); surface?.Dispose(); }
    }

    /// <inheritdoc />
    public override void Dispose() { tex?.Dispose(); GuiTextureTracker.Unregister(nameof(ArcanumDropdown)); base.Dispose(); }
}

/// <summary>
/// Floating menu opened by <see cref="ArcanumDropdown" />: a small themed card
/// with an <see cref="ArcanumList{T}" /> of the options. Clicking a row fires
/// the callback and closes; clicking outside or Escape closes too.
/// </summary>
public class GuiDialogArcanumDropdown : GuiDialog
{
    private readonly double x, y, w;
    private readonly string[] items;
    private readonly int selected;
    private readonly Action<int> onPick;
    /// <summary>Palette override applied while this menu composes and renders —
    /// null keeps the ambient <see cref="ArcanumGuiTheme.Palette"/> behavior.</summary>
    private readonly GuiThemePalette? palette;
    private ElementBounds? menuBounds;

    /// <inheritdoc />
    public override string ToggleKeyCombinationCode => null!;
    /// <inheritdoc />
    public override bool UnregisterOnClose => true;

    /// <summary>Create the menu at screen position (x, y) — already scaled px.</summary>
    /// <param name="palette">Optional palette the menu renders with — pass the
    /// owning dialog's theme so the menu doesn't pick up another mod's global
    /// palette state.</param>
    public GuiDialogArcanumDropdown(ICoreClientAPI capi, double x, double y, double w,
        string[] items, int selected, Action<int> onPick,
        GuiThemePalette? palette = null) : base(capi)
    {
        this.x = x; this.y = y; this.w = w;
        this.items = items;
        this.selected = selected;
        this.onPick = onPick;
        this.palette = palette;
    }

    /// <inheritdoc />
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        using var scope = palette != null ? ArcanumGuiTheme.WithPalette(palette) : null;
        double rowH = GuiElement.scaled(24);
        double h = Math.Min(items.Length, 8) * rowH + GuiElement.scaled(8);
        menuBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, x, y, w, h);
        SingleComposer = capi.Gui.CreateCompo("arcanum-dropdown-menu", menuBounds)
            .AddStaticElement(new ArcanumCard(capi, ElementBounds.Fill))
            .AddArcanumList(items, ElementBounds.Fixed(4, 4, w - 8, h - 8),
                s => s, rowH, OnPicked, key: "opts")
            .Compose();
        SingleComposer.GetArcanumList<string>("opts")?.Select(selected);
    }

    /// <inheritdoc />
    public override void OnRenderGUI(float deltaTime)
    {
        if (palette != null)
        {
            using var scope = ArcanumGuiTheme.WithPalette(palette);
            base.OnRenderGUI(deltaTime);
            return;
        }
        base.OnRenderGUI(deltaTime);
    }

    private void OnPicked(string item, int index)
    {
        onPick?.Invoke(index);
        TryClose();
    }

    /// <inheritdoc />
    public override void OnMouseDown(MouseEvent args)
    {
        base.OnMouseDown(args);
        if (!args.Handled && menuBounds != null)
        {
            menuBounds.CalcWorldBounds();
            if (!menuBounds.PointInside(args.X, args.Y)) TryClose();
        }
    }

    /// <inheritdoc />
    public override void OnKeyDown(KeyEvent args)
    {
        if (args.KeyCode == (int)GlKeys.Escape) { args.Handled = true; TryClose(); return; }
        base.OnKeyDown(args);
    }

    /// <inheritdoc />
    public override void Dispose() { base.Dispose(); }
}

/// <summary>Composer extensions for <see cref="ArcanumDropdown" />.</summary>
public static class ArcanumDropdownComposer
{
    /// <summary>Adds a themed dropdown — same call shape as vanilla AddDropDown.
    /// <paramref name="palette"/> optionally carries the owning dialog's theme
    /// into the spawned menu so it doesn't render with another mod's ambient
    /// palette.</summary>
    public static GuiComposer AddArcanumDropdown(
        this GuiComposer composer, string[] values, string[] names, int selectedIndex,
        Action<string, bool> onSelected, ElementBounds bounds, CairoFont? font = null,
        string? key = null, GuiThemePalette? palette = null)
    {
        if (!composer.Composed)
            composer.AddInteractiveElement(
                new ArcanumDropdown(composer.Api, bounds, values, names, selectedIndex, onSelected, font, palette), key);
        return composer;
    }

    /// <summary>Retrieve a dropdown added via <see cref="AddArcanumDropdown" />.</summary>
    public static ArcanumDropdown? GetArcanumDropdown(this GuiComposer composer, string key)
        => composer.GetElement(key) as ArcanumDropdown;
}
