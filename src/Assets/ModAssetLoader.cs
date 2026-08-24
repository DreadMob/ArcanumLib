using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ArcanumLib.Assets;

/// <summary>
/// Describes an asset loaded from a specific mod.
/// </summary>
/// <typeparam name="T">The type of the t value.</typeparam>
public sealed class ModAsset<T>
{
    /// <summary>
    /// Deserialized asset value.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Vintage Story asset location (domain:path).
    /// </summary>
    public AssetLocation Location { get; }

    /// <summary>
    /// Mod ID that supplied the asset.
    /// </summary>
    public string SourceModId { get; }

    /// <summary>Performs the mod asset operation.</summary>
    /// <param name="value">The value to set or compare.</param>
    /// <param name="location">The asset location.</param>
    /// <param name="sourceModId">The source mod id value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="location" /> is <see langword="null" />.</exception>
    public ModAsset(T value, AssetLocation location, string sourceModId)
    {
        Value = value;
        Location = location ?? throw new ArgumentNullException(nameof(location));
        SourceModId = sourceModId ?? throw new ArgumentNullException(nameof(sourceModId));
    }
}

/// <summary>
/// Raw JSON asset loaded from a mod.
/// </summary>
public sealed class RawModAsset
{
    /// <summary>
    /// Asset content as text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Vintage Story asset location (domain:path).
    /// </summary>
    public AssetLocation Location { get; }

    /// <summary>
    /// Domain / mod that supplied the asset.
    /// </summary>
    public string SourceModId { get; }

    /// <summary>Performs the raw mod asset operation.</summary>
    /// <param name="text">The text value.</param>
    /// <param name="location">The asset location.</param>
    /// <param name="sourceModId">The source mod id value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public RawModAsset(string text, AssetLocation location, string sourceModId)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        SourceModId = sourceModId ?? throw new ArgumentNullException(nameof(sourceModId));
    }
}

/// <summary>
/// Strategy for merging entries when multiple mods provide the same key.
/// </summary>
public enum MergeStrategy
{
    /// <summary>First entry wins, later duplicates are ignored.</summary>
    FirstWins,

    /// <summary>Last entry wins, later duplicates overwrite earlier ones.</summary>
    LastWins
}

