using System;
using Cairo;

namespace ArcanumLib.Gui.RadialMenu;

/// <summary>
/// Built-in default radial menu style — warm brown/gold theme.
/// Always available as the fallback when a requested style key is not registered.
/// </summary>
public class DefaultRadialMenuStyle : IRadialMenuStyle
{
    /// <inheritdoc />
    public string Key => "default";

    /// <inheritdoc />
    public virtual void DrawSector(Context ctx, float cx, float cy, float a0, float a1,
        bool hovered, bool isActive, bool disabled,
        float outerRadius, float innerRadius)
    {
        // Background wedge
        ctx.MoveTo(cx, cy);
        ctx.Arc(cx, cy, outerRadius, a0, a1);
        ctx.ClosePath();

        if (disabled)
        {
            ctx.SetSourceRGBA(0.10f, 0.10f, 0.10f, 0.80f);
            ctx.Fill();
        }
        else if (isActive)
        {
            ctx.SetSourceRGBA(0.55f, 0.40f, 0.12f, 0.90f);
            ctx.Fill();

            ctx.MoveTo(cx, cy);
            ctx.Arc(cx, cy, outerRadius, a0, a1);
            ctx.ClosePath();
            ctx.SetSourceRGBA(0.92f, 0.75f, 0.25f, 0.35f);
            ctx.Fill();
        }
        else if (hovered)
        {
            ctx.SetSourceRGBA(0.30f, 0.24f, 0.18f, 0.92f);
            ctx.Fill();

            ctx.MoveTo(cx, cy);
            ctx.Arc(cx, cy, outerRadius, a0, a1);
            ctx.ClosePath();
            ctx.SetSourceRGBA(0.77f, 0.53f, 0.29f, 0.45f);
            ctx.Fill();
        }
        else
        {
            ctx.SetSourceRGBA(0.18f, 0.14f, 0.10f, 0.88f);
            ctx.Fill();
        }

        // Border lines
        ctx.MoveTo(cx, cy);
        ctx.LineTo(cx + (float)Math.Cos(a0) * outerRadius, cy + (float)Math.Sin(a0) * outerRadius);
        ctx.MoveTo(cx, cy);
        ctx.LineTo(cx + (float)Math.Cos(a1) * outerRadius, cy + (float)Math.Sin(a1) * outerRadius);

        if (disabled)
            ctx.SetSourceRGBA(0.30f, 0.30f, 0.30f, 0.30f);
        else if (isActive)
            ctx.SetSourceRGBA(0.95f, 0.80f, 0.30f, 0.80f);
        else
            ctx.SetSourceRGBA(0.77f, 0.53f, 0.29f, 0.35f);
        ctx.LineWidth = 1.5f;
        ctx.Stroke();

        // Outer rim
        ctx.Arc(cx, cy, outerRadius, a0, a1);
        if (disabled)
            ctx.SetSourceRGBA(0.30f, 0.30f, 0.30f, 0.40f);
        else if (isActive)
            ctx.SetSourceRGBA(0.95f, 0.80f, 0.30f, 0.85f);
        else
            ctx.SetSourceRGBA(0.77f, 0.53f, 0.29f, 0.55f);
        ctx.LineWidth = 1.5f;
        ctx.Stroke();
    }

    /// <inheritdoc />
    public virtual void DrawCenterButton(Context ctx, float cx, float cy, float innerRadius)
    {
        // Inner circle background
        ctx.Arc(cx, cy, innerRadius, 0, 2f * (float)Math.PI);
        ctx.SetSourceRGBA(0.12f, 0.09f, 0.06f, 0.95f);
        ctx.Fill();

        // Border
        ctx.Arc(cx, cy, innerRadius, 0, 2f * (float)Math.PI);
        ctx.SetSourceRGBA(0.77f, 0.53f, 0.29f, 0.65f);
        ctx.LineWidth = 2f;
        ctx.Stroke();

        // X symbol
        ctx.SetSourceRGBA(0.95f, 0.92f, 0.88f, 1.0f);
        ctx.LineWidth = 3f;
        ctx.LineCap = LineCap.Round;
        float xs = 9f;
        ctx.MoveTo(cx - xs, cy - xs);
        ctx.LineTo(cx + xs, cy + xs);
        ctx.MoveTo(cx + xs, cy - xs);
        ctx.LineTo(cx - xs, cy + xs);
        ctx.Stroke();
    }

    /// <inheritdoc />
    public virtual (float r, float g, float b, float a) GetIconColor(bool disabled)
    {
        if (disabled)
            return (0.35f, 0.35f, 0.35f, 0.50f);
        return (0.95f, 0.92f, 0.88f, 1.0f);
    }
}
