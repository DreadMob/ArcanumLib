using System;
using ArcanumLib.Core;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class ArcanumRuntimeTests : IDisposable
{
    public ArcanumRuntimeTests()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void Activate_CreatesRuntimeAndSetsCurrent()
    {
        var runtime = ArcanumRuntime.Activate();

        Assert.NotNull(ArcanumRuntime.Current);
        Assert.Same(runtime, ArcanumRuntime.Current);
    }

    [Fact]
    public void Activate_DisposesPreviousRuntime()
    {
        var first = ArcanumRuntime.Activate();
        first.Services.Register(new object(), ArcanumServiceScope.Global);

        var second = ArcanumRuntime.Activate();

        Assert.NotSame(first, second);
        Assert.Same(second, ArcanumRuntime.Current);
        // The first runtime's services should be disposed/cleared.
        Assert.Null(first.Services.Get<object>(ArcanumServiceScope.Global));
    }

    [Fact]
    public void Dispose_ClearsCurrent()
    {
        var runtime = ArcanumRuntime.Activate();
        Assert.NotNull(ArcanumRuntime.Current);

        runtime.Dispose();

        Assert.Null(ArcanumRuntime.Current);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var runtime = ArcanumRuntime.Activate();
        runtime.Dispose();
        runtime.Dispose(); // should not throw
    }

    [Fact]
    public void Dispose_OnlyClearsCurrentIfSelf()
    {
        var first = ArcanumRuntime.Activate();
        var second = ArcanumRuntime.Activate();

        first.Dispose(); // should not clear Current because second is active

        Assert.Same(second, ArcanumRuntime.Current);
        second.Dispose();
    }

    [Fact]
    public void Services_AreIsolatedPerRuntime()
    {
        var first = ArcanumRuntime.Activate();
        first.Services.Register(new object(), ArcanumServiceScope.Global);

        var second = ArcanumRuntime.Activate();

        Assert.Null(second.Services.Get<object>(ArcanumServiceScope.Global));
    }

    [Fact]
    public void Initialize_SetsIsInitialized()
    {
        var runtime = ArcanumRuntime.Activate();

        Assert.False(runtime.IsInitialized);
        runtime.Initialize();
        Assert.True(runtime.IsInitialized);
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        var runtime = ArcanumRuntime.Activate();
        runtime.Initialize();
        runtime.Initialize(); // should not throw or run lifecycle again
        Assert.True(runtime.IsInitialized);
    }

    [Fact]
    public void Dispose_DisposesRegisteredServices()
    {
        var runtime = ArcanumRuntime.Activate();
        var disposable = Substitute.For<IDisposable>();
        runtime.Services.Register(disposable, ArcanumServiceScope.Global);

        runtime.Dispose();

        disposable.Received(1).Dispose();
    }

    [Fact]
    public void ArcanumServices_Get_ReturnsNull_WhenNoRuntime()
    {
        ArcanumRuntime.Current?.Dispose();

        Assert.Null(ArcanumServices.Get<ICoreServerAPI>());
    }

    [Fact]
    public void ArcanumServices_Register_Throws_WhenNoRuntime()
    {
        ArcanumRuntime.Current?.Dispose();

        Assert.Throws<InvalidOperationException>(() =>
            ArcanumServices.Register(new object(), ArcanumServiceScope.Global));
    }

    [Fact]
    public void ArcanumServices_Shutdown_IsNoOp_WhenNoRuntime()
    {
        ArcanumRuntime.Current?.Dispose();
        ArcanumServices.Shutdown(); // should not throw
    }

    [Fact]
    public void ArcanumServices_DelegatesToRuntime()
    {
        var runtime = ArcanumRuntime.Activate();
        var service = new object();
        ArcanumServices.Register(service, ArcanumServiceScope.Server);

        Assert.Same(service, ArcanumServices.Get<object>(ArcanumServiceScope.Server));
        Assert.Same(service, runtime.Services.Get<object>(ArcanumServiceScope.Server));
    }

    [Fact]
    public void ScopeFor_ReturnsCorrectScope()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var capi = Substitute.For<ICoreClientAPI>();

        Assert.Equal(ArcanumServiceScope.Server, ArcanumServices.ScopeFor(sapi));
        Assert.Equal(ArcanumServiceScope.Client, ArcanumServices.ScopeFor(capi));
        Assert.Equal(ArcanumServiceScope.Global, ArcanumServices.ScopeFor(null));
    }
}
