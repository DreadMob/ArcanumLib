using System;
using ArcanumLib.Actions;
using ArcanumLib.Common;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using ArcanumLib.Progression;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class ArcanumDataModSystemTests : IDisposable
{
    public ArcanumDataModSystemTests()
    {
        ArcanumServices.Shutdown();
        ModDataStore.Clear();
    }

    public void Dispose()
    {
        ArcanumServices.Shutdown();
        ModDataStore.Clear();
    }

    [Fact]
    public void ShouldLoad_Server_ReturnsTrue()
    {
        var system = new ArcanumDataModSystem();
        Assert.True(system.ShouldLoad(EnumAppSide.Server));
    }

    [Fact]
    public void ShouldLoad_Client_ReturnsFalse()
    {
        var system = new ArcanumDataModSystem();
        Assert.False(system.ShouldLoad(EnumAppSide.Client));
    }

    [Fact]
    public void StartServerSide_RegistersServicesAndTrackers()
    {
        var sapi = CreateSapi();

        var system = new ArcanumDataModSystem();
        system.StartServerSide(sapi);

        Assert.Same(sapi, ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<ActionRegistryService>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<ActionExecutorService>(ArcanumServiceScope.Server));
        Assert.NotNull(PlaytimeTracker.Current);
        Assert.NotNull(PityTracker.Current);
    }

    [Fact]
    public void StartServerSide_NullApi_ThrowsArgumentNullException()
    {
        var system = new ArcanumDataModSystem();
        Assert.Throws<ArgumentNullException>(() => system.StartServerSide(null!));
    }

    [Fact]
    public void Dispose_ClearsServicesAndTrackers()
    {
        var sapi = CreateSapi();

        var system = new ArcanumDataModSystem();
        system.StartServerSide(sapi);
        system.Dispose();

        Assert.Null(ArcanumServices.Get<ICoreServerAPI>(ArcanumServiceScope.Server));
        Assert.Null(ArcanumServices.Get<ActionRegistryService>(ArcanumServiceScope.Server));
        Assert.Null(ArcanumServices.Get<ActionExecutorService>(ArcanumServiceScope.Server));
        Assert.Null(PlaytimeTracker.Current);
        Assert.Null(PityTracker.Current);
    }

    private static ICoreServerAPI CreateSapi()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        sapi.Event.Returns(Substitute.For<IServerEventAPI>());
        return sapi;
    }
}
