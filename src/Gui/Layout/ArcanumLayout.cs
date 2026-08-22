using System;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Layout;

/// <summary>
/// Layout helpers for <see cref="ElementBounds"/>.
/// Produces child bounds relative to a parent, removing manual fixedX/fixedY arithmetic.
/// </summary>
public static class ArcanumLayout
{
    /// <summary>
    /// Creates a vertical stack of child bounds with the given heights and optional gap/padding.
    /// </summary>
    public static ElementBounds[] Vertical(
        ElementBounds parent,
        params double[] heights)
    {
        return Vertical(parent, 0.0, 0.0, heights);
    }

    /// <summary>
    /// Creates a vertical stack of child bounds with the given heights, gap and optional padding.
    /// </summary>
    public static ElementBounds[] Vertical(
        ElementBounds parent,
        double gap,
        double padding,
        params double[] heights)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        var result = new ElementBounds[heights.Length];
        double y = padding;
        double innerW = Math.Max(0, parent.OuterWidth - padding * 2.0);

        for (int i = 0; i < heights.Length; i++)
        {
            double h = heights[i];
            var b = ElementBounds.Fixed(padding, y, innerW, h);
            b.WithParent(parent);
            result[i] = b;
            y += h + gap;
        }

        return result;
    }

    /// <summary>
    /// Creates a vertical stack where the last child fills the remaining height of the parent.
    /// </summary>
    public static ElementBounds[] VerticalFill(
        ElementBounds parent,
        double gap,
        double padding,
        params double[] heights)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        var result = new ElementBounds[heights.Length];
        double y = padding;
        double innerW = Math.Max(0, parent.OuterWidth - padding * 2.0);
        double totalFixed = 0.0;

        for (int i = 0; i < heights.Length - 1; i++)
        {
            totalFixed += heights[i] + gap;
        }
        double fillH = Math.Max(0, parent.OuterHeight - padding * 2.0 - totalFixed);

        for (int i = 0; i < heights.Length; i++)
        {
            double h = i == heights.Length - 1 ? fillH : heights[i];
            var b = ElementBounds.Fixed(padding, y, innerW, h);
            b.WithParent(parent);
            result[i] = b;
            y += h + gap;
        }

        return result;
    }

    /// <summary>
    /// Creates a horizontal stack of child bounds with the given widths and optional gap/padding.
    /// </summary>
    public static ElementBounds[] Horizontal(
        ElementBounds parent,
        double gap,
        double padding,
        params double[] widths)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        var result = new ElementBounds[widths.Length];
        double x = padding;
        double innerH = Math.Max(0, parent.OuterHeight - padding * 2.0);

        for (int i = 0; i < widths.Length; i++)
        {
            double w = widths[i];
            var b = ElementBounds.Fixed(x, padding, w, innerH);
            b.WithParent(parent);
            result[i] = b;
            x += w + gap;
        }

        return result;
    }

    /// <summary>
    /// Creates a horizontal stack where the last child fills the remaining width of the parent.
    /// </summary>
    public static ElementBounds[] HorizontalFill(
        ElementBounds parent,
        double gap,
        double padding,
        params double[] widths)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        var result = new ElementBounds[widths.Length];
        double x = padding;
        double innerH = Math.Max(0, parent.OuterHeight - padding * 2.0);
        double totalFixed = 0.0;

        for (int i = 0; i < widths.Length - 1; i++)
        {
            totalFixed += widths[i] + gap;
        }
        double fillW = Math.Max(0, parent.OuterWidth - padding * 2.0 - totalFixed);

        for (int i = 0; i < widths.Length; i++)
        {
            double w = i == widths.Length - 1 ? fillW : widths[i];
            var b = ElementBounds.Fixed(x, padding, w, innerH);
            b.WithParent(parent);
            result[i] = b;
            x += w + gap;
        }

        return result;
    }
}
