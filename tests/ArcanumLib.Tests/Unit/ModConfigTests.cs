using System;
using ArcanumLib.Persistence;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ModConfigTests
{
    private class TestConfig
    {
        public int Value { get; set; }
        public string? Name { get; set; } = "default";
    }

    [Fact]
    public void Constructor_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ModConfig<TestConfig>(null!, "test.json"));
    }

    [Fact]
    public void Constructor_EmptyFilename_Throws()
    {
        var api = Substitute.For<ICoreAPI>();
        Assert.Throws<ArgumentException>(() => new ModConfig<TestConfig>(api, ""));
    }

    [Fact]
    public void Load_MissingFile_UsesDefaults()
    {
        var api = Substitute.For<ICoreAPI>();
        var config = new ModConfig<TestConfig>(api, "test.json");

        var result = config.Load();

        Assert.Equal(ConfigResultKind.DefaultsUsed, result.Kind);
        Assert.Equal("default", config.Current.Name);
    }

    [Fact]
    public void Load_ExistingFile_AppliesAndReturnsSuccess()
    {
        var api = Substitute.For<ICoreAPI>();
        var config = new ModConfig<TestConfig>(api, "test.json");
        var loaded = new TestConfig { Value = 42, Name = "loaded" };
        api.LoadModConfig<TestConfig>("test.json").Returns(loaded);

        var result = config.Load();

        Assert.Equal(ConfigResultKind.Success, result.Kind);
        Assert.Equal(42, config.Current.Value);
        Assert.Equal("loaded", config.Current.Name);
    }

    [Fact]
    public void Load_FailingValidation_UsesDefaults()
    {
        var api = Substitute.For<ICoreAPI>();
        var loaded = new TestConfig { Value = -1 };
        api.LoadModConfig<TestConfig>("test.json").Returns(loaded);

        var config = new ModConfig<TestConfig>(api, "test.json", c => c.Value >= 0);
        var result = config.Load();

        Assert.Equal(ConfigResultKind.ValidationFailed, result.Kind);
        Assert.Equal(0, config.Current.Value);
    }

    [Fact]
    public void Save_SerializesAndStores()
    {
        var api = Substitute.For<ICoreAPI>();
        var config = new ModConfig<TestConfig>(api, "test.json");
        config.Current = new TestConfig { Value = 7, Name = "save" };

        var result = config.Save();

        Assert.Equal(ConfigResultKind.Success, result.Kind);
        api.Received().StoreModConfig("test.json", Arg.Is<string>(s => s.Contains("\"Value\": 7") && s.Contains("\"save\"")));
    }

    [Fact]
    public void ToJson_RoundTrips()
    {
        var api = Substitute.For<ICoreAPI>();
        var config = new ModConfig<TestConfig>(api, "test.json");
        config.Current = new TestConfig { Value = 9, Name = "json" };

        var json = config.ToJson();

        Assert.Contains("\"Value\": 9", json);
        Assert.True(config.TryApplyJson(json));
        Assert.Equal(9, config.Current.Value);
    }

    [Fact]
    public void TryApplyJson_InvalidJson_ReturnsFalse()
    {
        var api = Substitute.For<ICoreAPI>();
        var config = new ModConfig<TestConfig>(api, "test.json");

        Assert.False(config.TryApplyJson("not json"));
    }

    [Fact]
    public void TryApplyJson_FailingValidation_ReturnsFalse()
    {
        var api = Substitute.For<ICoreAPI>();
        var config = new ModConfig<TestConfig>(api, "test.json", c => c.Value >= 0);

        Assert.False(config.TryApplyJson("{\"Value\":-5,\"Name\":\"bad\"}"));
    }
}
