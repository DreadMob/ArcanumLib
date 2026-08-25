using System;
using System.Collections.Generic;
using ArcanumLib.Core;
using ArcanumLib.Performance;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class DeferredWorkServiceTests
{
    private readonly DeferredWorkService _service = new();

    [Fact]
    public void Schedule_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Schedule("", () => { }, 100));
    }

    [Fact]
    public void Schedule_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.Schedule("key", null!, 100));
    }

    [Fact]
    public void Schedule_WithoutApi_RunsImmediately()
    {
        var fired = false;

        _service.Schedule("test1", () => fired = true, 100);

        Assert.True(fired);
    }

    [Fact]
    public void ScheduleCallback_WithoutApi_RunsImmediately()
    {
        var fired = false;

        _service.ScheduleCallback("test2", () => fired = true, 100);

        Assert.True(fired);
    }

    [Fact]
    public void ScheduleCallback_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.ScheduleCallback("", () => { }, 100));
    }

    [Fact]
    public void Coalesce_WithoutApi_RunsImmediately()
    {
        var fired = false;

        _service.Coalesce("test3", () => fired = true, 100);

        Assert.True(fired);
    }

    [Fact]
    public void Coalesce_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Coalesce("", () => { }, 100));
    }

    [Fact]
    public void AtEndOfTick_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.AtEndOfTick(null!));
    }

    [Fact]
    public void Cancel_EmptyKey_DoesNothing()
    {
        _service.Cancel("");
    }

    [Fact]
    public void IsPending_EmptyKey_ReturnsFalse()
    {
        Assert.False(_service.IsPending(""));
    }

    [Fact]
    public void CancelCallback_EmptyKey_DoesNothing()
    {
        _service.CancelCallback("");
    }

    [Fact]
    public void IsCallbackPending_EmptyKey_ReturnsFalse()
    {
        Assert.False(_service.IsCallbackPending(""));
    }

    [Fact]
    public void CancelCallbacksByPrefix_EmptyPrefix_DoesNothing()
    {
        _service.CancelCallbacksByPrefix("");
    }

    [Fact]
    public void Schedule_WithApi_DeferredExecution()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var fired = false;
            _service.Schedule("tick-test", () => fired = true, 100);

            Assert.True(_service.IsPending("tick-test"));

            helper.ElapsedMs = 200;
            helper.InvokeTick();

            Assert.True(fired);
            Assert.False(_service.IsPending("tick-test"));
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Cancel_RemovesPendingTask()
    {
        var (api, _) = CreateApi();
        _service.Start(api);
        try
        {
            _service.Schedule("cancel-test", () => { }, 1000);
            Assert.True(_service.IsPending("cancel-test"));

            _service.Cancel("cancel-test");

            Assert.False(_service.IsPending("cancel-test"));
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void ScheduleCallback_WithApi_RegistersAndFires()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var fired = false;
            _service.ScheduleCallback("cb-test", () => fired = true, 100);

            Assert.True(_service.IsCallbackPending("cb-test"));

            helper.InvokePendingCallbacks();

            Assert.True(fired);
            Assert.False(_service.IsCallbackPending("cb-test"));
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void CancelCallback_RemovesPendingCallback()
    {
        var (api, _) = CreateApi();
        _service.Start(api);
        try
        {
            _service.ScheduleCallback("cb-cancel", () => { }, 1000);
            Assert.True(_service.IsCallbackPending("cb-cancel"));

            _service.CancelCallback("cb-cancel");

            Assert.False(_service.IsCallbackPending("cb-cancel"));
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void CancelCallbacksByPrefix_RemovesMatchingCallbacks()
    {
        var (api, _) = CreateApi();
        _service.Start(api);
        try
        {
            _service.ScheduleCallback("player:1:effect", () => { }, 1000);
            _service.ScheduleCallback("player:1:buff", () => { }, 1000);
            _service.ScheduleCallback("player:2:effect", () => { }, 1000);

            _service.CancelCallbacksByPrefix("player:1:");

            Assert.False(_service.IsCallbackPending("player:1:effect"));
            Assert.False(_service.IsCallbackPending("player:1:buff"));
            Assert.True(_service.IsCallbackPending("player:2:effect"));
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Coalesce_WithApi_DeferredExecution()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var fireCount = 0;
            _service.Coalesce("coal-test", () => fireCount++, 100);
            _service.Coalesce("coal-test", () => fireCount++, 100);

            helper.ElapsedMs = 200;
            helper.InvokeTick();

            Assert.Equal(1, fireCount);
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void AtEndOfTick_WithApi_ExecutesAtEndOfTick()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var fired = false;
            _service.AtEndOfTick(() => fired = true);

            helper.InvokeTick();

            Assert.True(fired);
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Stop_ClearsAllPending()
    {
        var (api, _) = CreateApi();
        _service.Start(api);
        _service.Schedule("stop-test", () => { }, 10000);
        _service.ScheduleCallback("stop-cb", () => { }, 10000);

        _service.Stop();

        Assert.False(_service.IsPending("stop-test"));
        Assert.False(_service.IsCallbackPending("stop-cb"));
    }

    [Fact]
    public void IsEnabled_False_RunsImmediately()
    {
        var (api, _) = CreateApi();
        _service.Start(api);
        _service.IsEnabled = false;
        try
        {
            var fired = false;
            _service.Schedule("immediate-test", () => fired = true, 10000);

            Assert.True(fired);
        }
        finally
        {
            _service.IsEnabled = true;
            _service.Stop();
        }
    }

    [Fact]
    public void Schedule_SameKey_ReplacesTask()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var firstFired = false;
            var secondFired = false;

            _service.Schedule("replace-test", () => firstFired = true, 100);
            _service.Schedule("replace-test", () => secondFired = true, 100);

            helper.ElapsedMs = 200;
            helper.InvokeTick();

            Assert.False(firstFired);
            Assert.True(secondFired);
        }
        finally
        {
            _service.Stop();
        }
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
