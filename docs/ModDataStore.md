---
layout: default
title: ModDataStore
nav_order: 40
---

# ModDataStore

Versioned per-savegame data persistence.

## What is it for?

Use `ModDataStore` when your mod needs to save data that belongs to the current world/save, not to a global config:

- Player progression counters.
- World state, territorial ownership, faction treasuries.
- Item/effect cooldowns and status state.
- Any data that must survive server restarts but is tied to a save.

It is server-side only and serializes to JSON inside the savegame.

## Quick example

```csharp
using ArcanumLib.Persistence;

var store = ModDataStore.GetOrCreate<MySaveData>(sapi, "mymod", "state", 1);
store.Data.Counter++;
store.MarkDirty();
store.Save();
```

## Usage

### Create a store

```csharp
var store = ModDataStore.GetOrCreate<MySaveData>(sapi, "mymod", "state", dataVersion: 1);
```

`MySaveData` must have a parameterless constructor. `dataVersion` starts at `1` and is incremented when the data shape changes.

### Read and write

```csharp
store.Load();
store.Data.Counter++;
store.Save();
```

`Data` is created lazily. If no save exists, a fresh instance is returned.

### Use a custom root type

Useful when the root object is a dictionary:

```csharp
var store = ModDataStore.GetOrCreate(
    sapi, "mymod", "counters", 1,
    () => new Dictionary<string, int>());

store.Data["players"]++;
store.Save();
```

### Global API

If `ModDataStoreModSystem` is loaded, you can omit `sapi`:

```csharp
var store = ModDataStore.GetOrCreate<MySaveData>("mymod", "state", 1);
```

## ModSystem integration

```csharp
public class MyModSystem : ModSystem
{
    private IModDataStore<MySaveData>? store;

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        store = ModDataStore.GetOrCreate<MySaveData>(sapi, "mymod", "state", 1);
    }

    public override void OnSaveGameData()
    {
        store?.Save();
    }
}
```

## Notes

- `ModDataStore` is **server-side only**.
- In unit tests, register a test `ICoreServerAPI` in `ArcanumServices` under `ArcanumServiceScope.Server` (e.g., `ArcanumServices.Register<ICoreServerAPI>(sapi, ArcanumServiceScope.Server)`) or use the overload that accepts `sapi` directly.
- Increment `dataVersion` when the data shape changes; future migrations can check this value.
- `MarkDirty()` must be called when `Data` is modified, otherwise `Save()` is a no-op.
- `IsDirty` returns `true` if the store has been modified since the last successful `Save()`.

## Migrations

When you bump `dataVersion`, register a migration to transform the old loaded JSON before it is assigned to `Data`:

```csharp
store.RegisterMigration(1, 2, old =>
{
    var data = old.ToObject<MySaveData>();
    data.NewField = data.OldField;
    data.OldField = default;
    return JToken.FromObject(data);
});
```
