#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using EnumMouseButton = Vintagestory.API.Common.EnumMouseButton;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Map;

/// <summary>Cell geometry of a <see cref="GuiElementChunkGrid"/> for overlay painters (element-local, scaled pixels).</summary>
public readonly struct ChunkGridGeometry
{
    /// <summary>Cell edge in scaled pixels.</summary>
    public readonly double Cell;
    /// <summary>Visible chunk range, inclusive.</summary>
    public readonly int MinX, MinZ, MaxX, MaxZ;
    /// <summary>Element size in scaled pixels.</summary>
    public readonly double Width, Height;
    private readonly int centerX, centerZ;

    internal ChunkGridGeometry(double cell, int cx, int cz, double w, double h)
    {
        Cell = cell; centerX = cx; centerZ = cz; Width = w; Height = h;
        int rx = (int)Math.Ceiling(w / 2 / cell) + 1, rz = (int)Math.Ceiling(h / 2 / cell) + 1;
        MinX = cx - rx; MaxX = cx + rx; MinZ = cz - rz; MaxZ = cz + rz;
    }

    /// <summary>Left edge of chunk column <paramref name="cx"/>.</summary>
    public double X(int cx) => Width / 2 + (cx - centerX - 0.5) * Cell;
    /// <summary>Top edge of chunk row <paramref name="cz"/> (north is up).</summary>
    public double Y(int cz) => Height / 2 + (cz - centerZ - 0.5) * Cell;
    /// <summary>Element-local x of a world block x.</summary>
    public double WorldX(double x) => X(0) + x / ChunkTerrainSource.Size * Cell;
    /// <summary>Element-local y of a world block z.</summary>
    public double WorldZ(double z) => Y(0) + z / ChunkTerrainSource.Size * Cell;
}

/// <summary>
/// Interactive top-down chunk map for dialogs: vanilla-style terrain from a
/// <see cref="ChunkTerrainSource"/>, a pluggable overlay (claims, zones, …),
/// chunk selection and a player marker.
/// <list type="bullet">
/// <item>LMB click — toggle one chunk; LMB drag — rectangle (adds, or removes when the drag starts on a selected chunk).</item>
/// <item>RMB/MMB drag — pan (whole chunks); wheel — zoom around the cursor.</item>
/// </list>
/// The base layer is baked into one texture and re-baked only when the view,
/// selection, overlay or terrain changes; the hover outline is a separate tiny texture.
/// </summary>
public class GuiElementChunkGrid : GuiElement
{
    /// <summary>Zoom steps, unscaled pixels per chunk.</summary>
    public static readonly int[] ZoomSteps = { 6, 8, 10, 12, 16, 20, 24, 32, 40 };

    /// <summary>Terrain thumbnails; null draws the unexplored fill only.</summary>
    public ChunkTerrainSource? Terrain { get; set; }
    /// <summary>Chunk at the centre of the element.</summary>
    public int CenterX { get; private set; }
    /// <inheritdoc cref="CenterX"/>
    public int CenterZ { get; private set; }
    /// <summary>Unscaled pixels per chunk (one of <see cref="ZoomSteps"/>).</summary>
    public int CellPx { get; private set; } = 16;
    /// <summary>Selected chunk keys (<see cref="ChunkTerrainSource.Key"/>). Mutate via the methods so the view re-bakes.</summary>
    public IReadOnlyCollection<long> Selection => selection;
    /// <summary>When false, LMB does nothing (view-only map).</summary>
    public bool SelectionEnabled { get; set; } = true;
    /// <summary>Optional filter: chunks for which this returns false can't be selected.</summary>
    public Func<int, int, bool>? IsSelectable { get; set; }
    /// <summary>Painted over terrain and grid, under the selection.</summary>
    public Action<Context, ChunkGridGeometry>? PaintOverlay { get; set; }
    /// <summary>Painted last, over the selection and marker.</summary>
    public Action<Context, ChunkGridGeometry>? PaintTop { get; set; }
    /// <summary>World position of the "you are here" marker; null hides it.</summary>
    public Func<Vec3d?>? MarkerPos { get; set; }
    /// <summary>Marker heading (radians, entity yaw).</summary>
    public Func<float>? MarkerYaw { get; set; }

    /// <summary>Selection changed by the user.</summary>
    public event Action? SelectionChanged;
    /// <summary>Pan/zoom finished (fires once per gesture).</summary>
    public event Action? ViewChanged;
    /// <summary>Single click on a chunk (after the selection toggle), with the chunk coordinates.</summary>
    public event Action<int, int>? CellClicked;
    /// <summary>Hovered chunk changed; null when the cursor left the map.</summary>
    public event Action<Vec2i?>? HoverChanged;

