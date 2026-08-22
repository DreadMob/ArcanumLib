---
layout: default
title: ModAssetLoader
---

# ModAssetLoader

## What is it for?

`ArcanumLib.Assets.ModAssetLoader` loads JSON configuration assets from all loaded mods under a single asset path. It wraps Vintage Story's per-mod `IAssetManager.GetMany` scan so you do not have to write `foreach (var mod in api.ModLoader.Mods)` with a `try/catch` every time you load a new config type.

## When to use it

- You need to collect JSON files of the same kind from every loaded mod.
- Several mods add definitions under a shared asset path and you need to enumerate or merge them.
- You want typed deserialization, a flat string-keyed dictionary, or a custom keyed index.
- You need the raw JSON text plus the source mod ID and asset location.
- You want to restrict an asset search to a single mod domain.

## Quick example

```csharp
foreach (var asset in ModAssetLoader.LoadAll<ItemDefinition>(sapi, "config/items"))
{
    var def = asset.Value;
    var sourceMod = asset.SourceModId;
    var location = asset.Location;
}
```

## Usage

### Load typed assets from all mods

```csharp
foreach (var asset in ModAssetLoader.LoadAll<ItemDefinition>(sapi, "config/items"))
{
    var def = asset.Value;
    var sourceMod = asset.SourceModId;
    var location = asset.Location;
}
```

### Load a flat dictionary

Use this when each asset file is a JSON object with string keys:

```csharp
var titles = ModAssetLoader.LoadFlatDictionary<TitleDef>(api, "config/titles");
```

Later mods overwrite earlier mods by default. Pass `MergeStrategy.FirstWins` to keep the first value.

### Load and index a list by a key

```csharp
var itemDefs = ModAssetLoader.LoadDictionaryBy<ItemDefinition>(
    api, "config/items", i => i.Code);
```

### Load raw JSON text

For files that need custom parsing or inspection:

```csharp
foreach (var asset in ModAssetLoader.LoadAllRaw(sapi, "config/custom"))
{
    string json = asset.Text;
    AssetLocation loc = asset.Location;
    string modId = asset.SourceModId;
}
```

### Restrict to one mod

All methods accept an optional `sourceModId` / `domain` argument:

```csharp
var seasonal = ModAssetLoader.LoadAll<SeasonConfig>(api, "config/seasons", "mymod");
```

## Notes

- `MergeStrategy.LastWins` is the default for `LoadFlatDictionary` and `LoadDictionaryBy`; later packs overwrite earlier ones.
- `MergeStrategy.FirstWins` keeps the first value found.
- Lists are left to the caller to merge, because the right shape (concatenate, union, or keyed replace) depends on the system.
- Per-mod scan errors and malformed files are logged with the asset path and mod ID and do not stop loading from other mods.
