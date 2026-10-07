---
layout: default
title: Chunk grid map
parent: "Arcanum GUI Toolkit"
nav_order: 20
---

# Chunk grid map

`ArcanumLib.Gui.Map` — a top-down chunk map for dialogs (land claims, zones, region pickers).

- **`ChunkTerrainSource`** (client): 32×32 terrain thumbnails per chunk column in the vanilla world-map style. Reads the client's world-map cache (`Maps/<save>.db`, read-only connection) so explored-but-unloaded land shows, and renders loaded chunks live. Main thread only; keep one per session.
- **`GuiElementChunkGrid`**: LMB click/drag selects chunks (drag starting on a selected chunk deselects), RMB/MMB drag pans, wheel zooms. Base layer baked into one texture; only re-baked on change.

```csharp
var terrain = new ChunkTerrainSource(capi);   // once per session, Dispose on shutdown

composer.AddChunkGrid(ElementBounds.Fixed(0, 30, 600, 560), g =>
{
    g.Terrain = terrain;
    g.CenterOn(playerChunkX, playerChunkZ);
    g.IsSelectable = (cx, cz) => !IsForeign(cx, cz);
    g.PaintOverlay = (ctx, geo) => GuiElementChunkGrid.PaintCells(ctx, geo, myChunks, fill, edge, GuiElement.scaled(2));
    g.MarkerPos = () => capi.World.Player.Entity.Pos.XYZ;
    g.SelectionChanged += () => Use(g.Selection);   // keys: ChunkTerrainSource.Key(cx, cz)
    g.ViewChanged += RequestDataForNewView;
}, "map");
```

`ChunkGridGeometry` (passed to painters) maps chunk → element pixels (`X(cx)`, `Y(cz)`, `Cell`) and world blocks → pixels (`WorldX`, `WorldZ`). `PaintCells` fills a chunk set and strokes only its outer border.
