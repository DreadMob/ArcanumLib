using System.Collections.Generic;
using ArcanumLib.Common;
using NSubstitute;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PlaytimeTrackerTests
{
    [Fact]
    public void GetPlaytimeMs_ReturnsStoredValue()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());

        tracker.SetTotalMs("p1", 7200000);

        Assert.Equal(7200000, tracker.GetPlaytimeMs("p1"));
        Assert.Equal(2f, tracker.GetPlaytimeHours("p1"), 5);
    }

    [Fact]
    public void GetPlaytimeMs_UnknownPlayer_ReturnsZero()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());

        Assert.Equal(0, tracker.GetPlaytimeMs("nobody"));
        Assert.Equal(0f, tracker.GetPlaytimeHours("nobody"), 5);
    }

    [Fact]
    public void GetAllPlaytimeHours_ReturnsAllPlayers()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());

        tracker.SetTotalMs("p1", 3600000);
        tracker.SetTotalMs("p2", 7200000);

        var all = tracker.GetAllPlaytimeHours();

        Assert.Equal(2, all.Count);
        Assert.Equal(1f, all["p1"], 5);
        Assert.Equal(2f, all["p2"], 5);
    }

    [Fact]
    public void ImportFromDictionary_ImportsAndClampsNegatives()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());

        int count = tracker.ImportFromDictionary(new Dictionary<string, long>
        {
            ["p1"] = 3600000,
            ["p2"] = -1000,
            [""] = 1
        });

        Assert.Equal(2, count);
        Assert.Equal(3600000, tracker.GetPlaytimeMs("p1"));
        Assert.Equal(0, tracker.GetPlaytimeMs("p2"));
    }

    [Fact]
    public void SetFirstJoinMs_ThenGet_ReturnsValue()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());

        tracker.SetFirstJoinMs("p1", 1234567890);

        Assert.Equal(1234567890, tracker.GetFirstJoinMs("p1"));
    }

    [Fact]
    public void GetFirstJoinMs_Unknown_ReturnsNull()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());

        Assert.Null(tracker.GetFirstJoinMs("nobody"));
    }

    [Fact]
    public void GetLastOnlineMs_Offline_Unknown_ReturnsNull()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());

        Assert.Null(tracker.GetLastOnlineMs("nobody"));
    }
}
