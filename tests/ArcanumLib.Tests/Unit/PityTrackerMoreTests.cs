using System.Collections.Generic;
using ArcanumLib.Progression;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PityTrackerMoreTests
{
    [Fact]
    public void RegisterPityDefinitions_CreatesMultipleTiers()
    {
        var tracker = new PityTracker(null);

        tracker.RegisterPityDefinitions("tier", tier3Cap: 5, tier4Cap: 10);

        Assert.Contains("tier1", tracker.GetDefinitionIds());
        Assert.Contains("tier2", tracker.GetDefinitionIds());
        Assert.Contains("tier3", tracker.GetDefinitionIds());
        Assert.Contains("tier4", tracker.GetDefinitionIds());

        Assert.True(tracker.TryGetDefinition("tier1", out var def1));
        Assert.Equal(2, def1!.rules.Count);
    }

    [Fact]
    public void TryGetDefinition_ReturnsFalse_ForUnknown()
    {
        var tracker = new PityTracker(null);

        Assert.False(tracker.TryGetDefinition("missing", out var def));
        Assert.Null(def);
    }

    [Fact]
    public void GetOpensUntilGuarantee_SpecificTier()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition
        {
            definitionId = "mixed",
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 },
                new() { qualityTierIndex = 4, opensUntilGuarantee = 10 }
            }
        });

        const string player = "p";
        tracker.RecordOpen(player, "mixed", 0);
        tracker.RecordOpen(player, "mixed", 0);

        Assert.Equal(3, tracker.GetOpensUntilGuarantee(player, "mixed", 3));
        Assert.Equal(8, tracker.GetOpensUntilGuarantee(player, "mixed", 4));
    }

    [Fact]
    public void AddLegacyFallbackKey_TracksKey()
    {
        var tracker = new PityTracker(null, "old-key-1");

        Assert.Contains("old-key-1", tracker.LegacyFallbackKeys);

        tracker.AddLegacyFallbackKey("old-key-2");

        Assert.Contains("old-key-2", tracker.LegacyFallbackKeys);
    }

    [Fact]
    public void Save_And_Initialize_DoNotThrow_WhenNoSapi()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterPityDefinitions("save", 3, 5);

        const string player = "p";
        tracker.RecordOpen(player, "save1", 0);

        tracker.Save();

        var ex = Record.Exception(() => tracker.Initialize());

        Assert.Null(ex);
    }

    [Fact]
    public void RecordOpen_IgnoresEmptyInputs()
    {
        var tracker = new PityTracker(null);
        tracker.RegisterDefinition(new PityDefinition
        {
            definitionId = "x",
            rules = new List<PityTierRule> { new() { qualityTierIndex = 3, opensUntilGuarantee = 5 } }
        });

        tracker.RecordOpen("", "x", 0);
        tracker.RecordOpen("p", "", 0);
        tracker.RecordOpen("p", "x", 0);

        Assert.Equal(0, tracker.GetCounters("", "x")?.totalOpens ?? 0);
        Assert.Equal(0, tracker.GetCounters("p", "")?.totalOpens ?? 0);
        Assert.Equal(1, tracker.GetCounters("p", "x")?.totalOpens);
    }
}
