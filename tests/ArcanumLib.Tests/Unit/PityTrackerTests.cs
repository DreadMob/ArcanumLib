using System.Collections.Generic;
using ArcanumLib.Progression;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PityTrackerTests
{
    [Fact]
    public void RecordOpen_IncrementsCounters_AndGuarantee_KicksIn()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition
        {
            definitionId = "testcase",
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 },
                new() { qualityTierIndex = 4, opensUntilGuarantee = 10 }
            }
        });

        const string player = "player1";

        tracker.RecordOpen(player, "testcase", 0);
        tracker.RecordOpen(player, "testcase", 0);
        tracker.RecordOpen(player, "testcase", 0);
        tracker.RecordOpen(player, "testcase", 0);
        tracker.RecordOpen(player, "testcase", 0);

        Assert.Equal(3, tracker.GetGuaranteedQuality(player, "testcase"));
        Assert.Equal(0, tracker.GetOpensUntilGuarantee(player, "testcase"));
    }

    [Fact]
    public void RecordOpen_WithHighRolledQuality_ResetsLowerTiers()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition
        {
            definitionId = "testcase",
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 },
                new() { qualityTierIndex = 4, opensUntilGuarantee = 10 }
            }
        });

        const string player = "player2";

        for (int i = 0; i < 7; i++)
            tracker.RecordOpen(player, "testcase", 0);

        tracker.RecordOpen(player, "testcase", 4);

        var counters = tracker.GetCounters(player, "testcase");
        Assert.NotNull(counters);
        Assert.Equal(0, counters!.opensSinceQuality[3]);
        Assert.Equal(0, counters.opensSinceQuality[4]);
        Assert.Equal(8, counters.totalOpens);
    }

    [Fact]
    public void GetOpensUntilGuarantee_ReturnsBestRemaining()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition
        {
            definitionId = "testcase",
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 },
                new() { qualityTierIndex = 4, opensUntilGuarantee = 10 }
            }
        });

        const string player = "player3";

        for (int i = 0; i < 3; i++)
            tracker.RecordOpen(player, "testcase", 0);

        Assert.Equal(2, tracker.GetOpensUntilGuarantee(player, "testcase"));
    }

    [Fact]
    public void GetGuaranteedQuality_ReturnsZero_WhenNoRulesMet()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition
        {
            definitionId = "testcase",
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 10 }
            }
        });

        const string player = "player4";

        tracker.RecordOpen(player, "testcase", 0);

        Assert.Equal(0, tracker.GetGuaranteedQuality(player, "testcase"));
    }

    [Fact]
    public void ResetPlayerData_RemovesCounters()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition
        {
            definitionId = "testcase",
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 }
            }
        });

        const string player = "player5";
        tracker.RecordOpen(player, "testcase", 0);
        tracker.RecordOpen(player, "testcase", 0);

        tracker.ResetPlayerData(player);

        Assert.Null(tracker.GetCounters(player, "testcase"));
        Assert.Equal(0, tracker.GetGuaranteedQuality(player, "testcase"));
    }

    [Fact]
    public void RegisterDefinition_WithoutValidId_IsIgnored()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition { definitionId = "" });

        Assert.Empty(tracker.GetDefinitionIds());
    }
}
