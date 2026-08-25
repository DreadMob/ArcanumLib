using ArcanumLib.Events;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public record TestEvent : IEvent
{
    public int Value { get; init; }
}

public class EventBusServiceTests
{
    private readonly EventBusService _bus = new();

    [Fact]
    public void Subscribe_And_Publish_DeliversPayload()
    {
        TestEvent? received = null;

        _bus.Subscribe<TestEvent>(e => received = e);
        var evt = new TestEvent { Value = 42 };
        _bus.Publish(evt);

        Assert.NotNull(received);
        Assert.Equal(42, received!.Value);
    }

    [Fact]
    public void Publish_OnlyHitsSameTypeSubscribers()
    {
        int count = 0;
        _bus.Subscribe<TestEvent>(_ => count++);

        _bus.Publish(new TestEvent { Value = 1 });

        Assert.Equal(1, count);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        int count = 0;
        using var sub = _bus.Subscribe<TestEvent>(_ => count++);

        sub.Dispose();
        _bus.Publish(new TestEvent { Value = 1 });

        Assert.Equal(0, count);
    }

    [Fact]
    public void Tagged_Subscribe_DoesNotReceiveUntaggedEvents()
    {
        int count = 0;
        _bus.Subscribe<TestEvent>("tag", _ => count++);

        _bus.Publish(new TestEvent { Value = 1 });
        _bus.Publish("tag", new TestEvent { Value = 2 });

        Assert.Equal(1, count);
    }

    [Fact]
    public void Clear_RemovesSubscribers()
    {
        int count = 0;
        _bus.Subscribe<TestEvent>(_ => count++);
        _bus.Clear<TestEvent>();

        _bus.Publish(new TestEvent { Value = 1 });

        Assert.Equal(0, count);
    }

    [Fact]
    public void SubscriberCount_IsCorrect()
    {
        Assert.Equal(0, _bus.SubscriberCount<TestEvent>());

        var sub1 = _bus.Subscribe<TestEvent>(_ => { });
        var sub2 = _bus.Subscribe<TestEvent>(_ => { });

        Assert.Equal(2, _bus.SubscriberCount<TestEvent>());

        sub1.Dispose();
        Assert.Equal(1, _bus.SubscriberCount<TestEvent>());

        sub2.Dispose();
    }

    [Fact]
    public void Publish_UntypedTag_DeliversToUntypedSubscribers()
    {
        object? received = null;
        _bus.Subscribe("mytag", payload => received = payload);

        _bus.Publish("mytag", 123);

        Assert.Equal(123, received);
    }

    [Fact]
    public void Publish_Untyped_DoesNotThrow_OnEmptyTag()
    {
        Assert.Equal(0, _bus.Publish("", 123));
    }
}
