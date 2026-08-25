using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Core;
using ArcanumLib.Performance;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class DeferredWorkServiceEdgeCaseTests
{
    private readonly DeferredWorkService _service = new();

    [Fact]
    public void Coalesce_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.Coalesce("key", null!, 100));
    }

    [Fact]
    public void Schedule_NegativeDelay_ClampedToZero_WithoutApi()
    {
        var fired = false;
        _service.Schedule("neg-delay", () => fired = true, -100);
        Assert.True(fired);
    }

    [Fact]
    public void ScheduleCallback_NegativeDelay_ClampedToZero_WithoutApi()
    {
        var fired = false;
        _service.ScheduleCallback("neg-cb", () => fired = true, -100);
        Assert.True(fired);
    }

    [Fact]
    public void Coalesce_NegativeWindow_ClampedToZero_WithoutApi()
    {
        var fired = false;
        _service.Coalesce("neg-coal", () => fired = true, -100);
        Assert.True(fired);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        _service.Dispose();
        _service.Dispose();
    }

    [Fact]
    public void Stop_CalledTwice_DoesNotThrow()
    {
        _service.Stop();
        _service.Stop();
    }

    [Fact]
    public void Dispose_StopsAndClearsPending()
    {
        var (api, _) = CreateApi();
        _service.Start(api);
        _service.Schedule("dispose-test", () => { }, 10000);

        _service.Dispose();

        Assert.False(_service.IsPending("dispose-test"));
    }

    [Fact]
    public void AtEndOfTick_MultipleActions_AllExecute()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var count = 0;
            _service.AtEndOfTick(() => count++);
            _service.AtEndOfTick(() => count++);
            _service.AtEndOfTick(() => count++);

            helper.InvokeTick();

            Assert.Equal(3, count);
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Coalesce_WithMaxDelay_FiresAtMaxDelay()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var fired = false;
            helper.ElapsedMs = 1000;
            _service.Coalesce("maxdelay-test", () => fired = true, 10000, maxDelayMs: 500);

            // Max delay = 1000 + 500 = 1500ms. At 1200ms, window (10000) not reached but max delay not reached either.
            helper.ElapsedMs = 1200;
            helper.InvokeTick();
            Assert.False(fired);

            // At 1600ms, max delay (1500) is reached → fires
            helper.ElapsedMs = 1600;
            helper.InvokeTick();
            Assert.True(fired);
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Coalesce_SameKey_ReplacesAction()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var firstFired = false;
            var secondFired = false;

            _service.Coalesce("coal-replace", () => firstFired = true, 100);
            _service.Coalesce("coal-replace", () => secondFired = true, 100);

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

    [Fact]
    public void ScheduleCallback_SameKey_ReplacesCallback()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var firstFired = false;
            var secondFired = false;

            _service.ScheduleCallback("cb-replace", () => firstFired = true, 1000);
            _service.ScheduleCallback("cb-replace", () => secondFired = true, 100);

            helper.InvokePendingCallbacks();

            Assert.False(firstFired);
            Assert.True(secondFired);
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Schedule_WithApi_TaskFires_ThenRemovedFromPending()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var fired = false;
            _service.Schedule("fire-and-remove", () => fired = true, 100);

            Assert.True(_service.IsPending("fire-and-remove"));

            helper.ElapsedMs = 200;
            helper.InvokeTick();

            Assert.True(fired);
            Assert.False(_service.IsPending("fire-and-remove"));
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Coalesce_WithApi_NotFiredBeforeWindow()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            var fired = false;
            helper.ElapsedMs = 1000;
            _service.Coalesce("coal-window", () => fired = true, 500);

            helper.ElapsedMs = 1200;
            helper.InvokeTick();
            Assert.False(fired);

            helper.ElapsedMs = 1600;
            helper.InvokeTick();
            Assert.True(fired);
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void CancelCallbacksByPrefix_NoMatching_DoesNothing()
    {
        var (api, _) = CreateApi();
        _service.Start(api);
        try
        {
            _service.ScheduleCallback("other:1", () => { }, 1000);

            _service.CancelCallbacksByPrefix("player:1:");

            Assert.True(_service.IsCallbackPending("other:1"));
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void CancelCallbacksByPrefix_NullPrefix_DoesNothing()
    {
        _service.CancelCallbacksByPrefix(null!);
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        Assert.True(_service.IsEnabled);
    }

    [Fact]
    public void Client_AndServer_AreNotNull()
    {
        Assert.NotNull(_service.Client);
        Assert.NotNull(_service.Server);
    }

    [Fact]
    public void Schedule_WhitespaceKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Schedule("   ", () => { }, 100));
    }

    [Fact]
    public void ScheduleCallback_WhitespaceKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.ScheduleCallback("   ", () => { }, 100));
    }

    [Fact]
    public void Coalesce_WhitespaceKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Coalesce("   ", () => { }, 100));
    }

    [Fact]
    public void AtEndOfTick_WithApi_ExceptionInAction_DoesNotPropagate()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            _service.AtEndOfTick(() => throw new InvalidOperationException("boom"));
            _service.AtEndOfTick(() => { });

            var ex = Record.Exception(() => helper.InvokeTick());
            Assert.Null(ex);
        }
        finally
        {
            _service.Stop();
        }
    }

    [Fact]
    public void Schedule_WithApi_ExceptionInAction_DoesNotPropagate()
    {
        var (api, helper) = CreateApi();
        _service.Start(api);
        try
        {
            _service.Schedule("throwing-task", () => throw new InvalidOperationException("boom"), 100);

            helper.ElapsedMs = 200;
            var ex = Record.Exception(() => helper.InvokeTick());
            Assert.Null(ex);
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
