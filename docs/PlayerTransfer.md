---
layout: default
title: Account transfer
nav_order: 43
---

# Account transfer

`ArcanumLib.PlayerTransfer` — moves all progress of one account onto another (a player changed accounts) through every mod that registered its data, merging into what the target already has.

## The command

`/arcanum transfer <from> <to> confirm` (privilege `controlserver`). Both accounts must have joined the server at least once. Without `confirm` it lists what would move; with it the transfer runs and cannot be undone. `/arcanum transfer status` lists transfers still waiting for an account to join.

1. Every registered participant moves its data at once (both accounts may be offline).
2. `ServerData.CustomPlayerData` keys under registered prefixes move.
3. Progress kept on the player **entities** (watched attributes, `WorldData` mod data) is parked in a pending store: it is lifted off the source when it next joins and merged into the target when that one joins.
4. After-transfer steps run for data outside the world (a website database).

The source ends up with nothing.

## Registering your data

Any per-player store a mod keeps must register a participant next to its data, or it silently stays on the old account:

```csharp
using ArcanumLib.PlayerTransfer;

PlayerDataTransfer.Register("mymod-reputation", ctx =>
{
    if (PlayerDataMerge.MoveEntry(store.Data.ByUid, ctx.FromUid, ctx.ToUid))
    {
        store.MarkDirty();
        ctx.Note("mymod-reputation", "moved");
    }
});
```

| Registration | For |
|--------------|-----|
| `Register(id, ctx => …)` / `Register(IPlayerDataTransfer)` | Your own stores. Order of registration = order of execution; same id replaces. A participant that throws lands in `ctx.Failures`, the rest continue. |
| `RegisterEntityAttributePrefix(prefix, fillOnly)` | Player watched attributes that are progress. This is an **allow-list**: most mod keys on a player are transient state. `fillOnly` copies only when the target has no such key (pending rewards), never merges. |
| `ExcludeEntityAttributePrefix(prefix)` | Transient keys under an allowed prefix (combat tags, bindings). |
| `RegisterEntityAttributeSelector(attrs => keys)` | Data-driven key names that no fixed prefix covers. |
| `RegisterWorldModdataKey(key)` | `IServerPlayer.WorldData` mod data; byte blobs cannot merge, the target keeps its own. |
| `RegisterCustomDataPrefix(prefix)`, `RegisterCustomDataMerger(key, merge)` | `ServerData.CustomPlayerData` keys; a merger for structured string values (`a=1;b=2`). |
| `OnAfterEntityMerge(player => …)` | Recount derived totals on the target after its attributes were merged. |
| `RegisterAfterTransfer(id, (ctx, reply) => …)` | Data outside the world. Runs on the server thread and must not block; `reply` may be called later from any thread. |

## Merge rules (`PlayerDataMerge`)

Shared by every participant, so "combine two accounts" means the same everywhere:

- numbers add up (`NumberMerge.Sum`) unless the key names a time, level, tier, record or streak → the larger (`Max`), or a "first…" timestamp → the earlier non-zero (`MinNonZero`); `Classify(key)` picks the rule by the leaf name;
- bools OR, strings keep the target's value, arrays union, objects merge recursively.

Helpers: `MergeTree(target, source)` for tree attributes, `MergeValues(target, source)` for any serializable type, `MoveEntry(dict, from, to, merge?)`, `MoveEntryInEach(outer, from, to)` (per-encounter → per-player maps), `MoveMember(set, from, to)`.

Values where lower is better (best times) or that must not add (unlock times, lockouts, dialogue flags) need an explicit merge function.
