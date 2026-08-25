using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Assets;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ModAssetTests
{
    [Fact]
    public void ModAsset_Constructor_NullLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ModAsset<int>(42, null!, "mymod"));
    }

    [Fact]
    public void ModAsset_Constructor_NullSourceModId_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ModAsset<int>(42, new AssetLocation("game:foo"), null!));
    }

    [Fact]
    public void ModAsset_Constructor_PreservesValues()
    {
        var loc = new AssetLocation("game:foo");
        var asset = new ModAsset<int>(42, loc, "mymod");

        Assert.Equal(42, asset.Value);
        Assert.Same(loc, asset.Location);
        Assert.Equal("mymod", asset.SourceModId);
    }

    [Fact]
    public void RawModAsset_Constructor_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RawModAsset(null!, new AssetLocation("game:foo"), "mymod"));
    }

    [Fact]
    public void RawModAsset_Constructor_NullLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RawModAsset("text", null!, "mymod"));
    }

    [Fact]
    public void RawModAsset_Constructor_NullSourceModId_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RawModAsset("text", new AssetLocation("game:foo"), null!));
    }

    [Fact]
    public void RawModAsset_Constructor_PreservesValues()
    {
        var loc = new AssetLocation("game:foo");
        var asset = new RawModAsset("hello", loc, "mymod");

        Assert.Equal("hello", asset.Text);
        Assert.Same(loc, asset.Location);
        Assert.Equal("mymod", asset.SourceModId);
    }
}

public class MergeStrategyTests
{
    [Fact]
    public void MergeStrategy_HasExpectedValues()
    {
        Assert.Equal(0, (int)MergeStrategy.FirstWins);
        Assert.Equal(1, (int)MergeStrategy.LastWins);
    }
}

public class ModAssetLoaderTests
{
    [Fact]
    public void LoadAll_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ModAssetLoader.LoadAll<int>(null!, "config/foo").ToList());
    }

    [Fact]
    public void LoadAll_EmptyAssetPath_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentException>(() => ModAssetLoader.LoadAll<int>(api, "").ToList());
    }

    [Fact]
    public void LoadAll_WhitespaceAssetPath_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentException>(() => ModAssetLoader.LoadAll<int>(api, "   ").ToList());
    }

    [Fact]
    public void LoadAll_NullModLoader_ReturnsEmpty()
    {
        var api = Substitute.For<ICoreAPI>();
        api.ModLoader.Returns((IModLoader)null!);

        var result = ModAssetLoader.LoadAll<int>(api, "config/foo").ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void LoadAll_EmptyMods_ReturnsEmpty()
    {
        var api = Substitute.For<ICoreAPI>();
        var modLoader = Substitute.For<IModLoader>();
        modLoader.Mods.Returns(Array.Empty<Mod>());
        api.ModLoader.Returns(modLoader);

        var result = ModAssetLoader.LoadAll<int>(api, "config/foo").ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void LoadAllRaw_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ModAssetLoader.LoadAllRaw(null!, "config/foo").ToList());
    }

    [Fact]
    public void LoadAllRaw_EmptyAssetPath_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentException>(() => ModAssetLoader.LoadAllRaw(api, "").ToList());
    }

    [Fact]
    public void LoadFlatDictionary_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ModAssetLoader.LoadFlatDictionary<int>(null!, "config/foo"));
    }

    [Fact]
    public void LoadFlatDictionary_EmptyAssetPath_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentException>(() => ModAssetLoader.LoadFlatDictionary<int>(api, ""));
    }

    [Fact]
    public void LoadDictionaryBy_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ModAssetLoader.LoadDictionaryBy<int>(null!, "config/foo", v => "key"));
    }

    [Fact]
    public void LoadDictionaryBy_EmptyAssetPath_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentException>(() => ModAssetLoader.LoadDictionaryBy<int>(api, "", v => "key"));
    }

    [Fact]
    public void LoadDictionaryBy_NullKeySelector_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentNullException>(() => ModAssetLoader.LoadDictionaryBy<int>(api, "config/foo", null!));
    }

    [Fact]
    public void LoadList_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ModAssetLoader.LoadList<int>(null!, "config/foo"));
    }

    [Fact]
    public void LoadList_EmptyAssetPath_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentException>(() => ModAssetLoader.LoadList<int>(api, ""));
    }
}

