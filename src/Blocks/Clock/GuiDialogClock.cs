using System;
using ArcanumLib.Gui.Controls;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ArcanumLib.Blocks.Clock;

/// <summary>
/// Arcanum-styled clock chime configuration dialog.
/// Hours are laid out around an analog clock face; each hour is a clock hand
/// that can be clicked to toggle whether the clock chimes at that hour.
/// </summary>
public class GuiDialogClock : GuiDialog
{
    private const int PacketSave = BlockEntityClock.PacketSave;

    private const double DialogWidth = 430.0;
    private const double DialogHeight = 480.0;
    private const double TitleBarHeight = 31.0;

    private const int HourCount = 24;

    private readonly BlockPos _bePos;
    private readonly string _langPrefix;
    private ClockSettingsData _data;
    private int _hoveredHour = -1;
    private ElementBounds _drawBounds = new();

    public GuiDialogClock(BlockPos bePos, ICoreClientAPI capi, ClockSettingsData data, string langPrefix = ClockConfig.DefaultLangPrefix)
        : base(capi)
    {
        _bePos = bePos;
        _langPrefix = langPrefix;
        _data = data ?? new ClockSettingsData();
    }

    public override string ToggleKeyCombinationCode => null!;

    /// <summary>Allows the cursor to move freely over the clock face.</summary>
    public override bool PrefersUngrabbedMouse => true;

    /// <summary>Receives mouse events while the dialog is open.</summary>
    public override bool ShouldReceiveMouseEvents() => IsOpened();

