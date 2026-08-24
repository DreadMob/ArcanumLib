using System.Collections.Generic;
using ArcanumLib.Progression;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PityDefinitionTests
{
    [Fact]
    public void Validate_SortsRules_ByQualityTier()
    {
        var def = new PityDefinition
        {
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 5, opensUntilGuarantee = 10 },
                new() { qualityTierIndex = 2, opensUntilGuarantee = 5 },
                new() { qualityTierIndex = 3, opensUntilGuarantee = 8 }
            }
        };

        def.Validate();

        Assert.Equal(2, def.rules[0].qualityTierIndex);
        Assert.Equal(3, def.rules[1].qualityTierIndex);
        Assert.Equal(5, def.rules[2].qualityTierIndex);
    }

    [Fact]
    public void GetGuaranteedQuality_ReturnsHighestApplicable()
    {
        var def = new PityDefinition
        {
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 },
                new() { qualityTierIndex = 5, opensUntilGuarantee = 10 }
            }
        };

        var counters = new Dictionary<int, int>
        {
            [3] = 4,
            [5] = 9
        };

        Assert.Equal(5, def.GetGuaranteedQuality(counters));
    }

    [Fact]
    public void GetGuaranteedQuality_ReturnsZero_WhenThresholdNotMet()
    {
        var def = new PityDefinition
        {
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 }
            }
        };

        var counters = new Dictionary<int, int> { [3] = 2 };

        Assert.Equal(0, def.GetGuaranteedQuality(counters));
    }

    [Fact]
    public void GetGuaranteedQuality_ReturnsZero_ForNullCounters()
    {
        var def = new PityDefinition
        {
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 5 }
            }
        };

        Assert.Equal(0, def.GetGuaranteedQuality(null!));
    }

    [Fact]
    public void GetGuaranteedQuality_IgnoresRules_WithZeroThreshold()
    {
        var def = new PityDefinition
        {
            rules = new List<PityTierRule>
            {
                new() { qualityTierIndex = 3, opensUntilGuarantee = 0 },
                new() { qualityTierIndex = 4, opensUntilGuarantee = 5 }
            }
        };

        var counters = new Dictionary<int, int> { [3] = 100, [4] = 4 };

        Assert.Equal(4, def.GetGuaranteedQuality(counters));
    }
}
