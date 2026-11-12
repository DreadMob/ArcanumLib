using System;
using ArcanumLib.Core;
using ArcanumLib.Effects;
using ArcanumLib.Events;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

/// <summary>Singleplayer runs both sides in one process: per-side services must stay on their own side.</summary>
[Collection("ArcanumServices")]
public class SingleplayerScopeTests : IDisposable
{
    private sealed record Ping : IEvent;

    public SingleplayerScopeTests() => ArcanumRuntime.Activate();

    public void Dispose() => ArcanumRuntime.Current?.Dispose();

    [Fact]
    public void StatusEffects_EachSideOwnsItsService()
    {
        var capi = Substitute.For<ICoreClientAPI>();
        var sapi = Substitute.For<ICoreServerAPI>();
        var client = new StatusEffectModSystem();
        var server = new StatusEffectModSystem();

        client.StartClientSide(capi);
        server.StartServerSide(sapi);

        var clientService = ArcanumServices.Get<IStatusEffectService>(ArcanumServiceScope.Client);
        var serverService = ArcanumServices.Get<IStatusEffectService>(ArcanumServiceScope.Server);
        Assert.NotNull(clientService);
        Assert.NotNull(serverService);
        Assert.NotSame(clientService, serverService);

        client.Dispose();
        Assert.Null(ArcanumServices.Get<IStatusEffectService>(ArcanumServiceScope.Client));
        Assert.Same(serverService, ArcanumServices.Get<IStatusEffectService>(ArcanumServiceScope.Server));
        server.Dispose();
    }

    [Fact]
    public void EventBus_QueuesOnItsOwnSidesMainThread()
    {
        var capi = Substitute.For<ICoreClientAPI>();
        var sapi = Substitute.For<ICoreServerAPI>();
        ArcanumServices.Register<ICoreAPI>(capi, ArcanumServiceScope.Client);
        ArcanumServices.Register<ICoreAPI>(sapi, ArcanumServiceScope.Server);
        ArcanumServices.Register<ICoreAPI>(capi, ArcanumServiceScope.Global);   // the side that started last

        var bus = new EventBusService(ArcanumServiceScope.Server);
        bus.Subscribe<Ping>(_ => { });
        bus.PublishAsync(new Ping());

        // The bus reads the API as ICoreAPI, whose Event slot the side interfaces redeclare.
        ((ICoreAPI)sapi).Event.ReceivedWithAnyArgs(1).EnqueueMainThreadTask(default!, default!);
        ((ICoreAPI)capi).Event.DidNotReceiveWithAnyArgs().EnqueueMainThreadTask(default!, default!);
    }
}
