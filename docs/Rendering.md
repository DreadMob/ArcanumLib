---
layout: default
title: Rendering helpers
nav_order: 22
---

# Rendering helpers

`ArcanumLib.Rendering` — small render fixes registered by ArcanumLib, usable from JSON.

## See-through glass on entities (`ArcanumGlassShape`)

The vanilla entity renderer draws a whole entity in one opaque pass: a half-transparent pixel blends but still writes depth, so everything drawn later behind the pane (cab, passengers, other entities, water) disappears. `GlassEntityShapeRenderer` splits shape elements marked `"renderPass": 3` (Transparent) into their own mesh and draws it after the world's transparent pass — blended, without depth writes, back faces culled. Everything else renders exactly as with the vanilla `Shape` renderer.

```json
"client": { "renderer": "ArcanumGlassShape", "shape": { "base": "mymod:entity/carriage" } }
```

```json
{ "name": "window", "from": [...], "to": [...], "renderPass": 3, "faces": { ... } }
```

Entities only: chunk-meshed blocks already sort `renderPass 3` faces into the transparent pool. `GlassEntityShapeRenderer.Split(mesh)` returns the solid and glass parts of a mesh for custom renderers.

Keep the other elements free of half-transparent pixels — they still render in the opaque pass.

## Back-face culling for held items (`ArcanumCullFaces`)

```json
"behaviors": [ { "name": "ArcanumCullFaces" } ]
```

Turns on back-face culling when the item is held or lies on the ground, so an outline shell shows without its inside faces. GUI rendering is unchanged. ArcanumLib applies the same to such items lying in ground storage (client patch on `GroundStorageRenderer`).
