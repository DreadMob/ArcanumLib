using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.RadialMenu
{
    /// <summary>Represents radial menu gui.</summary>
    public partial class RadialMenuGui
    {
        private static void DrawHeart(Context ctx, float cx, float cy, float size)
        {
            float s = size;
            ctx.MoveTo(cx, cy + s * 0.35f);
            ctx.CurveTo(cx - s, cy - s * 0.4f, cx - s * 0.5f, cy - s, cx, cy - s * 0.25f);
            ctx.CurveTo(cx + s * 0.5f, cy - s, cx + s, cy - s * 0.4f, cx, cy + s * 0.35f);
            ctx.ClosePath();
            ctx.Fill();
        }

        private static void DrawStar(Context ctx, float cx, float cy, float size)
        {
            float s = size;
            for (int i = 0; i < 5; i++)
            {
                float a = (float)(i * 2 * Math.PI / 5 - Math.PI / 2);
                float a2 = (float)((i + 0.5f) * 2 * Math.PI / 5 - Math.PI / 2);
                float ox = cx + (float)Math.Cos(a) * s;
                float oy = cy + (float)Math.Sin(a) * s;
                float ix2 = cx + (float)Math.Cos(a2) * (s * 0.4f);
                float iy2 = cy + (float)Math.Sin(a2) * (s * 0.4f);
                if (i == 0) ctx.MoveTo(ox, oy);
                else ctx.LineTo(ox, oy);
                ctx.LineTo(ix2, iy2);
            }
            ctx.ClosePath();
            ctx.Fill();
        }

        private static void DrawGear(Context ctx, float cx, float cy, float size)
        {
            float s = size;
            int teeth = 6;
            for (int i = 0; i < teeth; i++)
            {
                float a1 = (float)(i * 2 * Math.PI / teeth - Math.PI / 2);
                float a2 = (float)((i + 0.15f) * 2 * Math.PI / teeth - Math.PI / 2);
                float a3 = (float)((i + 0.35f) * 2 * Math.PI / teeth - Math.PI / 2);
                float a4 = (float)((i + 0.5f) * 2 * Math.PI / teeth - Math.PI / 2);
                float r1 = s * 0.85f;
                float r2 = s * 0.55f;
                float r3 = s * 0.45f;
                if (i == 0) ctx.MoveTo(cx + (float)Math.Cos(a1) * r1, cy + (float)Math.Sin(a1) * r1);
                else ctx.LineTo(cx + (float)Math.Cos(a1) * r1, cy + (float)Math.Sin(a1) * r1);
                ctx.LineTo(cx + (float)Math.Cos(a2) * r1, cy + (float)Math.Sin(a2) * r1);
                ctx.LineTo(cx + (float)Math.Cos(a3) * r2, cy + (float)Math.Sin(a3) * r2);
                ctx.LineTo(cx + (float)Math.Cos(a4) * r3, cy + (float)Math.Sin(a4) * r3);
            }
            ctx.ClosePath();
            ctx.Stroke();
            ctx.Arc(cx, cy, s * 0.25f, 0, 2f * (float)Math.PI);
            ctx.Stroke();
        }

        private static void DrawSectorIcon(Context ctx, IRadialMenuStyle style, float cx, float cy, float a0, float a1,
            float iconRadius, float s, string icon, bool disabled, Action<Context, float, float, float>? customDraw = null)
        {
            float midAngle = (a0 + a1) / 2f;
            float ix = cx + (float)Math.Cos(midAngle) * iconRadius;
            float iy = cy + (float)Math.Sin(midAngle) * iconRadius;

            var (r, g, b, a) = style.GetIconColor(disabled);
            ctx.SetSourceRGBA(r, g, b, a);
            ctx.LineWidth = 2.2f * s / 20f;
            ctx.LineCap = LineCap.Round;

            if (customDraw != null)
            {
                ctx.LineWidth = 4.5f * s / 20f;
                customDraw(ctx, ix, iy, s * 4f);
                return;
            }

            switch (icon)
            {
                case "heart":
                    DrawHeart(ctx, ix, iy, s);
                    break;
                case "reset":
                    ctx.Arc(ix, iy, s * 0.7f, 0.3f, 2.1f);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.45f, iy - s * 0.15f);
                    ctx.LineTo(ix + s * 0.15f, iy - s * 0.45f);
                    ctx.LineTo(ix + s * 0.55f, iy - s * 0.65f);
                    ctx.Stroke();
                    break;
                case "sword":
                    ctx.MoveTo(ix, iy - s);
                    ctx.LineTo(ix, iy + s * 0.4f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.7f, iy - s * 0.2f);
                    ctx.LineTo(ix + s * 0.7f, iy - s * 0.2f);
                    ctx.Stroke();
                    break;
                case "reload":
                    ctx.Arc(ix, iy, s * 0.7f, 0.4f, 2.2f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.25f, iy - s * 0.55f);
                    ctx.LineTo(ix - s * 0.05f, iy - s * 0.85f);
                    ctx.LineTo(ix + s * 0.2f, iy - s * 0.45f);
                    ctx.Stroke();
                    break;
                case "clear":
                    ctx.MoveTo(ix - s * 0.5f, iy + s * 0.5f);
                    ctx.LineTo(ix + s * 0.7f, iy - s * 0.7f);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.35f, iy - s * 0.35f);
                    ctx.LineTo(ix + s * 0.7f, iy - s * 0.7f);
                    ctx.LineTo(ix + s * 0.35f, iy - s * 0.85f);
                    ctx.Stroke();
                    break;
                case "bug":
                    ctx.Arc(ix, iy, s * 0.4f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.55f, iy - s * 0.15f);
                    ctx.LineTo(ix - s * 0.35f, iy + s * 0.05f);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.55f, iy - s * 0.15f);
                    ctx.LineTo(ix + s * 0.35f, iy + s * 0.05f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.45f, iy + s * 0.25f);
                    ctx.LineTo(ix - s * 0.25f, iy + s * 0.1f);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.45f, iy + s * 0.25f);
                    ctx.LineTo(ix + s * 0.25f, iy + s * 0.1f);
                    ctx.Stroke();
                    break;
                case "clock":
                    ctx.Arc(ix, iy, s * 0.7f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    ctx.MoveTo(ix, iy);
                    ctx.LineTo(ix, iy - s * 0.5f);
                    ctx.Stroke();
                    ctx.MoveTo(ix, iy);
                    ctx.LineTo(ix + s * 0.3f, iy + s * 0.15f);
                    ctx.Stroke();
                    break;
                case "fire":
                    ctx.MoveTo(ix, iy - s);
                    ctx.LineTo(ix - s * 0.55f, iy + s * 0.3f);
                    ctx.LineTo(ix - s * 0.15f, iy + s * 0.1f);
                    ctx.LineTo(ix, iy + s * 0.5f);
                    ctx.LineTo(ix + s * 0.15f, iy + s * 0.1f);
                    ctx.LineTo(ix + s * 0.55f, iy + s * 0.3f);
                    ctx.ClosePath();
                    ctx.Fill();
                    break;
                case "star":
                    DrawStar(ctx, ix, iy, s);
                    break;
                case "feather":
                    ctx.MoveTo(ix - s * 0.1f, iy + s * 0.5f);
                    ctx.CurveTo(ix - s * 0.5f, iy + s * 0.2f, ix - s * 0.4f, iy - s * 0.4f, ix, iy - s * 0.6f);
                    ctx.CurveTo(ix + s * 0.4f, iy - s * 0.4f, ix + s * 0.5f, iy + s * 0.2f, ix + s * 0.1f, iy + s * 0.5f);
                    ctx.Stroke();
                    ctx.MoveTo(ix, iy - s * 0.6f);
                    ctx.LineTo(ix, iy + s * 0.5f);
                    ctx.Stroke();
                    break;
                case "shuffle":
                    ctx.MoveTo(ix - s * 0.5f, iy - s * 0.2f);
                    ctx.LineTo(ix + s * 0.1f, iy - s * 0.2f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.1f, iy - s * 0.4f);
                    ctx.LineTo(ix + s * 0.1f, iy - s * 0.2f);
                    ctx.LineTo(ix - s * 0.1f, iy);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.5f, iy + s * 0.2f);
                    ctx.LineTo(ix - s * 0.1f, iy + s * 0.2f);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.1f, iy);
                    ctx.LineTo(ix - s * 0.1f, iy + s * 0.2f);
                    ctx.LineTo(ix + s * 0.1f, iy + s * 0.4f);
                    ctx.Stroke();
                    break;
                case "arrowhook":
                    ctx.MoveTo(ix, iy - s * 0.6f);
                    ctx.LineTo(ix, iy + s * 0.1f);
                    ctx.Stroke();
                    ctx.Arc(ix - s * 0.2f, iy + s * 0.1f, s * 0.2f, 0, (float)Math.PI);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.4f, iy + s * 0.1f);
                    ctx.LineTo(ix - s * 0.2f, iy - s * 0.1f);
                    ctx.Stroke();
                    ctx.Arc(ix, iy - s * 0.6f, s * 0.1f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    ctx.Arc(ix, iy - s * 0.35f, s * 0.1f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    break;
                case "grab":
                    ctx.MoveTo(ix - s * 0.35f, iy - s * 0.4f);
                    ctx.LineTo(ix - s * 0.35f, iy + s * 0.3f);
                    ctx.Stroke();
                    ctx.MoveTo(ix, iy - s * 0.5f);
                    ctx.LineTo(ix, iy + s * 0.3f);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.35f, iy - s * 0.4f);
                    ctx.LineTo(ix + s * 0.35f, iy + s * 0.3f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.5f, iy + s * 0.3f);
                    ctx.LineTo(ix + s * 0.5f, iy + s * 0.3f);
                    ctx.Stroke();
                    break;
                case "dice":
                    ctx.Rectangle(ix - s * 0.5f, iy - s * 0.5f, s, s);
                    ctx.Stroke();
                    ctx.Arc(ix, iy, s * 0.15f, 0, 2f * (float)Math.PI);
                    ctx.Fill();
                    break;
                case "food":
                    ctx.Arc(ix, iy + s * 0.15f, s * 0.6f, (float)Math.PI, 2f * (float)Math.PI);
                    ctx.ClosePath();
                    ctx.Fill();
                    ctx.MoveTo(ix - s * 0.25f, iy + s * 0.15f);
                    ctx.LineTo(ix + s * 0.25f, iy + s * 0.15f);
                    ctx.Stroke();
                    break;
                case "explosion":
                    ctx.MoveTo(ix - s * 0.7f, iy - s * 0.7f);
                    ctx.LineTo(ix + s * 0.7f, iy + s * 0.7f);
                    ctx.MoveTo(ix + s * 0.7f, iy - s * 0.7f);
                    ctx.LineTo(ix - s * 0.7f, iy + s * 0.7f);
                    ctx.MoveTo(ix, iy - s);
                    ctx.LineTo(ix, iy + s);
                    ctx.MoveTo(ix - s, iy);
                    ctx.LineTo(ix + s, iy);
                    ctx.Stroke();
                    break;
                case "dash":
                    ctx.MoveTo(ix - s * 0.5f, iy - s * 0.35f);
                    ctx.LineTo(ix + s * 0.2f, iy);
                    ctx.LineTo(ix - s * 0.5f, iy + s * 0.35f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.7f, iy - s * 0.2f);
                    ctx.LineTo(ix - s * 0.35f, iy - s * 0.2f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.7f, iy);
                    ctx.LineTo(ix - s * 0.25f, iy);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.7f, iy + s * 0.2f);
                    ctx.LineTo(ix - s * 0.35f, iy + s * 0.2f);
                    ctx.Stroke();
                    break;
                case "skip":
                    ctx.MoveTo(ix - s * 0.6f, iy + s * 0.4f);
                    ctx.LineTo(ix - s * 0.2f, iy + s * 0.4f);
                    ctx.LineTo(ix - s * 0.2f, iy - s * 0.5f);
                    ctx.LineTo(ix + s * 0.2f, iy - s * 0.1f);
                    ctx.LineTo(ix - s * 0.2f, iy + s * 0.3f);
                    ctx.LineTo(ix + s * 0.6f, iy + s * 0.3f);
                    ctx.Stroke();
                    break;
                case "info":
                    ctx.Arc(ix, iy, s * 0.7f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    ctx.MoveTo(ix, iy - s * 0.1f);
                    ctx.LineTo(ix, iy + s * 0.4f);
                    ctx.Stroke();
                    ctx.Arc(ix, iy - s * 0.35f, s * 0.12f, 0, 2f * (float)Math.PI);
                    ctx.Fill();
                    break;
                case "gear":
                    DrawGear(ctx, ix, iy, s);
                    break;
                case "shield":
                    ctx.MoveTo(ix - s * 0.7f, iy - s * 0.3f);
                    ctx.Arc(ix, iy - s * 0.3f, s * 0.7f, (float)Math.PI, 0);
                    ctx.LineTo(ix + s * 0.7f, iy - s * 0.3f);
                    ctx.CurveTo(ix + s * 0.7f, iy + s * 0.5f, ix + s * 0.3f, iy + s * 0.8f, ix, iy + s * 0.9f);
                    ctx.CurveTo(ix - s * 0.3f, iy + s * 0.8f, ix - s * 0.7f, iy + s * 0.5f, ix - s * 0.7f, iy - s * 0.3f);
                    ctx.ClosePath();
                    ctx.Stroke();
                    ctx.MoveTo(ix, iy - s * 0.1f);
                    ctx.LineTo(ix, iy + s * 0.5f);
                    ctx.MoveTo(ix - s * 0.3f, iy + s * 0.2f);
                    ctx.LineTo(ix + s * 0.3f, iy + s * 0.2f);
                    ctx.Stroke();
                    break;
                case "scroll":
                    ctx.Rectangle(ix - s * 0.5f, iy - s * 0.55f, s, s * 1.1f);
                    ctx.Stroke();
                    ctx.Arc(ix, iy - s * 0.55f, s * 0.5f, (float)Math.PI, 0);
                    ctx.Stroke();
                    ctx.Arc(ix, iy + s * 0.55f, s * 0.5f, 0, (float)Math.PI);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.25f, iy - s * 0.15f);
                    ctx.LineTo(ix + s * 0.25f, iy - s * 0.15f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.25f, iy + s * 0.05f);
                    ctx.LineTo(ix + s * 0.25f, iy + s * 0.05f);
                    ctx.Stroke();
                    ctx.MoveTo(ix - s * 0.25f, iy + s * 0.25f);
                    ctx.LineTo(ix + s * 0.1f, iy + s * 0.25f);
                    ctx.Stroke();
                    break;
                case "user":
                    ctx.Arc(ix, iy - s * 0.25f, s * 0.35f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    ctx.Arc(ix, iy + s * 0.75f, s * 0.75f, (float)Math.PI, 0);
                    ctx.Stroke();
                    break;
                case "music":
                    ctx.Arc(ix - s * 0.25f, iy + s * 0.2f, s * 0.22f, 0, 2f * (float)Math.PI);
                    ctx.Fill();
                    ctx.MoveTo(ix + s * 0.05f, iy + s * 0.2f);
                    ctx.LineTo(ix + s * 0.05f, iy - s * 0.6f);
                    ctx.Stroke();
                    ctx.MoveTo(ix + s * 0.05f, iy - s * 0.6f);
                    ctx.LineTo(ix + s * 0.45f, iy - s * 0.25f);
                    ctx.Stroke();
                    break;
                case "eye":
                    ctx.Save();
                    ctx.Translate(ix, iy);
                    ctx.Scale(1.0, 0.6);
                    ctx.Arc(0, 0, s * 0.7f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    ctx.Restore();
                    ctx.Arc(ix, iy, s * 0.22f, 0, 2f * (float)Math.PI);
                    ctx.Fill();
                    break;
                case "rune":
                    ctx.MoveTo(ix, iy - s);
                    ctx.LineTo(ix, iy + s);
                    ctx.MoveTo(ix, iy - s * 0.25f);
                    ctx.LineTo(ix + s * 0.55f, iy - s * 0.7f);
                    ctx.MoveTo(ix, iy + s * 0.25f);
                    ctx.LineTo(ix - s * 0.55f, iy + s * 0.7f);
                    ctx.Stroke();
                    ctx.Arc(ix, iy, s * 0.18f, 0, 2f * (float)Math.PI);
                    ctx.Fill();
                    break;
                default:
                    ctx.Arc(ix, iy, s * 0.3f, 0, 2f * (float)Math.PI);
                    ctx.Stroke();
                    break;
            }
        }

        private void UpdateHoveredIndex(float mouseX, float mouseY)
        {
            if (SingleComposer?.Bounds == null) return;
            SingleComposer.Bounds.CalcWorldBounds();

            float scale = (float)RuntimeEnv.GUIScale;
            float cx = (float)(SingleComposer.Bounds.absX + SingleComposer.Bounds.InnerWidth / 2);
            float cy = (float)(SingleComposer.Bounds.absY + (_outerRadius + Margin) * scale);

            int oldHover = _hoveredIndex;
            int oldArc = _hoveredArc;
            int oldExpanded = _expandedIndex;

            var layout = new RadialMenuLayout(_items.Count, _outerRadius, _innerRadius);
            int arcCount = _expandedIndex >= 0 && _expandedIndex < _items.Count ? _items[_expandedIndex].Arcs?.Count ?? 0 : 0;
            var (sector, arc) = layout.Hit((mouseX - cx) / scale, (mouseY - cy) / scale, _expandedIndex, arcCount);
            _hoveredIndex = sector;
            _hoveredArc = arc;
            if (arc < 0)
            {
                // a sector with sub-arcs opens its ring; any other sector (or the center) closes it, the gap and the outside keep it
                if (sector >= 0) _expandedIndex = _items[sector].Arcs?.Count > 0 && !_items[sector].Disabled ? sector : -1;
                else if (sector == -2) _expandedIndex = -1;
            }

            if (oldHover != _hoveredIndex || oldArc != _hoveredArc || oldExpanded != _expandedIndex)
            {
                SingleComposer?.ReCompose();
            }
        }

        /// <summary>The item under the mouse: a sub-arc of the open ring, else the hovered sector, else null.</summary>
        private RadialMenuItem? HoveredItem()
        {
            if (_hoveredIndex < 0 || _hoveredIndex >= _items.Count) return null;
            var item = _items[_hoveredIndex];
            if (_hoveredArc >= 0 && item.Arcs != null && _hoveredArc < item.Arcs.Count) return item.Arcs[_hoveredArc];
            return item;
        }

        /// <summary>Performs the on mouse move operation.</summary>
        /// <param name="args">The arguments.</param>
        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);
            UpdateHoveredIndex(args.X, args.Y);
        }

        /// <summary>Performs the on mouse down operation.</summary>
        /// <param name="args">The arguments.</param>
        public override void OnMouseDown(MouseEvent args)
        {
            base.OnMouseDown(args);
            if (args.Button != EnumMouseButton.Left) return;

            UpdateHoveredIndex(args.X, args.Y);

            if (_hoveredIndex == -2)
            {
                TryClose();
                return;
            }

            if (HoveredItem() is { } item)
            {

                // Locked sector: no action, no submenu — keep the wheel open.
                if (item.Disabled) return;

                if (item.SubItems?.Count > 0)
                {
                    TryClose();
                    var subGui = new RadialMenuGui(capi, item.Label, item.SubItems, _outerRadius, _innerRadius);
                    subGui.TryOpen();
                    return;
                }

                if (item.Action != null)
                {
                    try { item.Action(); }
                    catch (Exception ex)
                    {
                        capi?.Logger?.Warning("[RadialMenuGui] Action swallowed exception: {0}", ex.Message);
                    }
                }
                if (item.CloseAfterClick)
                {
                    TryClose();
                }
            }
        }

        /// <summary>Performs the on key down operation.</summary>
        /// <param name="args">The arguments.</param>
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

        /// <summary>Performs the on key up operation.</summary>
        /// <param name="args">The arguments.</param>
        public override void OnKeyUp(KeyEvent args)
        {
            base.OnKeyUp(args);
            if (_holdKey.HasValue && args.KeyCode == (int)_holdKey.Value)
            {
                args.Handled = true;
                if (HoveredItem() is { } item)
                {
                    if (!item.Disabled && item.Action != null)
                        try { item.Action(); }
                        catch (Exception ex)
                        {
                            capi?.Logger?.Warning("[RadialMenuGui] Hold-key action swallowed exception: {0}", ex.Message);
                        }
                }
                TryClose();
            }
        }
    }
}
