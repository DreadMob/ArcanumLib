# ModDataStore

`ArcanumLib.Persistence.ModDataStore` provides versioned per-savegame storage for arbitrary C# objects. Data is serialized to JSON and saved into the current Vintage Story savegame.

## When to use it

Use `ModDataStore` when your mod needs:

- Persistent data tied to a specific savegame (not global config).
- Schema versioning so old data can be migrated when the data shape changes.
- Shared access across multiple `ModSystem` instances or consumers.
- A simple key/value store without writing custom save/load code.

## Creating a store

```csharp
var store = ModDataStore.GetOrCreate<MyData>(sapi, "mymod", "progress", dataVersion: 1);
```

The `dataVersion` argument starts at `1`. When the shape of `MyData` changes, increment the version and load code can detect/upgrade the old data.

## Reading and writing

```csharp
store.Load();     // load from disk if not already loaded
store.Data.X = 5; // mutate the typed data
store.Save();     // write to disk
```

`Data` is lazily allocated. If no save exists, the factory creates a fresh instance.

## Custom factory

Use the overload with a factory when the type needs non-default construction or when you want a `Dictionary<TKey, TValue>` as the root object:

```csharp
var store = ModDataStore.GetOrCreate(sapi, "mymod", "counters", 1, () => new Dictionary<string, int>());
```

## Global API

If `ModDataStoreModSystem` is loaded, you can omit the `sapi` argument after startup:

```csharp
var store = ModDataStore.GetOrCreate<MyData>("mymod", "progress", 1);
```

This is useful for consumers that do not have direct access to `ICoreServerAPI`.

## Integration in a ModSystem

```csharp
public class MyModSystem : ModSystem
{
    private IModDataStore<MyData>? store;

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        store = ModDataStore.GetOrCreate<MyData>(sapi, "mymod", "state", 1);
    }

    public override void OnSaveGameData()
    {
        store?.Save();
    }
}
```

## Notes

- `ModDataStore` is **server-side only** because it uses `sapi.WorldManager.SaveGame`.
- In unit tests, use `ModDataStoreInstance<T>` directly or pass `null` for `sapi` (where supported) to avoid disk access.
