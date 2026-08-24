using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Randomization;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class WeightedRandomTests
{
    [Fact]
    public void Pick_SingleItem_ReturnsThatItem()
    {
        var items = new[] { "only" };
        var result = WeightedRandom.Pick(items, _ => 1f, new Random(0));

        Assert.Equal("only", result);
    }

    [Fact]
    public void Pick_ZeroWeights_ReturnsFirstItem()
    {
        var items = new[] { "first", "second" };
        var result = WeightedRandom.Pick(items, _ => 0f, new Random(0));

        Assert.Equal("first", result);
    }

    [Fact]
    public void Pick_FavorsHeavyWeight_OverManyRolls()
    {
        var items = new[] { "a", "b" };
        var random = new Random(0);
        int heavyWins = 0;
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            var result = WeightedRandom.Pick(items, x => x == "a" ? 0.1f : 0.9f, random);
            if (result == "b")
                heavyWins++;
        }

        Assert.True((double)heavyWins / iterations > 0.85,
            $"Expected heavy item to win most of the time, but it won {heavyWins}/{iterations}.");
    }

    [Fact]
    public void Pick_Throws_WhenItemsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WeightedRandom.Pick<string>(null!, _ => 1f, new Random(0)));
    }

    [Fact]
    public void PickOrDefault_EmptySequence_ReturnsDefault()
    {
        var result = WeightedRandom.PickOrDefault(Array.Empty<string>(), _ => 1f, new Random(0));

        Assert.Null(result);
    }

    [Fact]
    public void PickDistinct_ReturnsAtMostCountItems()
    {
        var items = new[] { "a", "b", "c", "d" };
        var result = WeightedRandom.PickDistinct(items, _ => 1f, new Random(0), 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Distinct().Count());
    }

    [Fact]
    public void PickDistinct_CountExceedsPool_ReturnsAll()
    {
        var items = new[] { "a", "b" };
        var result = WeightedRandom.PickDistinct(items, _ => 1f, new Random(0), 5);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetPercentages_ComputesCorrectShares()
    {
        var items = new[] { "a", "b" };
        var percentages = WeightedRandom.GetPercentages(items, x => x == "a" ? 1f : 3f);

        Assert.Equal(25f, percentages[0].Percentage, 3);
        Assert.Equal(75f, percentages[1].Percentage, 3);
    }

    [Fact]
    public void WeightedTable_TotalWeight_IsRecalculated()
    {
        var table = new WeightedTable<string>();
        table.Add("a", 1f);
        table.Add("b", 2f);

        Assert.Equal(3f, table.TotalWeight, 3);
    }

    [Fact]
    public void WeightedTable_PickOrDefault_EmptyTable_ReturnsDefault()
    {
        var table = new WeightedTable<string>();
        var result = table.PickOrDefault(new Random(0));

        Assert.Null(result);
    }
}
