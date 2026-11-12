---
layout: default
title: Blocks
nav_order: 18
has_children: true
---

# Blocks

`ArcanumLib.Blocks` — block helpers that several mods used to ship each in their own copy.

| Helper | What it gives |
|--------|---------------|
| `arcanumlib.FacePlayer` | A horizontally orientable block turns its front to the player placing it. |
| `IMultiBlockForwarding` / `IMultiBlockForwardingBoxes` | Filler cells of a multiblock hand clicks, help, sounds and boxes to the controller. |
| `MultiblockReach` | Vanilla multiblock fillers reach 5 cells sideways and 8 up. |
| `GroundPiles` | One-per-stack items pile up to 12 high on one ground storage. |
| [Clock](Clock.md) | A data-driven clock block. |

## FacePlayer

Vanilla `HorizontalOrientable` picks the variant the player looks at, so a model authored with its front on −Z ends up turned away. `BlockBehaviorFacePlayer` places the opposite variant. Drop, pick block, schematic rotation and the placement preview work as in vanilla; multiblock footprints follow the variant and grow away from the player.

```json
"behaviors": [ { "name": "arcanumlib.FacePlayer", "properties": { "dropBlockFace": "north" } } ]
```

The variant group is `horizontalorientation` when the block has one, else `side`. A mod that used its own behavior name may register `BlockBehaviorFacePlayer` under that old name too.

## Multiblock forwarding

Declare the interfaces on the controller's block class; the default interface members do the rest. A member the class implements itself wins.

```csharp
public class BlockMyMachine : Block, IMultiBlockForwarding, IMultiBlockForwardingBoxes { }
```

- `IMultiBlockForwarding` — click start/step/stop/cancel, partial selection, pick block, interaction help and sounds on a filler cell go to the controller's own methods, with the selection moved onto the controller (hit point included).
- `IMultiBlockForwardingBoxes` — each filler shows the controller's collision and selection boxes shifted into its own frame, so the outline and the collision stay where the model is. Unlike `offsetHitboxes` this works with partial selection.
- Call the members through the interface (`((IMultiBlockInteract)block).MBOnBlockInteractStart(...)`) when you need them yourself.
- `MultiblockCells.ForController(sel, offset)` and `MultiblockCells.Shift(boxes, offset)` are the two conversions, for custom forwarding.

## MultiblockReach

Vanilla filler blocks (`game:multiblock-monolithic-{dx}-{dy}-{dz}`) exist for offsets −2…+2 sideways and −2…+3 up. `MultiblockReach` (server, after the JSON patches, before the blocktypes are read) adds the offsets up to `Side = 5` and `Up = 8` (`Down` stays 2) and drops repeated ones, so another mod patching the same states cannot produce two blocks of one code. Clients get the blocks from the server.

Dependent mods ship **no** multiblock patch of their own. A structure bigger than that needs the constants raised here.

## GroundPiles

Patterns, blueprints and other items with `maxstacksize: 1` cannot use vanilla `Stacking` (that needs one merged stack). `GroundPiles` lets them pile on one `SingleCenter` ground storage instead, opt-in per item:

```json
"attributes": {
  "groundStorageTransform": { ... },
  "groundPile": { "group": "patterns", "capacity": 12, "step": 0.0625, "fan": true }
}
```

| Key | Default | Meaning |
|-----|---------|---------|
| `group` | `""` | Only items of one group share a pile. |
| `capacity` | 12 | Items per pile (at most 12). |
| `step` | 0.0625 | Height of one layer in blocks. |
| `fan` | `false` | Each layer turns a little. |
| `rows` | none | Round items: e.g. `[4, 3, 1]` lies like a log pile, each row in the grooves of the one below (`step` = roll diameter). |
| `openOnDisplay` | `false` | An item with a `displayedShape` keeps its own shape on shelves and in display cases too. |

An item with a `displayedShape` lies with its own shape while alone and with the displayed one from the second on. A pit kiln built on a pile takes it as usual (empty grown slots are dropped first).

The patches are applied by ArcanumLib on start; a new pile item needs only the attribute. The mesh hooks sit on `BlockEntityDisplay` (ground storage does not override them), so they filter to ground storage and leave shelves and display cases alone.
