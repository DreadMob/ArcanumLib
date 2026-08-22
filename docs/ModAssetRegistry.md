---
layout: default
title: ModAssetRegistry
parent: "ModAssetLoader"
nav_order: 1
---

# ModAssetRegistry

## What is it for?

`ArcanumLib.Assets.ModAssetRegistry<TKey, TValue>` loads typed JSON assets from all mods under a single asset path, validates them, indexes them by a key, and exposes the merged registry with source metadata. The registry can be reloaded after assets change.

## When to use it

- You need to collect and merge definitions from multiple mods or content packs.
- Entries are looked up by a code, ID, or other key.
- You want to know which mod supplied each entry and where the asset is located.
- You need validation, initialization, or custom loading hooks.
- Assets may be reloaded at runtime.

## Quick example

```csharp
var registry = new ModAssetRegistry<string, ItemDefinition>(
    sapi,
    "config/items",
    asset => asset.Value.Code);

if (registry.TryGet("sword", out var item))
{
    var sourceMod = registry.GetSourceMod("sword");
}
```

## Usage

The main entry points are the constructor and `FromChildren<TParent>`. Both support the same merge, validation, and callback options.

### Constructor

```csharp
public ModAssetRegistry(
    ICoreAPI api,
    string assetPath,
    Func<ModAsset<TValue>, TKey> keySelector,
    MergeStrategy mergeStrategy = MergeStrategy.LastWins,
    IEqualityComparer<TKey>? comparer = null,
    string? sourceModId = null,
    Func<ModAsset<TValue>, bool>? validate = null,
    Action<ModAsset<TValue>, Exception>? onError = null,
    Action<ModAsset<TValue>>? initialize = null,
    Func<ICoreAPI, string, string?, IEnumerable<ModAsset<TValue>>>? loader = null,
    bool loadImmediately = true)
```

### From a child array

Use `FromChildren` when each asset file contains a collection of child definitions, for example `config/shops.json` with a `shops` array.

```csharp
var registry = ModAssetRegistry<string, Shop>.FromChildren<ShopConfig>(
    sapi,
    "config/shops",
    parent => parent.Shops,
    asset => asset.Value.Code);
```

### Lookups

```csharp
bool found = registry.TryGet("sword", out ItemDefinition item);
bool assetFound = registry.TryGetAsset("sword", out ModAsset<ItemDefinition> asset);
ItemDefinition? value = registry.Get("sword");
bool contains = registry.Contains("sword");
string? sourceMod = registry.GetSourceMod("sword");
AssetLocation? location = registry.GetLocation("sword");
```

### Other members

| Member | Description |
| --- | --- |
| `Entries` | All loaded entries as a read-only dictionary of `ModAsset<TValue>`. |
| `Values` | Values only as a read-only dictionary keyed by the selected key. |
| `Count` | Number of loaded entries. |
| `Reload()` | Re-reads all assets and rebuilds the registry. |

## Notes

- Duplicate keys are resolved by the chosen `MergeStrategy`. `LastWins` is the default.
- `validate` and `initialize` callbacks run during `Reload`; exceptions are routed through `onError` if provided.
- If `loadImmediately` is `false`, call `Reload()` manually.
- The default loader uses `ModAssetLoader.LoadAll<TValue>`; a custom loader can be supplied.