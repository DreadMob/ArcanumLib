using System;
using System.Threading.Tasks;
using ArcanumLib.Actions;
using ArcanumLib.Core;
using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Persistence;
using ArcanumLib.Performance;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.AtlasTests;

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class ModDataStoreAtlasTests : AtlasScenarioBase
{
    private ICoreServerAPI Sapi => (ICoreServerAPI)World.Api;

    private class TestData
    {
        public int Counter { get; set; } = 0;
        public string? Label { get; set; }
    }

    [AtlasScenario]
    public async Task ModDataStore_SaveLoad_RoundTrip()
    {
        await World.Ticks(5);

        var store = ModDataStore.GetOrCreate<TestData>(Sapi, "arcanumlib", "atlas-test", 1);
        store.Load();

        store.Data.Counter = 42;
        store.Data.Label = "hello";
        store.MarkDirty();
        store.Save();

        await World.Ticks(2);

        var store2 = ModDataStore.GetOrCreate<TestData>(Sapi, "arcanumlib", "atlas-test", 1);
        store2.Load();

        Assert.Equal(42, store2.Data.Counter);
        Assert.Equal("hello", store2.Data.Label);
    }

    [AtlasScenario]
    public async Task ModDataStore_IsLoaded_AfterLoad()
    {
        await World.Ticks(5);

        var store = ModDataStore.GetOrCreate<TestData>(Sapi, "arcanumlib", "atlas-loaded", 1);

        Assert.False(store.IsLoaded);
        store.Load();
        Assert.True(store.IsLoaded);
    }

    [AtlasScenario]
    public async Task ModDataStore_StoreKey_IsConsistent()
    {
        await World.Ticks(5);

        var store = ModDataStore.GetOrCreate<TestData>(Sapi, "arcanumlib", "atlas-key", 1);

        Assert.Equal("arcanumlib:md:arcanumlib:atlas-key", store.StoreKey);
        Assert.Equal("arcanumlib", store.ModId);
        Assert.Equal("atlas-key", store.StoreId);
        Assert.Equal(1, store.DataVersion);
    }
}

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class EventBusAtlasTests : AtlasScenarioBase
{
    public record TestEvent : IEvent
    {
        public string Message { get; init; } = "";
    }

    [AtlasScenario]
    public async Task EventBus_Publish_DeliversToSubscriber()
    {
        await World.Ticks(5);

        var bus = ArcanumServices.Get<IEventBusService>();
        Assert.NotNull(bus);

        TestEvent? received = null;
        using var sub = bus!.Subscribe<TestEvent>(e => received = e);

        bus.Publish(new TestEvent { Message = "hello atlas" });

        await World.Ticks(2);

        Assert.NotNull(received);
        Assert.Equal("hello atlas", received!.Message);
    }

    [AtlasScenario]
    public async Task EventBus_Unsubscribe_StopsDelivery()
    {
        await World.Ticks(5);

        var bus = ArcanumServices.Get<IEventBusService>();
        Assert.NotNull(bus);

        int count = 0;
        var sub = bus!.Subscribe<TestEvent>(_ => count++);

        bus.Publish(new TestEvent());
        await World.Ticks(1);
        Assert.Equal(1, count);

        sub.Dispose();

        bus.Publish(new TestEvent());
        await World.Ticks(1);
        Assert.Equal(1, count);
    }

    [AtlasScenario]
    public async Task EventBus_TaggedSubscription_DeliversToCorrectTag()
    {
        await World.Ticks(5);

        var bus = ArcanumServices.Get<IEventBusService>();
        Assert.NotNull(bus);

        string? taggedReceived = null;
        string? untaggedReceived = null;

        using var taggedSub = bus!.Subscribe<TestEvent>("mytag", e => taggedReceived = e.Message);
        using var untaggedSub = bus.Subscribe<TestEvent>(e => untaggedReceived = e.Message);

        bus.Publish("mytag", new TestEvent { Message = "tagged" });
        await World.Ticks(1);

        Assert.Equal("tagged", taggedReceived);
        Assert.Null(untaggedReceived);
    }

    [AtlasScenario]
    public async Task EventBus_ActiveSubscriptionCount_TracksSubscriptions()
    {
        await World.Ticks(5);

        var bus = ArcanumServices.Get<IEventBusService>();
        Assert.NotNull(bus);

        int before = bus!.ActiveSubscriptionCount();

        var sub = bus.Subscribe<TestEvent>(_ => { });
        await World.Ticks(1);

        Assert.Equal(before + 1, bus.ActiveSubscriptionCount());

        sub.Dispose();
        await World.Ticks(1);

        Assert.Equal(before, bus.ActiveSubscriptionCount());
    }
}

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class StatusEffectAtlasTests : AtlasScenarioBase
{
    private ICoreServerAPI Sapi => (ICoreServerAPI)World.Api;

    private class TestEffect : IStatusEffect
    {
        public string Code => "atlas-test-effect";
        public EnumStackMode StackMode => EnumStackMode.Refresh;
        public int MaxStacks => 1;
        public bool PersistThroughDeath => false;
        public EffectCategory Category => EffectCategory.Buff;
        public IReadOnlyCollection<string> Tags => new[] { "test" };
        public bool HasTick => false;

        public void OnApply(Entity entity, IStatusEffectInstance instance) { }
        public void OnRemove(Entity entity, IStatusEffectInstance instance) { }
        public void OnTick(Entity entity, IStatusEffectInstance instance, float dt) { }
    }

    [AtlasScenario]
    public async Task StatusEffectService_ApplyAndRemove_OnRealEntity()
    {
        await World.Ticks(5);

        var service = ArcanumServices.Get<IStatusEffectService>();
        Assert.NotNull(service);

        var player = await World.JoinPlayer("se-test");
        var entity = player.Entity;
        Assert.NotNull(entity);

        var effect = new TestEffect();
        var instance = service!.Apply(entity, effect, 5000f);

        Assert.NotNull(instance);
        Assert.True(service.Has(entity, "atlas-test-effect"));

        Assert.True(service.Remove(entity, "atlas-test-effect"));
        Assert.False(service.Has(entity, "atlas-test-effect"));
    }

    [AtlasScenario]
    public async Task StatusEffectService_GetActive_ReturnsAppliedEffects()
    {
        await World.Ticks(5);

        var service = ArcanumServices.Get<IStatusEffectService>();
        Assert.NotNull(service);

        var player = await World.JoinPlayer("se-active");
        var entity = player.Entity;

        service!.Apply(entity, new TestEffect(), 10000f);

        var active = service.GetActive(entity);
        Assert.Single(active);
        Assert.Equal("atlas-test-effect", active.First().Code);
    }

    [AtlasScenario]
    public async Task StatusEffectService_RemoveAll_ClearsAllEffects()
    {
        await World.Ticks(5);

        var service = ArcanumServices.Get<IStatusEffectService>();
        Assert.NotNull(service);

        var player = await World.JoinPlayer("se-rm");
        var entity = player.Entity;

        service!.Apply(entity, new TestEffect(), 10000f);

        Assert.True(service.RemoveAll(entity));
        Assert.Empty(service.GetActive(entity));
    }
}

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class ActionRegistryAtlasTests : AtlasScenarioBase
{
    private ICoreServerAPI Sapi => (ICoreServerAPI)World.Api;

    private class TestActionHandler : IActionHandler
    {
        public string Id => "atlas:test-action";
        public bool WasExecuted { get; private set; }
        public ActionContext? LastContext { get; private set; }

        public bool IsAvailable(ActionContext context) => true;

        public ActionResult Execute(ActionContext context)
        {
            WasExecuted = true;
            LastContext = context;
            return ActionResult.Success();
        }
    }

    private class UnavailableHandler : IActionHandler
    {
        public string Id => "atlas:unavailable";
        public bool IsAvailable(ActionContext context) => false;
        public ActionResult Execute(ActionContext context) => ActionResult.Success();
    }

    [AtlasScenario]
    public async Task ActionRegistry_RegisterAndGet_RoundTrip()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();
        var handler = new TestActionHandler();

        registry.Register(handler);

        Assert.True(registry.IsRegistered("atlas:test-action"));
        Assert.Same(handler, registry.GetHandler("atlas:test-action"));
    }

    [AtlasScenario]
    public async Task ActionRegistry_Execute_RunsHandler()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();
        var handler = new TestActionHandler();
        registry.Register(handler);

        var descriptor = new ActionDescriptor { Id = "atlas:test-action" };
        var context = new ActionContext(Sapi);

        var result = registry.Execute(descriptor, context);

        Assert.True(result.IsSuccess);
        Assert.True(handler.WasExecuted);
    }

    [AtlasScenario]
    public async Task ActionRegistry_Execute_Unavailable_ReturnsNotAvailable()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();
        registry.Register(new UnavailableHandler());

        var descriptor = new ActionDescriptor { Id = "atlas:unavailable" };
        var context = new ActionContext(Sapi);

        var result = registry.Execute(descriptor, context);

        Assert.False(result.IsSuccess);
    }

    [AtlasScenario]
    public async Task ActionRegistry_Execute_UnknownHandler_ReturnsHandlerNotFound()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();

        var descriptor = new ActionDescriptor { Id = "atlas:nonexistent" };
        var context = new ActionContext(Sapi);

        var result = registry.Execute(descriptor, context);

        Assert.False(result.IsSuccess);
    }

    [AtlasScenario]
    public async Task ActionRegistry_Unregister_RemovesHandler()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();
        registry.Register(new TestActionHandler());

        Assert.True(registry.Unregister("atlas:test-action"));
        Assert.False(registry.IsRegistered("atlas:test-action"));
    }

    [AtlasScenario]
    public async Task ActionRegistry_GetRegisteredIds_ReturnsAllIds()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();
        registry.Register(new TestActionHandler());
        registry.Register(new UnavailableHandler());

        var ids = registry.GetRegisteredIds();

        Assert.Equal(2, ids.Count);
        Assert.Contains("atlas:test-action", ids);
        Assert.Contains("atlas:unavailable", ids);
    }

    [AtlasScenario]
    public async Task ActionRegistry_ExecuteAll_StopsOnFirstFailure()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();
        registry.Register(new UnavailableHandler());
        registry.Register(new TestActionHandler());

        var descriptors = new[]
        {
            new ActionDescriptor { Id = "atlas:unavailable" },
            new ActionDescriptor { Id = "atlas:test-action" }
        };
        var context = new ActionContext(Sapi);

        var results = registry.ExecuteAll(descriptors, context, continueOnError: false);

        Assert.Single(results);
        Assert.False(results[0].IsSuccess);
    }

    [AtlasScenario]
    public async Task ActionRegistry_ExecuteAll_ContinueOnError_RunsAll()
    {
        await World.Ticks(5);

        var registry = new ActionRegistryService();
        var testHandler = new TestActionHandler();
        registry.Register(new UnavailableHandler());
        registry.Register(testHandler);

        var descriptors = new[]
        {
            new ActionDescriptor { Id = "atlas:unavailable" },
            new ActionDescriptor { Id = "atlas:test-action" }
        };
        var context = new ActionContext(Sapi);

        var results = registry.ExecuteAll(descriptors, context, continueOnError: true);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsSuccess);
        Assert.True(results[1].IsSuccess);
        Assert.True(testHandler.WasExecuted);
    }
}

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class DeferredWorkAtlasTests : AtlasScenarioBase
{
    private ICoreServerAPI Sapi => (ICoreServerAPI)World.Api;

    [AtlasScenario]
    public async Task DeferredWork_Schedule_ExecutesAfterDelay()
    {
        await World.Ticks(5);

        var service = ArcanumServices.Get<IDeferredWorkService>();
        Assert.NotNull(service);

        bool executed = false;
        service!.Server.Schedule("atlas-test-deferred", () => executed = true, 100);

        await World.Ticks(10);

        Assert.True(executed, "Deferred action was not executed within the expected time window.");
    }

    [AtlasScenario]
    public async Task DeferredWork_Schedule_ReplacesExistingKey()
    {
        await World.Ticks(5);

        var service = ArcanumServices.Get<IDeferredWorkService>();
        Assert.NotNull(service);

        int count = 0;
        service!.Server.Schedule("atlas-replace-key", () => count++, 100);
        service.Server.Schedule("atlas-replace-key", () => count += 10, 200);

        await World.Ticks(15);

        Assert.Equal(10, count);
    }

    [AtlasScenario]
    public async Task DeferredWork_ScheduleCallback_ExecutesOnce()
    {
        await World.Ticks(5);

        var service = ArcanumServices.Get<IDeferredWorkService>();
        Assert.NotNull(service);

        int count = 0;
        service!.Server.ScheduleCallback("atlas-callback-once", () => count++, 100);

        await World.Ticks(10);

        Assert.Equal(1, count);
    }
}