    private readonly HashSet<long> selection = new();
    private LoadedTexture baseTex;
    private LoadedTexture hoverTex;
    private bool dirty = true;
    private int bakedTerrainVersion = -1;
    private long lastBakeMs;
    private long lastRequestMs;
    private Vec2i? hover;
    private int hoverTexCell = -1;

    private bool dragging;
    private bool dragRemoves;
    private Vec2i dragFrom = new(), dragTo = new();
    private bool panning;
    private double panAccX, panAccY;
    private int lastMouseX, lastMouseY;
    private bool panMoved;
    private double markerBakedX = double.NaN, markerBakedZ = double.NaN;

    /// <summary>Creates the element; place it with <see cref="GuiComposerChunkGridExtensions.AddChunkGrid"/>.</summary>
    public GuiElementChunkGrid(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
    {
        baseTex = new LoadedTexture(capi);
        hoverTex = new LoadedTexture(capi);
    }

    /// <summary>Request a re-bake (e.g. overlay data changed).</summary>
    public void MarkDirty() => dirty = true;

    /// <summary>Center the view on a chunk.</summary>
    public void CenterOn(int cx, int cz)
    {
        if (cx == CenterX && cz == CenterZ) return;
        CenterX = cx; CenterZ = cz; dirty = true;
    }

    /// <summary>Set the zoom to the nearest step.</summary>
    public void SetZoom(int cellPx)
    {
        int best = ZoomSteps[0];
        foreach (int s in ZoomSteps) if (Math.Abs(s - cellPx) < Math.Abs(best - cellPx)) best = s;
        if (best != CellPx) { CellPx = best; dirty = true; hoverTexCell = -1; }
    }

    /// <summary>Step the zoom in (+1) or out (-1).</summary>
    public void Zoom(int dir)
    {
        int i = Array.IndexOf(ZoomSteps, CellPx);
        i = Math.Clamp((i < 0 ? 4 : i) + dir, 0, ZoomSteps.Length - 1);
        if (ZoomSteps[i] != CellPx) { CellPx = ZoomSteps[i]; dirty = true; hoverTexCell = -1; }
    }

    /// <summary>Replace the selection (no event).</summary>
    public void SetSelection(IEnumerable<long> keys) { selection.Clear(); selection.UnionWith(keys); dirty = true; }
    /// <summary>Clear the selection (no event).</summary>
    public void ClearSelection() { if (selection.Count == 0) return; selection.Clear(); dirty = true; }
    /// <summary>Whether a chunk is selected.</summary>
    public bool IsSelected(int cx, int cz) => selection.Contains(ChunkTerrainSource.Key(cx, cz));

    /// <summary>Current geometry (scaled pixels).</summary>
    public ChunkGridGeometry Geometry => new(scaled(CellPx), CenterX, CenterZ, Bounds.InnerWidth, Bounds.InnerHeight);

    // ── input ────────────────────────────────────────────────────────────

    private Vec2i CellAt(int mouseX, int mouseY)
    {
        var g = Geometry;
        double lx = mouseX - Bounds.absX - Bounds.absPaddingX, ly = mouseY - Bounds.absY - Bounds.absPaddingY;
        int cx = (int)Math.Floor((lx - g.Width / 2) / g.Cell + 0.5) + CenterX;
        int cz = (int)Math.Floor((ly - g.Height / 2) / g.Cell + 0.5) + CenterZ;
        return new Vec2i(cx, cz);
    }

    /// <inheritdoc />
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        args.Handled = true;
        lastMouseX = args.X; lastMouseY = args.Y;
        if (args.Button == EnumMouseButton.Left)
        {
            if (!SelectionEnabled) return;
            var c = CellAt(args.X, args.Y);
            dragging = true;
            dragFrom = c; dragTo = c.Copy();
            dragRemoves = IsSelected(c.X, c.Y);
            dirty = true;
        }
        else
        {
            panning = true; panMoved = false; panAccX = panAccY = 0;
        }
    }

    /// <inheritdoc />
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        bool inside = IsPositionInside(args.X, args.Y);
        var c = inside ? CellAt(args.X, args.Y) : null;
        if (!Equals2(c, hover)) { hover = c; HoverChanged?.Invoke(c); }

