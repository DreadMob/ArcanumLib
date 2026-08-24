using System;
using ArcanumLib.Common;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class EventScopeTests
{
    [Fact]
    public void Add_SubscribesAndUnsubscribesInReverseOrder()
    {
        var order = new System.Collections.Generic.List<int>();

        using (var scope = new EventScope())
        {
            scope.Add(() => order.Add(1), () => order.Add(-1));
            scope.Add(() => order.Add(2), () => order.Add(-2));
        }

        Assert.Equal(new[] { 1, 2, -2, -1 }, order);
    }

    [Fact]
    public void On_IsAliasForAdd()
    {
        bool subscribed = false;
        bool unsubscribed = false;

        using (var scope = new EventScope())
        {
            scope.On(() => subscribed = true, () => unsubscribed = true);
        }

        Assert.True(subscribed);
        Assert.True(unsubscribed);
    }

    [Fact]
    public void Dispose_SwallowsUnsubscribeException()
    {
        var api = Substitute.For<ICoreAPI>();

        var ex = Record.Exception(() =>
        {
            using var scope = new EventScope(api);
            scope.Add(() => { }, () => throw new InvalidOperationException("boom"));
            scope.Add(() => { }, () => { });
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Add_NullActions_Throws()
    {
        using var scope = new EventScope();
        Assert.Throws<ArgumentNullException>(() => scope.Add(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => scope.Add(() => { }, null!));
    }

    [Fact]
    public void Add_AfterDispose_Throws()
    {
        var scope = new EventScope();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.Add(() => { }, () => { }));
    }

    [Fact]
    public void CreateEventScope_ThrowsOnNull()
    {
        ICoreAPI? api = null;
        Assert.Throws<ArgumentNullException>(() => api!.CreateEventScope());
    }

    [Fact]
    public void CreateEventScope_ReturnsScopeTiedToApi()
    {
        var api = Substitute.For<ICoreAPI>();
        using var scope = api.CreateEventScope();

        Assert.NotNull(scope);
    }
}
