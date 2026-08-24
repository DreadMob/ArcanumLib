using System;
using ArcanumLib.Core;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ArcanumLifecycleTests
{
    public ArcanumLifecycleTests()
    {
        ArcanumLifecycle.DisposeAll();
    }

    [Fact]
    public void InitializeAll_CallsInitInOrder()
    {
        var order = new System.Collections.Generic.List<string>();

        ArcanumLifecycle.Register("first", () => order.Add("first"), () => order.Add("~first"));
        ArcanumLifecycle.Register("second", () => order.Add("second"), () => order.Add("~second"));

        ArcanumLifecycle.InitializeAll();

        Assert.Equal(new[] { "first", "second" }, order);

        ArcanumLifecycle.DisposeAll();
    }

    [Fact]
    public void DisposeAll_CallsDisposeInReverseOrder()
    {
        var order = new System.Collections.Generic.List<string>();

        ArcanumLifecycle.Register("first", () => { }, () => order.Add("~first"));
        ArcanumLifecycle.Register("second", () => { }, () => order.Add("~second"));

        ArcanumLifecycle.InitializeAll();
        ArcanumLifecycle.DisposeAll();

        Assert.Equal(new[] { "~second", "~first" }, order);
    }

    [Fact]
    public void Register_AfterInitialize_InvokesInitImmediately()
    {
        bool called = false;
        ArcanumLifecycle.InitializeAll();

        ArcanumLifecycle.Register("late", () => called = true, () => { });

        Assert.True(called);

        ArcanumLifecycle.DisposeAll();
    }

    [Fact]
    public void Register_ThrowsOnNullName()
    {
        Assert.Throws<ArgumentNullException>(() => ArcanumLifecycle.Register(null!, () => { }, () => { }));
    }

    [Fact]
    public void Register_ThrowsOnNullInit()
    {
        Assert.Throws<ArgumentNullException>(() => ArcanumLifecycle.Register("x", null!, () => { }));
    }

    [Fact]
    public void Register_ThrowsOnNullDispose()
    {
        Assert.Throws<ArgumentNullException>(() => ArcanumLifecycle.Register("x", () => { }, null!));
    }

    [Fact]
    public void InitializeAll_SwallowsInitException()
    {
        var order = new System.Collections.Generic.List<string>();

        ArcanumLifecycle.Register("bad", () => throw new InvalidOperationException("boom"), () => { });
        ArcanumLifecycle.Register("good", () => order.Add("good"), () => { });

        var ex = Record.Exception(() => ArcanumLifecycle.InitializeAll());

        Assert.Null(ex);
        Assert.Equal(new[] { "good" }, order);

        ArcanumLifecycle.DisposeAll();
    }
}
