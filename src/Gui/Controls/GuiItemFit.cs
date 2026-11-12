#nullable enable
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Draws an item stack fitted into a box by the size of its actual model. Vanilla
/// <c>RenderItemstackToGui</c> only multiplies the requested size by the guiTransform scale, so a model
/// that spans several blocks spills far out of its slot and a flat plate looks tiny. Here the model's vertices
/// are run through the same transform the GUI renderer uses (origin, scale, X/Y/Z rotation, the extra 180° X
/// flip of blocks); the size and centre are then chosen so the projected outline fills the box, and the draw is
/// clipped to it. Outlines are cached per collectible.
/// </summary>
public static class GuiItemFit
{
    private readonly record struct Extent(double MinX, double MaxX, double MinY, double MaxY);

    // keyed by collectible id, which the server assigns: cleared on world unload (ArcanumLibModSystem lifecycle)
    private static readonly Dictionary<(EnumItemClass, int, bool), Extent?> Cache = new();

    internal static int CachedCount => Cache.Count;

    internal static void RememberNoOutline(EnumItemClass cls, int id, bool spin) => Cache[(cls, id, spin)] = null;

    /// <summary>Forgets every cached outline (ids belong to the world that assigned them).</summary>
    public static void Clear() => Cache.Clear();

    /// <summary>Draw <paramref name="slot"/>'s stack (or nothing) centred in <paramref name="box"/>, its outline
    /// taking <paramref name="frac"/> of the box. <paramref name="spin"/>: slowly turning (the outline then covers
    /// every turn angle so it never grows out of the box mid-turn).</summary>
    public static void Render(ICoreClientAPI capi, ItemSlot slot, ElementBounds box, double frac, int color,
        float dt, bool shading = true, bool spin = false, bool showStackSize = true)
    {
        var stack = slot.Itemstack;
        if (stack?.Collectible == null) return;
        if (GuiOccluders.Covers(box)) return;
        box.CalcWorldBounds();
        double w = box.OuterWidth * frac, h = box.OuterHeight * frac;
        double cx = box.absX + box.OuterWidth / 2.0, cy = box.absY + box.OuterHeight / 2.0;

        var info = capi.Render.GetItemStackRenderInfo(slot, EnumItemRenderTarget.Gui, dt);
        var tf = info?.Transform ?? stack.Collectible.GuiTransform;
        var ext = tf == null ? null : OutlineOf(capi, stack, tf, spin);

        double px = cx, py = cy;
        float size;
        if (ext is { } e && e.MaxX - e.MinX > 1e-4 && e.MaxY - e.MinY > 1e-4)
        {
            size = (float)Math.Min(w / (e.MaxX - e.MinX), h / (e.MaxY - e.MinY));
            // the renderer puts the transform origin (plus its pixel translation) at posX/posY
            px = cx - (tf!.Origin.X + GuiElement.scaled(tf.Translation.X)) - size * (e.MinX + e.MaxX) / 2;
            py = cy - (tf.Origin.Y + GuiElement.scaled(tf.Translation.Y)) - size * (e.MinY + e.MaxY) / 2;
            if (stack.Class == EnumItemClass.Item) { px += 3; py += 1; }
        }
        else
        {
            var sc = tf?.ScaleXYZ;
            size = (float)(Math.Min(w, h) / Math.Max(0.01f, Math.Max(sc?.X ?? 1f, sc?.Y ?? 1f)));
        }

        capi.Render.PushScissor(box, true);
        capi.Render.RenderItemstackToGui(slot, px, py, 90, size, color, dt, shading, spin, showStackSize);
        capi.Render.PopScissor();
    }

