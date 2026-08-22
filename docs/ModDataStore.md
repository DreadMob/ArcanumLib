---
layout: default
title: ModDataStore
---

# ModDataStore

Versioned per-savegame data persistence.

## What is it for?

Use `ModDataStore` when your mod needs to save data that belongs to the current world/save, not to a global config:

- Player progression counters.
- World state, POI ownership, city treasuries.
- Item/effect cooldowns and status state.
- Any data that must survive server restarts but is tied to a save.

It is server-side only and serializes to JSON inside the savegame.

## Quick example

```csharp
using ArcanumLib.Persistence;

var store = ModDataStore.GetOrCreate<MySaveData>(sapi, "mymod", "state", 1);
store.Data.Counter++;
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
- In unit tests, create a `ModDataStoreInstance<T>` directly or pass `null` for `sapi` where supported.
- Increment `dataVersion` when the data shape changes; future migrations can check this value.
