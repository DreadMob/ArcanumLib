using ArcanumLib.Events;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public record TestEvent : IEvent
{
    public int Value { get; init; }
}

public class EventBusTests
{
    public EventBusTests()
    {
        EventBus.ClearAll();
    }

    [Fact]
    public void Subscribe_And_Publish_DeliversPayload()
    {
        TestEvent? received = null;

        EventBus.Subscribe<TestEvent>(e => received = e);
        var evt = new TestEvent { Value = 42 };
        EventBus.Publish(evt);

        Assert.NotNull(received);
        Assert.Equal(42, received!.Value);
    }

    [Fact]
    public void Publish_OnlyHitsSameTypeSubscribers()
    {
        int count = 0;
        EventBus.Subscribe<TestEvent>(_ => count++);

        EventBus.Publish(new TestEvent { Value = 1 });

        Assert.Equal(1, count);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        int count = 0;
        using var sub = EventBus.Subscribe<TestEvent>(_ => count++);

        sub.Dispose();
        EventBus.Publish(new TestEvent { Value = 1 });

        Assert.Equal(0, count);
    }

    [Fact]
    public void Tagged_Subscribe_DoesNotReceiveUntaggedEvents()
    {
        int count = 0;
        EventBus.Subscribe<TestEvent>("tag", _ => count++);

        EventBus.Publish(new TestEvent { Value = 1 });
        EventBus.Publish("tag", new TestEvent { Value = 2 });

        Assert.Equal(1, count);
    }

    [Fact]
    public void Clear_RemovesSubscribers()
    {
        int count = 0;
        EventBus.Subscribe<TestEvent>(_ => count++);
        EventBus.Clear<TestEvent>();

        EventBus.Publish(new TestEvent { Value = 1 });

        Assert.Equal(0, count);
    }

    [Fact]
    public void SubscriberCount_IsCorrect()
    {
        Assert.Equal(0, EventBus.SubscriberCount<TestEvent>());

        var sub1 = EventBus.Subscribe<TestEvent>(_ => { });
        var sub2 = EventBus.Subscribe<TestEvent>(_ => { });

        Assert.Equal(2, EventBus.SubscriberCount<TestEvent>());

        sub1.Dispose();
        Assert.Equal(1, EventBus.SubscriberCount<TestEvent>());

        sub2.Dispose();
    }

    [Fact]
    public void Publish_UntypedTag_DeliversToUntypedSubscribers()
    {
        object? received = null;
        EventBus.Subscribe("mytag", payload => received = payload);

        EventBus.Publish("mytag", 123);

        Assert.Equal(123, received);
    }

    [Fact]
    public void Publish_Untyped_DoesNotThrow_OnEmptyTag()
    {
        Assert.Equal(0, EventBus.Publish("", 123));
    }
}
