using System;
using ArcanumLib.Core;
using ArcanumLib.Performance;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class GameTimeSchedulerTests : IDisposable
{
    private readonly GameTimeScheduler _scheduler = new();
    private readonly ICoreServerAPI _sapi;
    private readonly IServerWorldAccessor _world;

    public GameTimeSchedulerTests()
    {
        ArcanumRuntime.Activate();

        _world = Substitute.For<IServerWorldAccessor>();
        var calendar = Substitute.For<IGameCalendar>();
        calendar.TotalHours.Returns(6.0);
        _world.Calendar.Returns(calendar);

        _sapi = Substitute.For<ICoreServerAPI>();
        _sapi.World.Returns(_world);
        _sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>()).Returns(1L);
    }

    public void Dispose()
    {
        _scheduler.Dispose();
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void Start_NullApi_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _scheduler.Start(null!));
    }

    [Fact]
    public void Start_WithApi_RegistersTickListener()
    {
        _scheduler.Start(_sapi);

        _sapi.Event.Received(1).RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>());
    }

    [Fact]
    public void Stop_UnregistersTickListener()
    {
        _sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>()).Returns(42L);
        _scheduler.Start(_sapi);
        _scheduler.Stop();

        _sapi.Event.Received(1).UnregisterGameTickListener(42L);
    }

    [Fact]
    public void Dispose_StopsScheduler()
    {
        _sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>()).Returns(42L);
        _scheduler.Start(_sapi);
        _scheduler.Dispose();

        _sapi.Event.Received(1).UnregisterGameTickListener(42L);
    }

    [Fact]
    public void IsEnabled_DefaultTrue()
    {
        Assert.True(_scheduler.IsEnabled);
    }

    [Fact]
    public void CheckIntervalMs_Default2000()
    {
        Assert.Equal(2000, _scheduler.CheckIntervalMs);
    }

    [Fact]
    public void ScheduleDaily_InvalidHour_Throws()
    {
        _scheduler.Start(_sapi);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _scheduler.ScheduleDaily(-1, _ => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _scheduler.ScheduleDaily(24, _ => { }));
    }

    [Fact]
    public void ScheduleDaily_NullAction_Throws()
    {
        _scheduler.Start(_sapi);

        Assert.Throws<ArgumentNullException>(() =>
            _scheduler.ScheduleDaily(6, null!));
    }

    [Fact]
    public void ScheduleDaily_NotStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _scheduler.ScheduleDaily(6, _ => { }));
    }

    [Fact]
    public void ScheduleDaily_Valid_ReturnsId()
    {
        _scheduler.Start(_sapi);

        var id = _scheduler.ScheduleDaily(6, _ => { });

        Assert.True(id > 0);
        Assert.Equal(1, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void ScheduleHourly_InvalidMinute_Throws()
    {
        _scheduler.Start(_sapi);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _scheduler.ScheduleHourly(-1, _ => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _scheduler.ScheduleHourly(60, _ => { }));
    }

    [Fact]
    public void ScheduleHourly_NullAction_Throws()
    {
        _scheduler.Start(_sapi);

        Assert.Throws<ArgumentNullException>(() =>
            _scheduler.ScheduleHourly(0, null!));
    }

    [Fact]
    public void ScheduleHourly_NotStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _scheduler.ScheduleHourly(0, _ => { }));
    }

    [Fact]
    public void ScheduleHourly_Valid_ReturnsId()
    {
        _scheduler.Start(_sapi);

        var id = _scheduler.ScheduleHourly(30, _ => { });

        Assert.True(id > 0);
        Assert.Equal(1, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void ScheduleAfterHours_InvalidHours_Throws()
    {
        _scheduler.Start(_sapi);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _scheduler.ScheduleAfterHours(0, _ => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _scheduler.ScheduleAfterHours(-1, _ => { }));
    }

    [Fact]
    public void ScheduleAfterHours_NullAction_Throws()
    {
        _scheduler.Start(_sapi);

        Assert.Throws<ArgumentNullException>(() =>
            _scheduler.ScheduleAfterHours(1, null!));
    }

    [Fact]
    public void ScheduleAfterHours_NotStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _scheduler.ScheduleAfterHours(1, _ => { }));
    }

    [Fact]
    public void ScheduleAfterHours_Valid_ReturnsId()
    {
        _scheduler.Start(_sapi);

        var id = _scheduler.ScheduleAfterHours(2.5, _ => { });

        Assert.True(id > 0);
        Assert.Equal(1, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void Cancel_RemovesSchedule()
    {
        _scheduler.Start(_sapi);
        var id = _scheduler.ScheduleDaily(6, _ => { });

        _scheduler.Cancel(id);

        Assert.Equal(0, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void Cancel_UnknownId_DoesNothing()
    {
        _scheduler.Start(_sapi);
        _scheduler.ScheduleDaily(6, _ => { });

        _scheduler.Cancel(999);

        Assert.Equal(1, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void Cancel_NotStarted_DoesNothing()
    {
        _scheduler.Cancel(1);
        // Should not throw
    }

    [Fact]
    public void CancelAll_RemovesAllSchedules()
    {
        _scheduler.Start(_sapi);
        _scheduler.ScheduleDaily(6, _ => { });
        _scheduler.ScheduleHourly(30, _ => { });
        _scheduler.ScheduleAfterHours(2, _ => { });

        _scheduler.CancelAll();

        Assert.Equal(0, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void GetScheduleCount_NotStarted_ReturnsZero()
    {
        Assert.Equal(0, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void GetScheduleCount_WithSchedules_ReturnsCount()
    {
        _scheduler.Start(_sapi);
        _scheduler.ScheduleDaily(6, _ => { });
        _scheduler.ScheduleHourly(30, _ => { });

        Assert.Equal(2, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void Stop_ClearsSchedules()
    {
        _scheduler.Start(_sapi);
        _scheduler.ScheduleDaily(6, _ => { });

        _scheduler.Stop();

        Assert.Equal(0, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void Start_Twice_StopsPreviousAndRestarts()
    {
        _sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>()).Returns(1L);
        _scheduler.Start(_sapi);
        _scheduler.ScheduleDaily(6, _ => { });

        _scheduler.Start(_sapi);

        // Previous schedules should be cleared
        Assert.Equal(0, _scheduler.GetScheduleCount());
    }

    [Fact]
    public void IsEnabled_SetFalse_PausesScheduling()
    {
        _scheduler.IsEnabled = false;
        Assert.False(_scheduler.IsEnabled);
    }

    [Fact]
    public void CheckIntervalMs_CanBeSet()
    {
        _scheduler.CheckIntervalMs = 5000;
        Assert.Equal(5000, _scheduler.CheckIntervalMs);
    }
}
