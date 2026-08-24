using System;
using ArcanumLib.Common;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class CleanupScopeTests
{
    [Fact]
    public void Add_ThenUse_DisposeInReverseOrder()
    {
        var disposable1 = new TrackedDisposable();
        var disposable2 = new TrackedDisposable();

        using (var scope = new CleanupScope())
        {
            scope.Add(disposable1).Add(disposable2);
        }

        Assert.True(disposable2.Disposed);
        Assert.True(disposable1.Disposed);
    }

    [Fact]
    public void Use_IsAliasForAdd()
    {
        var disposable = new TrackedDisposable();

        using var scope = new CleanupScope();
        scope.Use(disposable).Dispose();

        Assert.True(disposable.Disposed);
    }

    [Fact]
    public void AddListener_RegistersForUnregistration()
    {
        var api = Substitute.For<ICoreAPI>();
        using (var scope = new CleanupScope(api))
        {
            scope.AddListener(42);
        }

        api.Event.Received(1).UnregisterGameTickListener(42);
    }

    [Fact]
    public void AddDeferred_HandlesUnknownKeyWithoutCrashing()
    {
        var api = Substitute.For<ICoreAPI>();

        var ex = Record.Exception(() =>
        {
            using var scope = new CleanupScope(api);
            scope.AddDeferred("unknown-key");
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_SwallowsDisposableException()
    {
        var api = Substitute.For<ICoreAPI>();

        var ex = Record.Exception(() =>
        {
            using var scope = new CleanupScope(api);
            scope.Add(new ThrowingDisposable());
            scope.Add(new TrackedDisposable());
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Add_ThrowsOnNull()
    {
        using var scope = new CleanupScope();
        Assert.Throws<ArgumentNullException>(() => scope.Add(null!));
    }

    [Fact]
    public void AddAfterDispose_ThrowsObjectDisposed()
    {
        var scope = new CleanupScope();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.Add(new TrackedDisposable()));
    }

    [Fact]
    public void AddListener_Zero_ThrowsArgumentException()
    {
        using var scope = new CleanupScope();
        Assert.Throws<ArgumentException>(() => scope.AddListener(0));
    }

    [Fact]
    public void AddDeferred_Empty_ThrowsArgumentException()
    {
        using var scope = new CleanupScope();
        Assert.Throws<ArgumentException>(() => scope.AddDeferred(""));
    }

    private sealed class TrackedDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("boom");
    }
}
