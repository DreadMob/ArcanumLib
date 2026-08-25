using System;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using NSubstitute;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class ModDataStoreFactoryTests : IDisposable
{
    public ModDataStoreFactoryTests()
    {
        ArcanumRuntime.Activate();
        ArcanumServices.Register<ModDataStoreRegistry>(new ModDataStoreRegistry(), ArcanumServiceScope.Server);
        ModDataStore.Clear();
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void GetOrCreate_WithApi_ReturnsSameInstanceForSameKey()
    {
        var sapi = Substitute.For<ICoreServerAPI>();

        var store1 = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store1");
        var store2 = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store1");

        Assert.Same(store1, store2);
    }

    [Fact]
    public void GetOrCreate_DifferentStoreIds_ReturnDifferentInstances()
    {
        var sapi = Substitute.For<ICoreServerAPI>();

        var store1 = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store1");
        var store2 = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store2");

        Assert.NotSame(store1, store2);
    }

    [Fact]
    public void GetOrCreate_GlobalSapi_UsesRegisteredApi()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        ArcanumServices.Register<ICoreServerAPI>(sapi, ArcanumServiceScope.Server);

        var store = ModDataStore.GetOrCreate<TestData>("mod", "store-global");

        Assert.NotNull(store);
        Assert.EndsWith("mod:store-global", store.StoreKey);
    }

    [Fact]
    public void GetOrCreate_GlobalSapi_Null_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => ModDataStore.GetOrCreate<TestData>("mod", "store"));
    }

    [Fact]
    public void GetOrCreate_ValidatesInputs()
    {
        var sapi = Substitute.For<ICoreServerAPI>();

        Assert.Throws<ArgumentNullException>(() => ModDataStore.GetOrCreate<TestData>(null!, "mod", "store"));
        Assert.Throws<ArgumentException>(() => ModDataStore.GetOrCreate<TestData>(sapi, "", "store"));
        Assert.Throws<ArgumentException>(() => ModDataStore.GetOrCreate<TestData>(sapi, "mod", "  "));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store", 0));
    }

    [Fact]
    public void Clear_RemovesAllStores()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var store = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store-clear");

        ModDataStore.Clear();

        var store2 = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store-clear");
        Assert.NotSame(store, store2);
    }

    private class TestData
    {
        public int Value { get; set; }
    }
}
