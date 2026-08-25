using System;
using System.Threading;
using ArcanumLib.Common;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using NSubstitute;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class PlaytimeCooldownManagerTests : IDisposable
{
    public PlaytimeCooldownManagerTests()
    {
        ArcanumRuntime.Activate();
        ArcanumServices.Register<ModDataStoreRegistry>(new ModDataStoreRegistry(), ArcanumServiceScope.Server);
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
    }

    [Fact]
    public void IsOnCooldown_ImmediatelyAfterSet_ReturnsTrue()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        mgr.SetCooldown("p1", "runes");

        Assert.True(mgr.IsOnCooldown("p1", "runes", 5));
    }

    [Fact]
    public void IsOnCooldown_AfterExpiry_ReturnsFalse()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        mgr.SetCooldown("p1", "runes");
        Thread.Sleep(1100);

        Assert.False(mgr.IsOnCooldown("p1", "runes", 1));
    }

    [Fact]
    public void GetCooldownRemaining_BeforeExpiry_ReturnsPositive()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        mgr.SetCooldown("p1", "runes");

        Assert.InRange(mgr.GetCooldownRemaining("p1", "runes", 10), 1, 10);
    }

    [Fact]
    public void ClearCooldown_RemovesEntry()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        mgr.SetCooldown("p1", "runes");
        Assert.True(mgr.IsOnCooldown("p1", "runes", 10));

        mgr.ClearCooldown("p1", "runes");
        Assert.False(mgr.IsOnCooldown("p1", "runes", 10));
    }

    [Fact]
    public void IsInCombat_ImmediatelyAfterMark_ReturnsTrue()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        mgr.MarkCombat("p1");
        Assert.True(mgr.IsInCombat("p1", 5));
    }

    [Fact]
    public void IsInCombat_AfterExpiry_ReturnsFalse()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        mgr.MarkCombat("p1");
        Thread.Sleep(1100);

        Assert.False(mgr.IsInCombat("p1", 1));
    }

    [Fact]
    public void HasRequiredPlaytime_NoTracker_ReturnsFalseForPositiveRequirement()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        Assert.False(mgr.HasRequiredPlaytime("p1", 1f));
    }

    [Fact]
    public void HasRequiredPlaytime_ZeroOrNegative_AlwaysTrue()
    {
        var mgr = new PlaytimeCooldownManager((PlaytimeTracker?)null);

        Assert.True(mgr.HasRequiredPlaytime("p1", 0f));
        Assert.True(mgr.HasRequiredPlaytime("p1", -1f));
    }

    [Fact]
    public void PlaytimeRemaining_WithTracker_ReturnsExpected()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());
        tracker.SetTotalMs("p1", 3600000); // 1 hour
        var mgr = new PlaytimeCooldownManager(tracker);

        Assert.Equal(1f, mgr.GetPlaytimeRemaining("p1", 2f), 5);
        Assert.True(mgr.HasRequiredPlaytime("p1", 1f));
        Assert.False(mgr.HasRequiredPlaytime("p1", 2f));
    }

    [Fact]
    public void CanProceed_CombinesAllChecks()
    {
        using var tracker = new PlaytimeTracker(Substitute.For<ICoreServerAPI>());
        tracker.SetTotalMs("p1", 7200000); // 2 hours
        var mgr = new PlaytimeCooldownManager(tracker);

        Assert.True(mgr.CanProceed("p1", "category", 0, 0, 1f));

        mgr.SetCooldown("p1", "category");
        Assert.False(mgr.CanProceed("p1", "category", 10, 0, 1f));

        mgr.ClearCooldown("p1", "category");
        mgr.MarkCombat("p1");
        Assert.False(mgr.CanProceed("p1", "category", 0, 10, 1f));

        mgr = new PlaytimeCooldownManager(tracker);
        Assert.False(mgr.CanProceed("p1", "category", 0, 0, 5f));
    }
}
