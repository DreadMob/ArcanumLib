using System.Collections.Generic;
using ArcanumLib.Performance;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class DeferredWorkTests
{
    [Fact]
    public void Schedule_EmptyKey_Throws()
    {
        UseImmediateScheduler();
        Assert.Throws<ArgumentException>(() =>
            DeferredWork.Schedule("", () => { }, 100));
    }

    [Fact]
    public void Schedule_NullAction_Throws()
    {
        UseImmediateScheduler();
        Assert.Throws<ArgumentNullException>(() =>
            DeferredWork.Schedule("key", null!, 100));
    }

    [Fact]
    public void Schedule_WithoutApi_RunsImmediately()
    {
        UseImmediateScheduler();
        var fired = false;

        DeferredWork.Schedule("test1", () => fired = true, 100);

        Assert.True(fired);
    }

    [Fact]
    public void ScheduleCallback_WithoutApi_RunsImmediately()
    {
        UseImmediateScheduler();
        var fired = false;

        DeferredWork.ScheduleCallback("test2", () => fired = true, 100);

        Assert.True(fired);
    }

    [Fact]
    public void ScheduleCallback_EmptyKey_Throws()
    {
        UseImmediateScheduler();
        Assert.Throws<ArgumentException>(() =>
            DeferredWork.ScheduleCallback("", () => { }, 100));
    }

    [Fact]
    public void Coalesce_WithoutApi_RunsImmediately()
    {
        UseImmediateScheduler();
        var fired = false;

        DeferredWork.Coalesce("test3", () => fired = true, 100);

        Assert.True(fired);
    }

    [Fact]
    public void Coalesce_EmptyKey_Throws()
    {
        UseImmediateScheduler();
        Assert.Throws<ArgumentException>(() =>
            DeferredWork.Coalesce("", () => { }, 100));
    }

    [Fact]
    public void AtEndOfTick_NullAction_Throws()
    {
        UseImmediateScheduler();
        Assert.Throws<ArgumentNullException>(() =>
            DeferredWork.AtEndOfTick(null!));
    }

    [Fact]
    public void Cancel_EmptyKey_DoesNothing()
    {
        UseImmediateScheduler();
        DeferredWork.Cancel("");
    }

    [Fact]
    public void IsPending_EmptyKey_ReturnsFalse()
    {
        UseImmediateScheduler();
        Assert.False(DeferredWork.IsPending(""));
    }

    [Fact]
    public void CancelCallback_EmptyKey_DoesNothing()
    {
        UseImmediateScheduler();
        DeferredWork.CancelCallback("");
    }

    [Fact]
    public void IsCallbackPending_EmptyKey_ReturnsFalse()
    {
        UseImmediateScheduler();
        Assert.False(DeferredWork.IsCallbackPending(""));
    }

    [Fact]
    public void CancelCallbacksByPrefix_EmptyPrefix_DoesNothing()
    {
        UseImmediateScheduler();
        DeferredWork.CancelCallbacksByPrefix("");
    }

    [Fact]
    public void Schedule_WithApi_DeferredExecution()
    {
        var (api, helper) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            var fired = false;
            DeferredWork.Schedule("tick-test", () => fired = true, 100);

            Assert.True(DeferredWork.IsPending("tick-test"));

            helper.ElapsedMs = 200;
            helper.InvokeTick();

            Assert.True(fired);
            Assert.False(DeferredWork.IsPending("tick-test"));
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void Cancel_RemovesPendingTask()
    {
        var (api, _) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            DeferredWork.Schedule("cancel-test", () => { }, 1000);
            Assert.True(DeferredWork.IsPending("cancel-test"));

            DeferredWork.Cancel("cancel-test");

            Assert.False(DeferredWork.IsPending("cancel-test"));
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void ScheduleCallback_WithApi_RegistersAndFires()
    {
        var (api, helper) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            var fired = false;
            DeferredWork.ScheduleCallback("cb-test", () => fired = true, 100);

            Assert.True(DeferredWork.IsCallbackPending("cb-test"));

            helper.InvokePendingCallbacks();

            Assert.True(fired);
            Assert.False(DeferredWork.IsCallbackPending("cb-test"));
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void CancelCallback_RemovesPendingCallback()
    {
        var (api, _) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            DeferredWork.ScheduleCallback("cb-cancel", () => { }, 1000);
            Assert.True(DeferredWork.IsCallbackPending("cb-cancel"));

            DeferredWork.CancelCallback("cb-cancel");

            Assert.False(DeferredWork.IsCallbackPending("cb-cancel"));
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void CancelCallbacksByPrefix_RemovesMatchingCallbacks()
    {
        var (api, _) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            DeferredWork.ScheduleCallback("player:1:effect", () => { }, 1000);
            DeferredWork.ScheduleCallback("player:1:buff", () => { }, 1000);
            DeferredWork.ScheduleCallback("player:2:effect", () => { }, 1000);

            DeferredWork.CancelCallbacksByPrefix("player:1:");

            Assert.False(DeferredWork.IsCallbackPending("player:1:effect"));
            Assert.False(DeferredWork.IsCallbackPending("player:1:buff"));
            Assert.True(DeferredWork.IsCallbackPending("player:2:effect"));
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void Coalesce_WithApi_DeferredExecution()
    {
        var (api, helper) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            var fireCount = 0;
            DeferredWork.Coalesce("coal-test", () => fireCount++, 100);
            DeferredWork.Coalesce("coal-test", () => fireCount++, 100);

            helper.ElapsedMs = 200;
            helper.InvokeTick();

            Assert.Equal(1, fireCount);
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void AtEndOfTick_WithApi_ExecutesAtEndOfTick()
    {
        var (api, helper) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            var fired = false;
            DeferredWork.AtEndOfTick(() => fired = true);

            helper.InvokeTick();

            Assert.True(fired);
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void Stop_ClearsAllPending()
    {
        var (api, _) = CreateApi();
        DeferredWork.Start(api);
        DeferredWork.Schedule("stop-test", () => { }, 10000);
        DeferredWork.ScheduleCallback("stop-cb", () => { }, 10000);

        DeferredWork.Stop();

        Assert.False(DeferredWork.IsPending("stop-test"));
        Assert.False(DeferredWork.IsCallbackPending("stop-cb"));
    }

    [Fact]
    public void IsEnabled_False_RunsImmediately()
    {
        var (api, _) = CreateApi();
        DeferredWork.Start(api);
        DeferredWork.IsEnabled = false;
        try
        {
            var fired = false;
            DeferredWork.Schedule("immediate-test", () => fired = true, 10000);

            Assert.True(fired);
        }
        finally
        {
            DeferredWork.IsEnabled = true;
            DeferredWork.Stop();
        }
    }

    [Fact]
    public void Schedule_SameKey_ReplacesTask()
    {
        var (api, helper) = CreateApi();
        DeferredWork.Start(api);
        try
        {
            var firstFired = false;
            var secondFired = false;

            DeferredWork.Schedule("replace-test", () => firstFired = true, 100);
            DeferredWork.Schedule("replace-test", () => secondFired = true, 100);

            helper.ElapsedMs = 200;
            helper.InvokeTick();

            Assert.False(firstFired);
            Assert.True(secondFired);
        }
        finally
        {
            DeferredWork.Stop();
        }
    }

    private static void UseImmediateScheduler()
    {
        DeferredWork.Stop();
    }

    private static (ICoreServerAPI api, EventApiHelper helper) CreateApi()
    {
        var helper = new EventApiHelper();
        var api = helper.CreateApi();
        return (api, helper);
    }

    /// <summary>
    /// Wraps an NSubstitute IEventAPI with captured tick listeners and callbacks
    /// so tests can simulate game ticks and callback firing.
    /// </summary>
    private sealed class EventApiHelper
    {
        public long ElapsedMs;
        private long _nextTickListenerId = 1;
        private long _nextCallbackId = 1;
        private readonly Dictionary<long, Action<float>> _tickListeners = new();
        private readonly Dictionary<long, Action<float>> _pendingCallbacks = new();

        public ICoreServerAPI CreateApi()
        {
            var eventApi = Substitute.For<IServerEventAPI>();

            eventApi.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>())
                .Returns(call =>
                {
                    var id = _nextTickListenerId++;
                    _tickListeners[id] = call.ArgAt<Action<float>>(0);
                    return id;
                });

            eventApi.When(e => e.UnregisterGameTickListener(Arg.Any<long>()))
                .Do(call => _tickListeners.Remove(call.ArgAt<long>(0)));

            eventApi.RegisterCallback(Arg.Any<Action<float>>(), Arg.Any<int>())
                .Returns(call =>
                {
                    var id = _nextCallbackId++;
                    _pendingCallbacks[id] = call.ArgAt<Action<float>>(0);
                    return id;
                });

            eventApi.When(e => e.UnregisterCallback(Arg.Any<long>()))
                .Do(call => _pendingCallbacks.Remove(call.ArgAt<long>(0)));

            var api = Substitute.For<ICoreServerAPI>();
            api.Event.Returns(eventApi);
            ((ICoreAPI)api).Event.Returns((IEventAPI)eventApi);

            var world = Substitute.For<IServerWorldAccessor>();
            world.ElapsedMilliseconds.Returns(_ => ElapsedMs);
            api.World.Returns(world);
            ((ICoreAPI)api).World.Returns((IWorldAccessor)world);

            var logger = Substitute.For<ILogger>();
            api.Logger.Returns(logger);

            return api;
        }

        public int TickListenerCount => _tickListeners.Count;

        public void InvokeTick()
        {
            foreach (var listener in _tickListeners.Values)
                listener(0);
        }

        public void InvokePendingCallbacks()
        {
            var snapshot = new List<Action<float>>(_pendingCallbacks.Values);
            _pendingCallbacks.Clear();
            foreach (var cb in snapshot)
                cb(0);
        }
    }
}