/// <summary>
/// Loads assets from all loaded mods. Allows multiple mods to
/// contribute JSON definitions under the same asset path.
/// </summary>
public static class ModAssetLoader
{
    /// <summary>
    /// Loads all typed assets at <paramref name="assetPath" /> from all loaded mods.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="api">Core API.</param>
    /// <param name="assetPath">Asset path, e.g. "config/encounters".</param>
    /// <param name="sourceModId">Optional mod ID to restrict loading to one mod/pack.</param>
    /// <returns>A collection of load all values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="assetPath" /> is invalid.</exception>
    public static IEnumerable<ModAsset<T>> LoadAll<T>(ICoreAPI api, string assetPath, string? sourceModId = null)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));

        var mods = api.ModLoader?.Mods;
        if (mods == null) yield break;

        foreach (var mod in mods)
        {
            if (mod?.Info?.ModID == null) continue;
            if (sourceModId != null && !string.Equals(mod.Info.ModID, sourceModId, StringComparison.OrdinalIgnoreCase)) continue;

            IReadOnlyDictionary<AssetLocation, T>? assets;
            try
            {
                assets = api.Assets.GetMany<T>(api.Logger, assetPath, mod.Info.ModID);
            }
            catch (Exception ex)
            {
                api.Logger?.Warning("[ArcanumLib] Failed to scan '{0}' from mod '{1}': {2}", assetPath, mod.Info.ModID, ex.Message);
                continue;
            }

            if (assets == null) continue;

            foreach (var kvp in assets)
            {
                if (kvp.Value is null) continue;
                yield return new ModAsset<T>(kvp.Value, kvp.Key, mod.Info.ModID);
            }
        }
    }

    /// <summary>
    /// Loads raw JSON text for all assets at <paramref name="assetPath" />.
    /// </summary>
    /// <param name="api">The core API instance.</param>
    /// <param name="assetPath">The asset path value.</param>
    /// <param name="domain">The domain value.</param>
    /// <returns>A collection of load all raw values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="assetPath" /> is invalid.</exception>
    public static IEnumerable<RawModAsset> LoadAllRaw(ICoreAPI api, string assetPath, string? domain = null)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));

        IEnumerable<IAsset>? assets;
        try
        {
            assets = api.Assets.GetMany(assetPath, domain, loadAsset: true);
        }
        catch (Exception ex)
        {
            api.Logger?.Warning("[ArcanumLib] Failed to scan raw assets at '{0}': {1}", assetPath, ex.Message);
            yield break;
        }

        if (assets == null) yield break;

        foreach (var asset in assets)
        {
            if (asset == null || asset.Location == null) continue;

            string text;
            try
            {
                text = asset.ToText();
            }
            catch (Exception ex)
            {
                api.Logger?.Warning("[ArcanumLib] Failed to read asset '{0}': {1}", asset.Location, ex.Message);
                continue;
            }

            if (string.IsNullOrWhiteSpace(text)) continue;

            yield return new RawModAsset(text, asset.Location, asset.Location.Domain);
        }
    }

    /// <summary>
    /// Loads a flattened dictionary from all mods. Each mod may supply one or more
    /// JSON objects with string keys and <typeparamref name="TValue" /> values.
    /// </summary>
    /// <typeparam name="TValue">The type of the tvalue value.</typeparam>
    /// <param name="api">The core API instance.</param>
    /// <param name="assetPath">The asset path value.</param>
    /// <param name="mergeStrategy">The merge strategy value.</param>
    /// <returns>A dictionary of load flat dictionary.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="assetPath" /> is invalid.</exception>
    public static IReadOnlyDictionary<string, TValue> LoadFlatDictionary<TValue>(
        ICoreAPI api,
        string assetPath,
        MergeStrategy mergeStrategy = MergeStrategy.LastWins)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));

        var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in LoadAll<Dictionary<string, TValue>>(api, assetPath))
        {
            foreach (var kvp in asset.Value)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value is null) continue;

                bool exists = result.ContainsKey(kvp.Key);
                if (!exists || mergeStrategy == MergeStrategy.LastWins)
                    result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// Loads all individual items and indexes them by a key selector.
    /// Useful when each mod supplies an array or object of definitions.
    /// </summary>
    /// <typeparam name="TValue">The type of the tvalue value.</typeparam>
    /// <param name="api">The core API instance.</param>
    /// <param name="assetPath">The asset path value.</param>
    /// <param name="keySelector">The key selector value.</param>
    /// <param name="mergeStrategy">The merge strategy value.</param>
    /// <returns>A dictionary of load dictionary by.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="assetPath" /> is invalid.</exception>
    public static IReadOnlyDictionary<string, TValue> LoadDictionaryBy<TValue>(
        ICoreAPI api,
        string assetPath,
        System.Func<TValue, string> keySelector,
        MergeStrategy mergeStrategy = MergeStrategy.LastWins)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

        var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in LoadAll<TValue>(api, assetPath))
        {
            if (asset.Value is null) continue;

            string key = keySelector(asset.Value);
            if (string.IsNullOrWhiteSpace(key)) continue;

            bool exists = result.ContainsKey(key);
            if (!exists || mergeStrategy == MergeStrategy.LastWins)
                result[key] = asset.Value;
        }

        return result;
    }

    /// <summary>
    /// Loads all individual items and returns them as a list.
    /// </summary>
    /// <typeparam name="T">The type of the t value.</typeparam>
    /// <param name="api">The core API instance.</param>
    /// <param name="assetPath">The asset path value.</param>
    /// <param name="sourceModId">The source mod id value.</param>
    /// <returns>A collection of load list values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="assetPath" /> is invalid.</exception>
    public static IReadOnlyList<T> LoadList<T>(ICoreAPI api, string assetPath, string? sourceModId = null)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));

        return LoadAll<T>(api, assetPath, sourceModId)
            .Where(a => a.Value is not null)
            .Select(a => a.Value)
            .ToList();
    }
}