    /// <summary>Receives keyboard events while the dialog is open.</summary>
    public override bool ShouldReceiveKeyboardEvents() => IsOpened();

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        Compose();
    }

    public void UpdateFromServer(ClockSettingsData data)
    {
        if (data == null) return;

        _data = data;
        SingleComposer?.ReCompose();
    }

    public override void OnMouseMove(MouseEvent args)
    {
        if (args.Handled)
        {
            base.OnMouseMove(args);
            return;
        }

        int previousHover = _hoveredHour;
        _hoveredHour = GetHourAt(args.X, args.Y);

        if (previousHover != _hoveredHour)
        {
            SingleComposer?.ReCompose();
        }

        base.OnMouseMove(args);
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (args.Handled)
        {
            base.OnMouseDown(args);
            return;
        }

        int hour = GetHourAt(args.X, args.Y);
        if (hour >= 0)
        {
            ToggleHour(hour);
            args.Handled = true;
            return;
        }

        base.OnMouseDown(args);
    }

    public override void OnKeyDown(KeyEvent args)
    {
        if (!IsOpened()) return;

        if (args.KeyCode == (int)GlKeys.Escape)
        {
            TryClose();
            args.Handled = true;
        }

        base.OnKeyDown(args);
    }

    private void Compose()
    {
        ClearComposers();

        string title = Lang.Get($"{_langPrefix}clock-settings-title");

        ElementBounds dialogBounds = ElementBounds
            .Fixed(0, 0, DialogWidth, DialogHeight)
            .WithAlignment(EnumDialogArea.CenterMiddle);

        ElementBounds bgBounds = ElementBounds.Fixed(0, 0, DialogWidth, DialogHeight);

        _drawBounds = ElementBounds
            .Fixed(0, TitleBarHeight, DialogWidth, DialogHeight - TitleBarHeight)
            .WithParent(bgBounds);

        SingleComposer = capi.Gui
            .CreateCompo($"arcanum-clock-{_bePos}", dialogBounds)
            .AddArcanumDialogBackground(bgBounds)
            .AddDialogTitleBar(title, () => TryClose())
            .BeginChildElements(bgBounds)
            .AddStaticCustomDraw(_drawBounds, OnDraw)
            .EndChildElements()
            .Compose();
    }

    private void OnDraw(Context ctx, ImageSurface surface, ElementBounds b)
    {
        b.CalcWorldBounds();

        double contentX = b.drawX;
        double contentY = b.drawY;
        double contentW = b.OuterWidth;
        double contentH = b.OuterHeight;

        double bottomH = GuiElement.scaled(46);
        double clockAreaH = contentH - bottomH;
        double cx = contentX + contentW / 2.0;
        double cy = contentY + clockAreaH / 2.0;

        double outerR = Math.Min(contentW, clockAreaH) / 2.0 - GuiElement.scaled(34);
        double innerR = GuiElement.scaled(36);
        double labelR = outerR + GuiElement.scaled(8);

        // Clock face backing
        ArcanumGuiTheme.FillCircle(ctx, cx, cy, outerR, ArcanumGuiTheme.SurfaceDeepest.WithAlpha(0.94));
        ArcanumGuiTheme.StrokeCircle(ctx, cx, cy, outerR, ArcanumGuiTheme.BorderSilver, GuiElement.scaled(1.5));

        // Decorative inner ring
        ArcanumGuiTheme.StrokeCircle(ctx, cx, cy, innerR, ArcanumGuiTheme.BorderDefault, GuiElement.scaled(1.0));

        const double step = 2.0 * Math.PI / HourCount;
        const double startAngle = -Math.PI / 2.0 - step / 2.0;

        for (int h = 0; h < HourCount; h++)
        {
            double angle = startAngle + h * step + step / 2.0;
            bool active = ((_data.EnabledHours >> h) & 1) == 1;
            bool hovered = _hoveredHour == h;

            DrawHourHand(ctx, cx, cy, angle, innerR, outerR, active, hovered);
            DrawHourLabel(ctx, cx, cy, angle, labelR, h, active, hovered);
        }

        // Center hub
        ArcanumGuiTheme.FillCircle(ctx, cx, cy, GuiElement.scaled(6), ArcanumGuiTheme.Accent);
        ArcanumGuiTheme.StrokeCircle(ctx, cx, cy, GuiElement.scaled(6), ArcanumGuiTheme.BorderSilverBright, GuiElement.scaled(1.0));

        // Bottom hint / hover caption
        string caption = _hoveredHour >= 0
            ? GetHoverText(_hoveredHour)
            : Lang.Get($"{_langPrefix}clock-settings-hint");

        DrawBottomText(ctx, contentX, contentY + clockAreaH, contentW, bottomH, caption);
    }

    private void DrawHourHand(Context ctx, double cx, double cy, double angle, double innerR, double outerR, bool active, bool hovered)
    {
        double handInnerR = innerR + GuiElement.scaled(4);
        double handOuterR = outerR - GuiElement.scaled(6);

        double baseHalfAngle = Math.PI / HourCount * 0.85;
        double tipHalfAngle = Math.PI / HourCount * 0.30;

        (double x, double y) p1 = Polar(cx, cy, handInnerR, angle - baseHalfAngle);
        (double x, double y) p2 = Polar(cx, cy, handOuterR, angle - tipHalfAngle);
        (double x, double y) p3 = Polar(cx, cy, handOuterR, angle + tipHalfAngle);
        (double x, double y) p4 = Polar(cx, cy, handInnerR, angle + baseHalfAngle);

        ctx.MoveTo(p1.x, p1.y);
        ctx.LineTo(p2.x, p2.y);
        ctx.LineTo(p3.x, p3.y);
        ctx.LineTo(p4.x, p4.y);
        ctx.ClosePath();

        RGBA fill;
        if (active)
        {
            fill = hovered ? ArcanumGuiTheme.AccentBright : ArcanumGuiTheme.Accent;
        }
        else
        {
            fill = hovered ? ArcanumGuiTheme.SurfaceCardHover : ArcanumGuiTheme.SurfaceCard;
        }

        RGBA stroke;
        if (active)
        {
            stroke = ArcanumGuiTheme.BorderSilverBright;
        }
        else
        {
            stroke = hovered ? ArcanumGuiTheme.BorderStrong : ArcanumGuiTheme.BorderDefault;
        }

        fill.WithAlpha(active ? 0.95 : 0.65).Apply(ctx);
        ctx.FillPreserve();

        stroke.WithAlpha(active ? 1.0 : 0.55).Apply(ctx);
        ctx.LineWidth = GuiElement.scaled(hovered ? 2.0 : 1.5);
        ctx.LineJoin = LineJoin.Round;
        ctx.Stroke();

        // Decorative tip dot
        (double tipX, double tipY) = Polar(cx, cy, handOuterR, angle);
        RGBA tipColor = active ? ArcanumGuiTheme.AccentBright : ArcanumGuiTheme.BorderSilver;
        ArcanumGuiTheme.FillCircle(ctx, tipX, tipY, GuiElement.scaled(2.5), tipColor);
    }

    private void DrawHourLabel(Context ctx, double cx, double cy, double angle, double labelR, int hour, bool active, bool hovered)
    {
        string text = hour.ToString("D2");

        ctx.SelectFontFace("Sans", FontSlant.Normal, active || hovered ? FontWeight.Bold : FontWeight.Normal);
        ctx.SetFontSize(GuiElement.scaled(12));

        RGBA color = active ? ArcanumGuiTheme.TextPrimary : (hovered ? ArcanumGuiTheme.TextSecondary : ArcanumGuiTheme.TextMuted);
        color.Apply(ctx);

        TextExtents ext = ctx.TextExtents(text);

        double halfW = ext.Width / 2.0;
        double halfH = ext.Height / 2.0;
        double sideProjection = halfW * Math.Abs(Math.Cos(angle)) + halfH * Math.Abs(Math.Sin(angle));
        double labelCenterR = labelR + sideProjection;

        double lx = cx + Math.Cos(angle) * labelCenterR;
        double ly = cy + Math.Sin(angle) * labelCenterR;

        ctx.MoveTo(lx - halfW - ext.XBearing, ly - halfH - ext.YBearing);
        ctx.ShowText(text);
    }

    private void DrawBottomText(Context ctx, double x, double y, double w, double h, string text)
    {
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(GuiElement.scaled(12));
        ArcanumGuiTheme.TextSecondary.Apply(ctx);

        TextExtents ext = ctx.TextExtents(text);
        double tx = x + (w - ext.Width) / 2.0 - ext.XBearing;
        double ty = y + (h - ext.Height) / 2.0 - ext.YBearing;

        ctx.MoveTo(tx, ty);
        ctx.ShowText(text);
    }

    private int GetHourAt(int mouseX, int mouseY)
    {
        if (SingleComposer?.Bounds == null) return -1;

        SingleComposer.Bounds.CalcWorldBounds();

        double dialogX = SingleComposer.Bounds.absX;
        double dialogY = SingleComposer.Bounds.absY;
        double dialogW = SingleComposer.Bounds.InnerWidth;
        double dialogH = SingleComposer.Bounds.InnerHeight;

        double contentX = dialogX;
        double contentY = dialogY + GuiElement.scaled(TitleBarHeight);
        double contentW = dialogW;
        double contentH = dialogH - GuiElement.scaled(TitleBarHeight);
        double bottomH = GuiElement.scaled(46);
        double clockAreaH = contentH - bottomH;
        double cx = contentX + contentW / 2.0;
        double cy = contentY + clockAreaH / 2.0;

        double dx = mouseX - cx;
        double dy = mouseY - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        double outerR = Math.Min(contentW, clockAreaH) / 2.0 - GuiElement.scaled(34);
        double innerR = GuiElement.scaled(36);

        if (dist < innerR + GuiElement.scaled(4) || dist > outerR + GuiElement.scaled(40))
            return -1;

        double angle = Math.Atan2(dy, dx);
        const double step = 2.0 * Math.PI / HourCount;
        const double startAngle = -Math.PI / 2.0 - step / 2.0;
        double norm = GameMath.Mod(angle - startAngle, 2.0 * Math.PI);
        int hour = (int)(norm / step);

        if (hour < 0 || hour >= HourCount) return -1;
        return hour;
    }

    private void ToggleHour(int hour)
    {
        int mask = 1 << hour;
        _data.EnabledHours = (_data.EnabledHours & mask) != 0
            ? (_data.EnabledHours & ~mask)
            : (_data.EnabledHours | mask);

        capi.Network.SendBlockEntityPacket(_bePos, PacketSave, SerializerUtil.Serialize(_data));
        SingleComposer?.ReCompose();
    }

    private string GetHoverText(int hour)
    {
        string range = $"{hour:D2}:00 — {hour:D2}:59";
        bool active = ((_data.EnabledHours >> hour) & 1) == 1;
        string state = active
            ? Lang.Get($"{_langPrefix}clock-hour-active")
            : Lang.Get($"{_langPrefix}clock-hour-inactive");
        return $"{range}  ({state})";
    }

    private static (double x, double y) Polar(double cx, double cy, double r, double angle)
    {
        return (cx + Math.Cos(angle) * r, cy + Math.Sin(angle) * r);
    }
}
