using System;
using ArcanumLib.Core;
using ArcanumLib.Performance;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class ArcanumPerformanceModSystemTests : IDisposable
{
    public ArcanumPerformanceModSystemTests()
    {
        ArcanumRuntime.Activate();
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void StartServerSide_RegistersGameTimeAndStatServicesAndStartsDeferredWork()
    {
        var sapi = CreateSapi();
        RegisterDeferredWorkService();

        var system = new ArcanumPerformanceModSystem();
        system.StartServerSide(sapi);

        Assert.NotNull(ArcanumServices.Get<IGameTimeScheduler>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<GameTimeScheduler>(ArcanumServiceScope.Server));

        Assert.NotNull(ArcanumServices.Get<IStatCoalescingEngine>(ArcanumServiceScope.Server));
        Assert.NotNull(ArcanumServices.Get<StatCoalescingEngine>(ArcanumServiceScope.Server));
    }

    [Fact]
    public void StartClientSide_StartsDeferredWorkClient()
    {
        var capi = CreateCapi();
        var deferred = Substitute.For<IDeferredWorkService>();
        ArcanumServices.Register(deferred);

        var system = new ArcanumPerformanceModSystem();
        system.StartClientSide(capi);

        deferred.Received(1).Start(capi);
    }

    [Fact]
    public void Dispose_UnregistersGameTimeAndStatServices()
    {
        var sapi = CreateSapi();
        RegisterDeferredWorkService();

        var system = new ArcanumPerformanceModSystem();
        system.StartServerSide(sapi);
        system.Dispose();

        Assert.Null(ArcanumServices.Get<IGameTimeScheduler>(ArcanumServiceScope.Server));
        Assert.Null(ArcanumServices.Get<GameTimeScheduler>(ArcanumServiceScope.Server));
        Assert.Null(ArcanumServices.Get<IStatCoalescingEngine>(ArcanumServiceScope.Server));
        Assert.Null(ArcanumServices.Get<StatCoalescingEngine>(ArcanumServiceScope.Server));
    }

    private static void RegisterDeferredWorkService()
    {
        var d = new DeferredWorkService();
        ArcanumServices.Register(d);
        ArcanumServices.Register<IDeferredWorkService>(d);
    }

    private static ICoreServerAPI CreateSapi()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        sapi.Event.Returns(Substitute.For<IServerEventAPI>());
        sapi.Logger.Returns(Substitute.For<ILogger>());
        sapi.World.Returns(Substitute.For<IServerWorldAccessor>());
        return sapi;
    }

    private static ICoreClientAPI CreateCapi()
    {
        var capi = Substitute.For<ICoreClientAPI>();
        capi.Event.Returns(Substitute.For<IClientEventAPI>());
        capi.Logger.Returns(Substitute.For<ILogger>());
        capi.World.Returns(Substitute.For<IClientWorldAccessor>());
        return capi;
    }
}