    private static Extent? OutlineOf(ICoreClientAPI capi, ItemStack stack, ModelTransform tf, bool spin)
    {
        var key = (stack.Class, stack.Collectible.Id, spin);
        if (Cache.TryGetValue(key, out var hit)) return hit;
        Extent? ext = null;
        try
        {
            MeshData? mesh = null;
            if (stack.Collectible is Block block) mesh = capi.TesselatorManager.GetDefaultBlockMesh(block);
            else if (stack.Collectible is Item item) capi.Tesselator.TesselateItem(item, out mesh);
            if (mesh?.xyz != null && mesh.VerticesCount > 0)
                ext = Project(mesh.xyz, mesh.VerticesCount, tf, stack.Class == EnumItemClass.Block, spin);
        }
        catch (Exception ex)
        {
            capi.Logger.VerboseDebug("[ArcanumLib] no GUI outline for {0}: {1}", stack.Collectible.Code, ex.Message);
        }
        Cache[key] = ext;
        return ext;
    }

    /// <summary>Screen-space outline per unit of render size (x right, y down) of the model vertices under the GUI
    /// transform: scale · Rx(rx [+180 for blocks]) · Ry(ry + turn) · Rz(rz) · (v − origin).</summary>
    internal static (double minX, double maxX, double minY, double maxY)? ProjectRaw(float[] xyz, int count,
        float ox, float oy, float oz, float sx, float sy, float rx, float ry, float rz, bool isBlock, bool spin)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        double ax = (rx + (isBlock ? 180 : 0)) * GameMath.DEG2RAD, az = rz * GameMath.DEG2RAD;
        double cxr = Math.Cos(ax), sxr = Math.Sin(ax), czr = Math.Cos(az), szr = Math.Sin(az);
        int turns = spin ? 12 : 1;
        int step = Math.Max(1, count / 4000);                     // big models: a sample of the vertices is plenty
        for (int t = 0; t < turns; t++)
        {
            double ay = (ry + t * 30) * GameMath.DEG2RAD, cyr = Math.Cos(ay), syr = Math.Sin(ay);
            for (int i = 0; i < count; i += step)
            {
                double x = xyz[i * 3] - ox, y = xyz[i * 3 + 1] - oy, z = xyz[i * 3 + 2] - oz;
                double x1 = x * czr - y * szr, y1 = x * szr + y * czr;          // Rz
                double x2 = x1 * cyr + z * syr, z2 = -x1 * syr + z * cyr;      // Ry
                double y3 = y1 * cxr - z2 * sxr;                               // Rx
                double px = x2 * sx, py = y3 * sy;
                if (px < minX) minX = px; if (px > maxX) maxX = px;
                if (py < minY) minY = py; if (py > maxY) maxY = py;
            }
        }
        return minX <= maxX ? (minX, maxX, minY, maxY) : null;
    }

    private static Extent? Project(float[] xyz, int count, ModelTransform tf, bool isBlock, bool spin)
    {
        var r = ProjectRaw(xyz, count, tf.Origin.X, tf.Origin.Y, tf.Origin.Z, tf.ScaleXYZ.X, tf.ScaleXYZ.Y,
            tf.Rotation.X, tf.Rotation.Y, tf.Rotation.Z, isBlock, spin);
        return r is { } v ? new Extent(v.minX, v.maxX, v.minY, v.maxY) : null;
    }
}

/// <summary>
/// Screen rectangles of floating GUI layers (dropdown lists) that 3D item icons must not draw through: the GUI
/// renders models with depth, so a model in a dialog underneath pokes through a list opened over it. Owners add
/// their absolute bounds while open; <see cref="GuiItemFit"/> skips icons under them.
/// </summary>
public static class GuiOccluders
{
    private static readonly List<ElementBounds> Open = new();

    public static void Add(ElementBounds b) { if (!Open.Contains(b)) Open.Add(b); }

    public static void Remove(ElementBounds b) => Open.Remove(b);

    internal static int Count => Open.Count;

    /// <summary>Drops every open occluder (a dialog closed by world unload never removes its own).</summary>
    public static void Clear() => Open.Clear();

    public static bool Covers(ElementBounds box)
    {
        if (Open.Count == 0) return false;
        box.CalcWorldBounds();
        foreach (var o in Open)
        {
            o.CalcWorldBounds();
            if (box.absX < o.absX + o.OuterWidth && o.absX < box.absX + box.OuterWidth
                && box.absY < o.absY + o.OuterHeight && o.absY < box.absY + box.OuterHeight)
                return true;
        }
        return false;
    }
}
