using System;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using NSubstitute;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class ModDataStoreTests : IDisposable
{
    public ModDataStoreTests()
    {
        ArcanumRuntime.Activate();
        ModDataStore.Clear();
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
        ModDataStore.Clear();
    }

    private class TestData
    {
        public int Counter { get; set; }
    }

    [Fact]
    public void GetOrCreate_Throws_WhenSapiIsNull()
    {
        Assert.Throws<ArgumentNullException>("sapi", () =>
            ModDataStore.GetOrCreate<TestData>(null!, "mod", "store"));
    }

    [Fact]
    public void GetOrCreate_Throws_WhenFactoryIsNull()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        Assert.Throws<ArgumentNullException>("factory", () =>
            ModDataStore.GetOrCreate(sapi, "mod", "store", 1, (Func<TestData>)null!));
    }

    [Theory]
    [InlineData(null, "store")]
    [InlineData("", "store")]
    [InlineData(" ", "store")]
    [InlineData("mod", null)]
    [InlineData("mod", "")]
    [InlineData("mod", " ")]
    public void GetOrCreate_Throws_WhenIdEmpty(string? modId, string? storeId)
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        Assert.Throws<ArgumentException>(() =>
            ModDataStore.GetOrCreate(sapi, modId!, storeId!, 1, () => new TestData()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetOrCreate_Throws_WhenDataVersionInvalid(int version)
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        Assert.Throws<ArgumentOutOfRangeException>("dataVersion", () =>
            ModDataStore.GetOrCreate(sapi, "mod", "store", version, () => new TestData()));
    }

    [Fact]
    public void GetOrCreate_ReturnsSameInstance_ForSameKey()
    {
        var sapi = Substitute.For<ICoreServerAPI>();

        var a = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store");
        var b = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store");

        Assert.Same(a, b);
    }

    [Fact]
    public void GetOrCreate_DifferentStoreIds_AreDifferentInstances()
    {
        var sapi = Substitute.For<ICoreServerAPI>();

        var a = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store-a");
        var b = ModDataStore.GetOrCreate<TestData>(sapi, "mod", "store-b");

        Assert.NotSame(a, b);
    }

    [Fact]
    public void GetOrCreateGlobal_Throws_WhenSapiNotSet()
    {
        // No ICoreServerAPI registered in ArcanumServices for Server scope.
        Assert.Throws<InvalidOperationException>(() =>
            ModDataStore.GetOrCreate<TestData>("mod", "store"));
    }

    [Fact]
    public void GetOrCreateGlobal_UsesRegisteredSapi()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        ArcanumServices.Register<ICoreServerAPI>(sapi, ArcanumServiceScope.Server);

        var store = ModDataStore.GetOrCreate<TestData>("mod", "store-global-2");

        Assert.NotNull(store);
        Assert.EndsWith("mod:store-global-2", store.StoreKey);
    }

    [Fact]
    public void ModDataStoreInstance_WithoutApi_LoadsAndSavesAsNoOp()
    {
        var store = new ModDataStoreInstance<TestData>(null, "mod", "store", 1, () => new TestData());

        Assert.False(store.IsLoaded);

        store.Data.Counter = 5;
        store.MarkDirty();

        Assert.True(store.IsLoaded);
        Assert.True(store.IsDirty);
        Assert.Equal(5, store.Data.Counter);

        store.Save();

        // Without a real savegame, Save cannot persist the data, so the dirty flag stays true.
        Assert.True(store.IsDirty);
    }

    [Fact]
    public void ModDataStoreInstance_Data_AutoLoads_WhenAccessed()
    {
        var store = new ModDataStoreInstance<TestData>(null, "mod", "store", 1, () => new TestData());

        var data = store.Data;

        Assert.NotNull(data);
        Assert.True(store.IsLoaded);
    }

    [Fact]
    public void ModDataStoreInstance_Throws_WhenModIdEmpty()
    {
        Assert.Throws<ArgumentException>("modId", () =>
            new ModDataStoreInstance<TestData>(null, "", "store", 1, () => new TestData()));
    }

    [Fact]
    public void ModDataStoreInstance_Throws_WhenStoreIdEmpty()
    {
        Assert.Throws<ArgumentException>("storeId", () =>
            new ModDataStoreInstance<TestData>(null, "mod", "", 1, () => new TestData()));
    }

    [Fact]
    public void ModDataStoreInstance_Throws_WhenDataVersionLessThanOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>("dataVersion", () =>
            new ModDataStoreInstance<TestData>(null, "mod", "store", 0, () => new TestData()));
    }

    [Fact]
    public void ModDataStoreInstance_Throws_WhenFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>("factory", () =>
            new ModDataStoreInstance<TestData>(null, "mod", "store", 1, (Func<TestData>)null!));
    }
}