        if (dragging)
        {
            var to = CellAt(args.X, args.Y);
            if (to.X != dragTo.X || to.Y != dragTo.Y) { dragTo = to; dirty = true; }
        }
        else if (panning)
        {
            double cell = scaled(CellPx);
            panAccX += args.X - lastMouseX; panAccY += args.Y - lastMouseY;
            int sx = (int)(panAccX / cell), sz = (int)(panAccY / cell);
            if (sx != 0 || sz != 0)
            {
                CenterX -= sx; CenterZ -= sz;
                panAccX -= sx * cell; panAccY -= sz * cell;
                panMoved = true; dirty = true;
            }
        }
        lastMouseX = args.X; lastMouseY = args.Y;
    }

    /// <inheritdoc />
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (dragging)
        {
            dragging = false;
            bool single = dragFrom.X == dragTo.X && dragFrom.Y == dragTo.Y;
            bool changed = false;
            foreach (var (x, z) in Rect(dragFrom, dragTo))
            {
                long k = ChunkTerrainSource.Key(x, z);
                if (dragRemoves) changed |= selection.Remove(k);
                else if (IsSelectable?.Invoke(x, z) ?? true) changed |= selection.Add(k);
            }
            dirty = true;
            if (changed) SelectionChanged?.Invoke();
            if (single) CellClicked?.Invoke(dragFrom.X, dragFrom.Y);
            args.Handled = true;
        }
        if (panning)
        {
            panning = false;
            if (panMoved) ViewChanged?.Invoke();
            args.Handled = true;
        }
        base.OnMouseUp(api, args);
    }

    /// <inheritdoc />
    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (!IsPositionInside(api.Input.MouseX, api.Input.MouseY)) return;
        var before = CellAt(api.Input.MouseX, api.Input.MouseY);
        int old = CellPx;
        Zoom(args.delta > 0 ? 1 : -1);
        if (CellPx != old)
        {
            // keep the chunk under the cursor under the cursor
            var after = CellAt(api.Input.MouseX, api.Input.MouseY);
            CenterX += before.X - after.X; CenterZ += before.Y - after.Y;
            dirty = true;
            ViewChanged?.Invoke();
        }
        args.SetHandled(true);
    }

    private static bool Equals2(Vec2i? a, Vec2i? b) => a == null ? b == null : b != null && a.X == b.X && a.Y == b.Y;

    private static IEnumerable<(int, int)> Rect(Vec2i a, Vec2i b)
    {
        for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
            for (int z = Math.Min(a.Y, b.Y); z <= Math.Max(a.Y, b.Y); z++)
                yield return (x, z);
    }

    // ── render ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { dirty = true; }

    /// <inheritdoc />
    public override void RenderInteractiveElements(float deltaTime)
    {
        long now = api.ElapsedMilliseconds;
        if (Terrain != null)
        {
            if (now - lastRequestMs > 500 || dirty)
            {
                lastRequestMs = now;
                var g = Geometry;
                for (int x = g.MinX; x <= g.MaxX; x++)
                    for (int z = g.MinZ; z <= g.MaxZ; z++)
                        Terrain.Request(x, z);
                if (MarkerPos?.Invoke() is { } mp && (Math.Abs(mp.X - markerBakedX) > 8 || Math.Abs(mp.Z - markerBakedZ) > 8)) dirty = true;
            }
            Terrain.Process(8);
            if (Terrain.Version != bakedTerrainVersion && now - lastBakeMs > 200) dirty = true;
        }

        if (dirty) { dirty = false; lastBakeMs = now; Bake(); }

        api.Render.PushScissor(Bounds, true);
        if (baseTex.TextureId > 0)
            api.Render.Render2DLoadedTexture(baseTex, (float)(Bounds.absX + Bounds.absPaddingX), (float)(Bounds.absY + Bounds.absPaddingY));
        if (hover != null && !panning)
        {
            var g = Geometry;
            int cell = (int)Math.Round(g.Cell);
            if (hoverTexCell != cell) BakeHover(cell);
            double pad = scaled(2);
            api.Render.Render2DLoadedTexture(hoverTex,
                (float)(Bounds.absX + Bounds.absPaddingX + g.X(hover.X) - pad),
                (float)(Bounds.absY + Bounds.absPaddingY + g.Y(hover.Y) - pad));
        }
        api.Render.PopScissor();
    }

    private void BakeHover(int cell)
    {
        hoverTexCell = cell;
        int pad = (int)Math.Ceiling(scaled(2));
        int s = cell + pad * 2;
        using var surf = new ImageSurface(Format.Argb32, s, s);
        using var ctx = new Context(surf);
        var p = ArcanumGuiTheme.Palette;
        ctx.SetSourceRGBA(0, 0, 0, 0.55);
        ctx.LineWidth = scaled(3);
        ctx.Rectangle(pad, pad, cell, cell);
        ctx.Stroke();
        ctx.SetSourceRGBA(p.AccentBright.R, p.AccentBright.G, p.AccentBright.B, 1);
        ctx.LineWidth = scaled(1.5);
        ctx.Rectangle(pad, pad, cell, cell);
        ctx.Stroke();
        generateTexture(surf, ref hoverTex);
    }

    private void Bake()
    {
        int w = Math.Max(1, (int)Bounds.InnerWidth), h = Math.Max(1, (int)Bounds.InnerHeight);
        var g = Geometry;
        bakedTerrainVersion = Terrain?.Version ?? -1;
        using var surf = new ImageSurface(Format.Argb32, w, h);
        using var ctx = new Context(surf);
        var p = ArcanumGuiTheme.Palette;

        // Unexplored: dark ink with a faint diagonal hatch.
        ctx.SetSourceRGBA(0.12, 0.09, 0.06, 1);
        ctx.Paint();
        ctx.SetSourceRGBA(0.32, 0.25, 0.17, 0.35);
        ctx.LineWidth = 1;
        for (double d = -h; d < w; d += scaled(7)) { ctx.MoveTo(d, h); ctx.LineTo(d + h, 0); }
        ctx.Stroke();

        // Terrain.
        if (Terrain != null)
        {
            using var tile = new ImageSurface(Format.Argb32, ChunkTerrainSource.Size, ChunkTerrainSource.Size);
            for (int x = g.MinX; x <= g.MaxX; x++)
                for (int z = g.MinZ; z <= g.MaxZ; z++)
                {
                    var px = Terrain.Get(x, z);
                    if (px == null) continue;
                    tile.Flush();
                    Marshal.Copy(px, 0, tile.DataPtr, px.Length);
                    tile.MarkDirty();
                    ctx.Save();
                    ctx.Translate(Math.Floor(g.X(x)), Math.Floor(g.Y(z)));
                    double sc = (Math.Floor(g.X(x + 1)) - Math.Floor(g.X(x))) / ChunkTerrainSource.Size;
                    double scz = (Math.Floor(g.Y(z + 1)) - Math.Floor(g.Y(z))) / ChunkTerrainSource.Size;
                    ctx.Scale(sc, scz);
                    using (var pat = new SurfacePattern(tile) { Filter = Filter.Nearest })
                    {
                        ctx.SetSource(pat);
                        ctx.Rectangle(0, 0, ChunkTerrainSource.Size, ChunkTerrainSource.Size);
                        ctx.Fill();
                    }
                    ctx.Restore();
                }
        }

        // Chunk grid.
        if (g.Cell >= scaled(9))
        {
            ctx.SetSourceRGBA(0.1, 0.07, 0.04, 0.22);
            ctx.LineWidth = 1;
            for (int x = g.MinX; x <= g.MaxX + 1; x++) { double X = Math.Floor(g.X(x)) + 0.5; ctx.MoveTo(X, 0); ctx.LineTo(X, h); }
            for (int z = g.MinZ; z <= g.MaxZ + 1; z++) { double Y = Math.Floor(g.Y(z)) + 0.5; ctx.MoveTo(0, Y); ctx.LineTo(w, Y); }
            ctx.Stroke();
        }

        PaintOverlay?.Invoke(ctx, g);

        // Selection (+ live drag preview).
        var shown = new HashSet<long>(selection);
        HashSet<long>? preview = null;
        if (dragging)
        {
            preview = new HashSet<long>();
            foreach (var (x, z) in Rect(dragFrom, dragTo))
            {
                long k = ChunkTerrainSource.Key(x, z);
                if (dragRemoves) shown.Remove(k);
                else if (IsSelectable?.Invoke(x, z) ?? true) { shown.Add(k); preview.Add(k); }
            }
        }
        PaintCells(ctx, g, shown, p.Accent.WithAlpha(0.38), p.AccentBright, scaled(2));
        if (dragging && dragRemoves)
        {
            var rem = new HashSet<long>();
            foreach (var (x, z) in Rect(dragFrom, dragTo)) if (selection.Contains(ChunkTerrainSource.Key(x, z))) rem.Add(ChunkTerrainSource.Key(x, z));
            PaintCells(ctx, g, rem, p.StatusFailed.WithAlpha(0.35), p.StatusFailed, scaled(1.5));
        }

        // You-are-here marker.
        if (MarkerPos?.Invoke() is { } m)
        {
            markerBakedX = m.X; markerBakedZ = m.Z;
            double mx = g.WorldX(m.X), my = g.WorldZ(m.Z);
            float yaw = MarkerYaw?.Invoke() ?? 0;
            double r = Math.Max(scaled(5), g.Cell * 0.32);
            ctx.Save();
            ctx.Translate(mx, my);
            ctx.Rotate(-yaw + Math.PI);   // entity yaw 0 faces +Z (south, down on the map)
            ctx.MoveTo(0, -r); ctx.LineTo(r * 0.7, r * 0.75); ctx.LineTo(0, r * 0.35); ctx.LineTo(-r * 0.7, r * 0.75); ctx.ClosePath();
            ctx.SetSourceRGBA(0.95, 0.95, 0.9, 1); ctx.FillPreserve();
            ctx.SetSourceRGBA(0.1, 0.06, 0.03, 1); ctx.LineWidth = scaled(1.5); ctx.Stroke();
            ctx.Restore();
        }

        PaintTop?.Invoke(ctx, g);

        // Inner rim.
        ctx.SetSourceRGBA(p.BorderSilver.R, p.BorderSilver.G, p.BorderSilver.B, 0.8);
        ctx.LineWidth = scaled(1.5);
        ctx.Rectangle(0.75, 0.75, w - 1.5, h - 1.5);
        ctx.Stroke();

        generateTexture(surf, ref baseTex, false);
    }

    /// <summary>
    /// Fill a chunk set and stroke only its outer boundary — helper for overlay
    /// painters (claims, zones): adjacent chunks read as one region.
    /// </summary>
    public static void PaintCells(Context ctx, ChunkGridGeometry g, ICollection<long> cells, RGBA fill, RGBA edge, double edgeWidth)
    {
        if (cells.Count == 0) return;
        ctx.SetSourceRGBA(fill.R, fill.G, fill.B, fill.A);
        foreach (long k in cells)
        {
            int x = (int)(k >> 32), z = (int)k;
            if (x < g.MinX || x > g.MaxX || z < g.MinZ || z > g.MaxZ) continue;
            ctx.Rectangle(Math.Floor(g.X(x)), Math.Floor(g.Y(z)), Math.Floor(g.X(x + 1)) - Math.Floor(g.X(x)), Math.Floor(g.Y(z + 1)) - Math.Floor(g.Y(z)));
        }
        ctx.Fill();

        ctx.SetSourceRGBA(edge.R, edge.G, edge.B, edge.A);
        ctx.LineWidth = edgeWidth;
        ctx.LineCap = LineCap.Square;
        double o = edgeWidth / 2;
        foreach (long k in cells)
        {
            int x = (int)(k >> 32), z = (int)k;
            if (x < g.MinX || x > g.MaxX || z < g.MinZ || z > g.MaxZ) continue;
            double x0 = Math.Floor(g.X(x)), x1 = Math.Floor(g.X(x + 1)), y0 = Math.Floor(g.Y(z)), y1 = Math.Floor(g.Y(z + 1));
            if (!cells.Contains(ChunkTerrainSource.Key(x, z - 1))) { ctx.MoveTo(x0 + o, y0 + o); ctx.LineTo(x1 - o, y0 + o); }
            if (!cells.Contains(ChunkTerrainSource.Key(x, z + 1))) { ctx.MoveTo(x0 + o, y1 - o); ctx.LineTo(x1 - o, y1 - o); }
            if (!cells.Contains(ChunkTerrainSource.Key(x - 1, z))) { ctx.MoveTo(x0 + o, y0 + o); ctx.LineTo(x0 + o, y1 - o); }
            if (!cells.Contains(ChunkTerrainSource.Key(x + 1, z))) { ctx.MoveTo(x1 - o, y0 + o); ctx.LineTo(x1 - o, y1 - o); }
        }
        ctx.Stroke();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        baseTex?.Dispose();
        hoverTex?.Dispose();
        base.Dispose();
    }
}

/// <summary>Composer helpers for <see cref="GuiElementChunkGrid"/>.</summary>
public static class GuiComposerChunkGridExtensions
{
    /// <summary>Adds a chunk grid map. Configure it through the returned element (via <see cref="GetChunkGrid"/>) or the <paramref name="setup"/> callback.</summary>
    public static GuiComposer AddChunkGrid(this GuiComposer composer, ElementBounds bounds, Action<GuiElementChunkGrid>? setup = null, string? key = null)
    {
        if (!composer.Composed)
        {
            var el = new GuiElementChunkGrid(composer.Api, bounds);
            setup?.Invoke(el);
            composer.AddInteractiveElement(el, key);
        }
        return composer;
    }

    /// <summary>Retrieve a chunk grid added via <see cref="AddChunkGrid"/>.</summary>
    public static GuiElementChunkGrid? GetChunkGrid(this GuiComposer composer, string key)
        => composer.GetElement(key) as GuiElementChunkGrid;
}
