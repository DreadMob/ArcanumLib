# ModAssetLoader

`ArcanumLib.Assets.ModAssetLoader` loads JSON configuration assets from all loaded mods. It is useful when multiple mods or content packs contribute definitions under the same asset path (for example `config/encounters`, `config/worldevents`, or `config/titles`).

## Why use it

Vintage Story's `IAssetManager.GetMany` already scans one mod at a time. `ModAssetLoader` wraps that scan so you do not have to write `foreach (var mod in api.ModLoader.Mods)` with a `try/catch` every time you load a new config type.

## Main methods

### Load typed assets from all mods

```csharp
foreach (var asset in ModAssetLoader.LoadAll<EncounterDefinition>(sapi, "config/encounters"))
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
var quizDefs = ModAssetLoader.LoadDictionaryBy<QuizDefinition>(
    api, "config/quizzes", q => q.id);
```

### Load raw JSON text

For files that need custom parsing or inspection:

```csharp
foreach (var asset in ModAssetLoader.LoadAllRaw(sapi, "config/progression"))
{
    string json = asset.Text;
    AssetLocation loc = asset.Location;
    string modId = asset.SourceModId;
}
```

### Restrict to one mod

All methods accept an optional `sourceModId` / `domain` argument:

```csharp
var seasonal = ModAssetLoader.LoadAll<BountySeasonalConfig>(api, "config/bounty/seasonal", "albase");
```

## Merge strategies

- `MergeStrategy.LastWins` — later packs overwrite earlier ones (default for `LoadFlatDictionary` and `LoadDictionaryBy`).
- `MergeStrategy.FirstWins` — the first value found is kept.

Lists are left to the caller to merge, because the right shape (concatenate, union, or keyed replace) depends on the system.

## Error handling

Per-mod scan errors and malformed files are logged with the asset path and mod ID and do not stop loading from other mods.
