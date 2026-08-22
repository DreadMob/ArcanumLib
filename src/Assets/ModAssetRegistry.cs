using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vintagestory.API.Common;

namespace ArcanumLib.Assets;

/// <summary>
/// Loads typed JSON assets from all mods under a single asset path, validates them,
/// indexes them by a key and exposes the merged registry with source metadata.
/// The registry can be reloaded after assets change.
/// </summary>
public sealed class ModAssetRegistry<TKey, TValue> where TKey : notnull
{
    private readonly ICoreAPI _api;
    private readonly string _assetPath;
    private readonly string? _sourceModId;
    private readonly System.Func<ModAsset<TValue>, TKey> _keySelector;
    private readonly MergeStrategy _mergeStrategy;
    private readonly IEqualityComparer<TKey> _comparer;
    private readonly System.Func<ModAsset<TValue>, bool>? _validate;
    private readonly System.Action<ModAsset<TValue>, Exception>? _onError;
    private readonly System.Action<ModAsset<TValue>>? _initialize;
    private readonly System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<TValue>>> _loader;

    private Dictionary<TKey, ModAsset<TValue>> _entries;

    /// <summary>
    /// All loaded entries keyed by the selected key. Includes source mod and asset location metadata.
    /// </summary>
    public IReadOnlyDictionary<TKey, ModAsset<TValue>> Entries => _entries;

    /// <summary>
    /// Values only, keyed by the selected key.
    /// </summary>
    public IReadOnlyDictionary<TKey, TValue> Values => _entries.ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value.Value,
        _comparer);

    /// <summary>
    /// Number of loaded entries.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Creates a registry and optionally loads it immediately.
    /// </summary>
    public ModAssetRegistry(
        ICoreAPI api,
        string assetPath,
        System.Func<ModAsset<TValue>, TKey> keySelector,
        MergeStrategy mergeStrategy = MergeStrategy.LastWins,
        IEqualityComparer<TKey>? comparer = null,
        string? sourceModId = null,
        System.Func<ModAsset<TValue>, bool>? validate = null,
        System.Action<ModAsset<TValue>, Exception>? onError = null,
        System.Action<ModAsset<TValue>>? initialize = null,
        System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<TValue>>>? loader = null,
        bool loadImmediately = true)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));

        if (string.IsNullOrWhiteSpace(assetPath))
            throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));

        _assetPath = assetPath;
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _mergeStrategy = mergeStrategy;
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
        _sourceModId = sourceModId;
        _validate = validate;
        _onError = onError;
        _initialize = initialize;
        _loader = loader ?? DefaultLoader;

        _entries = new Dictionary<TKey, ModAsset<TValue>>(_comparer);

        if (loadImmediately)
            Reload();
    }

    /// <summary>
    /// Reloads all assets at the configured path and rebuilds the registry.
    /// </summary>
    public void Reload()
    {
        var next = new Dictionary<TKey, ModAsset<TValue>>(_comparer);

        foreach (var asset in _loader(_api, _assetPath, _sourceModId))
        {
            if (asset is null)
                continue;

            if (_initialize != null)
            {
                try
                {
                    _initialize(asset);
                }
                catch (Exception ex)
                {
                    _onError?.Invoke(asset, ex);
                    continue;
                }
            }

            if (_validate != null)
            {
                try
                {
                    if (!_validate(asset))
                        continue;
                }
                catch (Exception ex)
                {
                    _onError?.Invoke(asset, ex);
                    continue;
                }
            }

            TKey key;
            try
            {
                key = _keySelector(asset);
            }
            catch (Exception ex)
            {
                _onError?.Invoke(asset, ex);
                continue;
            }

            if (key is null)
                continue;

            bool exists = next.ContainsKey(key);
            if (!exists || _mergeStrategy == MergeStrategy.LastWins)
                next[key] = asset;
        }

        _entries = next;
    }

    /// <summary>
    /// Tries to get a value by key.
    /// </summary>
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Tries to get the full <see cref="ModAsset{TValue}"/> entry by key.
    /// </summary>
    public bool TryGetAsset(TKey key, [NotNullWhen(true)] out ModAsset<TValue>? entry)
        => _entries.TryGetValue(key, out entry);

    /// <summary>
    /// Gets a value by key, or <c>null</c> if not found.
    /// </summary>
    public TValue? Get(TKey key)
    {
        if (_entries.TryGetValue(key, out var entry))
            return entry.Value;
        return default(TValue?);
    }

    /// <summary>
    /// Returns true when the registry contains the given key.
    /// </summary>
    public bool Contains(TKey key) => _entries.ContainsKey(key);

    /// <summary>
    /// Returns the mod ID that supplied the entry, or null if not found.
    /// </summary>
    public string? GetSourceMod(TKey key)
        => _entries.TryGetValue(key, out var entry) ? entry.SourceModId : null;

    /// <summary>
    /// Returns the asset location of the entry, or null if not found.
    /// </summary>
    public AssetLocation? GetLocation(TKey key)
        => _entries.TryGetValue(key, out var entry) ? entry.Location : null;

    /// <summary>
    /// Creates a registry from assets that contain collections of child definitions.
    /// For example, a file <c>config/shops.json</c> with a <c>shops</c> array.
    /// </summary>
    public static ModAssetRegistry<TKey, TValue> FromChildren<TParent>(
        ICoreAPI api,
        string assetPath,
        System.Func<TParent, IEnumerable<TValue?>?> childSelector,
        System.Func<ModAsset<TValue>, TKey> keySelector,
        MergeStrategy mergeStrategy = MergeStrategy.LastWins,
        IEqualityComparer<TKey>? comparer = null,
        string? sourceModId = null,
        System.Func<ModAsset<TValue>, bool>? validate = null,
        System.Action<ModAsset<TValue>, Exception>? onError = null,
        System.Action<ModAsset<TValue>>? initialize = null,
        bool loadImmediately = true)
        where TParent : notnull
    {
        if (childSelector is null)
            throw new ArgumentNullException(nameof(childSelector));

        IEnumerable<ModAsset<TValue>> ChildLoader(ICoreAPI a, string path, string? source)
        {
            foreach (var parent in ModAssetLoader.LoadAll<TParent>(a, path, source))
            {
                if (parent is null)
                    continue;

                IEnumerable<TValue?>? extracted;
                try
                {
                    extracted = childSelector(parent.Value);
                }
                catch (Exception ex)
                {
                    a.Logger?.Warning("[ArcanumLib] Failed to extract children from '{0}' (mod '{1}'): {2}",
                        parent.Location, parent.SourceModId, ex.Message);
                    continue;
                }

                if (extracted is null)
                    continue;

                foreach (var child in extracted)
                {
                    if (child is null)
                        continue;

                    yield return new ModAsset<TValue>(child, parent.Location, parent.SourceModId);
                }
            }
        }

        return new ModAssetRegistry<TKey, TValue>(
            api,
            assetPath,
            keySelector,
            mergeStrategy,
            comparer,
            sourceModId,
            validate,
            onError,
            initialize,
            ChildLoader,
            loadImmediately);
    }

    private static IEnumerable<ModAsset<TValue>> DefaultLoader(ICoreAPI api, string path, string? source)
        => ModAssetLoader.LoadAll<TValue>(api, path, source);
}
