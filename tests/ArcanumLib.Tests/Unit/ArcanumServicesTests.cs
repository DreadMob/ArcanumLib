using System;
using ArcanumLib.Core;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class ArcanumServicesTests : IDisposable
{
    public ArcanumServicesTests()
    {
        ArcanumServices.Shutdown();
    }

    public void Dispose()
    {
        ArcanumServices.Shutdown();
    }

    [Fact]
    public void Register_And_Get_ByScope()
    {
        var service = new object();
        ArcanumServices.Register(service, ArcanumServiceScope.Server);

        Assert.Same(service, ArcanumServices.Get<object>(ArcanumServiceScope.Server));
    }

    [Fact]
    public void Get_FallsBack_WhenScopeIsNull()
    {
        var service = new object();
        ArcanumServices.Register(service, ArcanumServiceScope.Global);

        Assert.Same(service, ArcanumServices.Get<object>());
    }

    [Fact]
    public void Get_ReturnsNull_WhenNotRegistered()
    {
        Assert.Null(ArcanumServices.Get<string>());
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNotRegistered()
    {
        Assert.False(ArcanumServices.TryGet<string>(out _));
    }

    [Fact]
    public void Unregister_DisposesService_WhenDisposable()
    {
        var service = new DisposableService();
        ArcanumServices.Register(service, ArcanumServiceScope.World);

        ArcanumServices.Unregister<DisposableService>(ArcanumServiceScope.World);

        Assert.True(service.Disposed);
    }

    [Fact]
    public void Shutdown_RemovesAllServices()
    {
        ArcanumServices.Register(new object(), ArcanumServiceScope.Global);
        ArcanumServices.Register(new object(), ArcanumServiceScope.Server);

        ArcanumServices.Shutdown();

        Assert.Null(ArcanumServices.Get<object>(ArcanumServiceScope.Global));
        Assert.Null(ArcanumServices.Get<object>(ArcanumServiceScope.Server));
    }

    [Fact]
    public void EnsureInitialized_CreatesService_WhenMissing()
    {
        var created = ArcanumServices.EnsureInitialized(() => new object(), ArcanumServiceScope.Global);

        Assert.NotNull(created);
        Assert.Same(created, ArcanumServices.EnsureInitialized(() => new object(), ArcanumServiceScope.Global));
    }

    [Fact]
    public void Register_Throws_WhenServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ArcanumServices.Register<string>(null!));
    }

    [Fact]
    public void ScopeFor_ServerApi_ReturnsServer()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        Assert.Equal(ArcanumServiceScope.Server, ArcanumServices.ScopeFor(sapi));
    }

    [Fact]
    public void ScopeFor_ClientApi_ReturnsClient()
    {
        var capi = Substitute.For<ICoreClientAPI>();
        Assert.Equal(ArcanumServiceScope.Client, ArcanumServices.ScopeFor(capi));
    }

    [Fact]
    public void ScopeFor_NullOrUnknown_ReturnsGlobal()
    {
        Assert.Equal(ArcanumServiceScope.Global, ArcanumServices.ScopeFor(null));
        Assert.Equal(ArcanumServiceScope.Global, ArcanumServices.ScopeFor(Substitute.For<ICoreAPI>()));
    }

    private sealed class DisposableService : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