public class ModAssetRegistryTests
{
    private static ICoreAPI CreateApi()
    {
        var api = Substitute.For<ICoreAPI>();
        api.ModLoader.Returns((IModLoader)null!);
        return api;
    }

    [Fact]
    public void Constructor_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ModAssetRegistry<string, int>(null!, "config/foo", a => a.Value.ToString()!));
    }

    [Fact]
    public void Constructor_EmptyAssetPath_Throws()
    {
        var api = CreateApi();
        Assert.Throws<ArgumentException>(() =>
            new ModAssetRegistry<string, int>(api, "", a => a.Value.ToString()!));
    }

    [Fact]
    public void Constructor_NullKeySelector_Throws()
    {
        var api = CreateApi();
        Assert.Throws<ArgumentNullException>(() =>
            new ModAssetRegistry<string, int>(api, "config/foo", null!));
    }

    [Fact]
    public void Constructor_LoadImmediately_LoadsEntries()
    {
        var api = CreateApi();
        var loc = new AssetLocation("game:foo");
        var asset = new ModAsset<int>(42, loc, "game");
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[] { asset });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Equal(1, registry.Count);
        Assert.True(registry.Contains("42"));
    }

    [Fact]
    public void Constructor_LoadImmediatelyFalse_DoesNotLoad()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader, loadImmediately: false);

        Assert.Equal(0, registry.Count);
        loader.DidNotReceive().Invoke(Arg.Any<ICoreAPI>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public void Reload_RebuildsEntries()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader, loadImmediately: false);

        var asset = new ModAsset<int>(99, new AssetLocation("game:bar"), "game");
        loader.Invoke(api, "config/foo", null).Returns(new[] { asset });

        registry.Reload();

        Assert.Equal(1, registry.Count);
        Assert.True(registry.Contains("99"));
    }

    [Fact]
    public void TryGet_ExistingKey_ReturnsValue()
    {
        var api = CreateApi();
        var asset = new ModAsset<int>(42, new AssetLocation("game:foo"), "game");
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[] { asset });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.True(registry.TryGet("42", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.False(registry.TryGet("missing", out _));
    }

    [Fact]
    public void TryGetAsset_ExistingKey_ReturnsEntry()
    {
        var api = CreateApi();
        var loc = new AssetLocation("game:foo");
        var asset = new ModAsset<int>(42, loc, "game");
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[] { asset });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.True(registry.TryGetAsset("42", out var entry));
        Assert.NotNull(entry);
        Assert.Equal("game", entry!.SourceModId);
    }

    [Fact]
    public void Get_ExistingKey_ReturnsValue()
    {
        var api = CreateApi();
        var asset = new ModAsset<int>(42, new AssetLocation("game:foo"), "game");
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[] { asset });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Equal(42, registry.Get("42"));
    }

    [Fact]
    public void Get_MissingKey_ReturnsDefault()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Equal(0, registry.Get("missing"));
    }

    [Fact]
    public void Contains_MissingKey_ReturnsFalse()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.False(registry.Contains("missing"));
    }

    [Fact]
    public void GetSourceMod_ExistingKey_ReturnsModId()
    {
        var api = CreateApi();
        var asset = new ModAsset<int>(42, new AssetLocation("game:foo"), "game");
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[] { asset });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Equal("game", registry.GetSourceMod("42"));
    }

    [Fact]
    public void GetSourceMod_MissingKey_ReturnsNull()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Null(registry.GetSourceMod("missing"));
    }

    [Fact]
    public void GetLocation_ExistingKey_ReturnsLocation()
    {
        var api = CreateApi();
        var loc = new AssetLocation("game:foo");
        var asset = new ModAsset<int>(42, loc, "game");
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[] { asset });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Same(loc, registry.GetLocation("42"));
    }

    [Fact]
    public void GetLocation_MissingKey_ReturnsNull()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Null(registry.GetLocation("missing"));
    }

    [Fact]
    public void Values_CachedAndRebuiltOnReload()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(42, new AssetLocation("game:foo"), "game")
        });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        var values1 = registry.Values;
        var values2 = registry.Values;
        Assert.Same(values1, values2);

        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(99, new AssetLocation("game:bar"), "game")
        });
        registry.Reload();

        var values3 = registry.Values;
        Assert.NotSame(values1, values3);
        Assert.True(values3.ContainsKey("99"));
    }

    [Fact]
    public void LastWins_OverwritesDuplicates()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(1, new AssetLocation("game:a"), "game"),
            new ModAsset<int>(2, new AssetLocation("game:b"), "game")
        });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => "same-key", mergeStrategy: MergeStrategy.LastWins, loader: loader);

        Assert.Equal(1, registry.Count);
        Assert.Equal(2, registry.Get("same-key"));
    }

    [Fact]
    public void FirstWins_KeepsFirstDuplicate()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(1, new AssetLocation("game:a"), "game"),
            new ModAsset<int>(2, new AssetLocation("game:b"), "game")
        });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => "same-key", mergeStrategy: MergeStrategy.FirstWins, loader: loader);

        Assert.Equal(1, registry.Count);
        Assert.Equal(1, registry.Get("same-key"));
    }

    [Fact]
    public void Validate_Fails_SkipsEntry()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(-1, new AssetLocation("game:a"), "game"),
            new ModAsset<int>(42, new AssetLocation("game:b"), "game")
        });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!,
            validate: a => a.Value > 0,
            loader: loader);

        Assert.Equal(1, registry.Count);
        Assert.True(registry.Contains("42"));
        Assert.False(registry.Contains("-1"));
    }

    [Fact]
    public void Initialize_CalledOnEachAsset()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(42, new AssetLocation("game:a"), "game")
        });

        ModAsset<int>? initialized = null;
        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!,
            initialize: a => initialized = a,
            loader: loader);

        Assert.NotNull(initialized);
        Assert.Equal(42, initialized!.Value);
    }

    [Fact]
    public void Initialize_Throws_SkipsEntry()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(42, new AssetLocation("game:a"), "game")
        });

        ModAsset<int>? erroredAsset = null;
        Exception? caughtEx = null;
        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!,
            initialize: _ => throw new InvalidOperationException("boom"),
            onError: (asset, ex) => { erroredAsset = asset; caughtEx = ex; },
            loader: loader);

        Assert.Equal(0, registry.Count);
        Assert.NotNull(erroredAsset);
        Assert.NotNull(caughtEx);
    }

    [Fact]
    public void KeySelector_Throws_SkipsEntry()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(42, new AssetLocation("game:a"), "game")
        });

        ModAsset<int>? erroredAsset = null;
        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => throw new InvalidOperationException("key boom"),
            onError: (asset, _) => erroredAsset = asset,
            loader: loader);

        Assert.Equal(0, registry.Count);
        Assert.NotNull(erroredAsset);
    }

    [Fact]
    public void KeySelector_ReturnsNull_SkipsEntry()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(new[]
        {
            new ModAsset<int>(42, new AssetLocation("game:a"), "game")
        });

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => (string)null!,
            loader: loader);

        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Entries_ReturnsReadOnlyDictionary()
    {
        var api = CreateApi();
        var loader = Substitute.For<System.Func<ICoreAPI, string, string?, IEnumerable<ModAsset<int>>>>();
        loader.Invoke(api, "config/foo", null).Returns(Array.Empty<ModAsset<int>>());

        var registry = new ModAssetRegistry<string, int>(
            api, "config/foo", a => a.Value.ToString()!, loader: loader);

        Assert.Empty(registry.Entries);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, ModAsset<int>>>(registry.Entries);
    }
}

