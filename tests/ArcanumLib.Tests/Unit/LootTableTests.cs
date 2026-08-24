using System;
using System.Linq;
using ArcanumLib.Randomization;
using Newtonsoft.Json;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class LootTableTests
{
    [Fact]
    public void EffectiveWeight_AppliesLuckMultiplier()
    {
        var table = new LootTable<string>(new[]
        {
            new LootEntry<string>("common", 10f, 0),
            new LootEntry<string>("rare", 2f, 2)
        }, luckMultiplier: 1f);

        Assert.Equal(10f, table.EffectiveWeight(table.Entries[0]), 5);
        Assert.Equal(6f, table.EffectiveWeight(table.Entries[1]), 5);
    }

    [Fact]
    public void Roll_SingleItem_ReturnsThatItem()
    {
        var table = new LootTable<string>();
        table.Add("only", 1f);

        Assert.Equal("only", table.Roll(new Random(0)));
    }

    [Fact]
    public void Roll_ZeroTotal_ReturnsDefault()
    {
        var table = new LootTable<string>();
        table.Add("nothing", -1f);

        Assert.Null(table.Roll(new Random(0)));
    }

    [Fact]
    public void RollMany_ReturnsRequestedCount()
    {
        var table = new LootTable<string>();
        table.Add("a", 1f);
        table.Add("b", 1f);

        var result = table.RollMany(new Random(0), 10);

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void RollDistinct_ReturnsAtMostCount()
    {
        var table = new LootTable<string>();
        table.Add("a", 1f);
        table.Add("b", 1f);

        var result = table.RollDistinct(new Random(0), 5);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FromJson_DeserializesCorrectly()
    {
        const string json = """
        {
            "luckMultiplier": 0.5,
            "entries": [
                { "value": "sword", "weight": 10, "tier": 0 },
                { "value": "gem", "weight": 2, "tier": 2 }
            ]
        }
        """;

        var table = LootTable<string>.FromJson(json);

        Assert.Equal(0.5f, table.LuckMultiplier, 5);
        Assert.Equal(2, table.Entries.Count);
        Assert.Equal("gem", table.Entries[1].Value);
    }

    [Fact]
    public void ToJson_SerializesAndDeserializes()
    {
        var table = new LootTable<string>();
        table.Add("sword", 10f, 0);
        table.Add("gem", 2f, 2);

        var json = table.ToJson();
        var roundTrip = LootTable<string>.FromJson(json);

        Assert.Equal(table.LuckMultiplier, roundTrip.LuckMultiplier, 5);
        Assert.Equal(table.Entries.Count, roundTrip.Entries.Count);
    }

    [Fact]
    public void FromJson_Empty_Throws()
    {
        Assert.ThrowsAny<Exception>(() => LootTable<string>.FromJson(""));
    }
}
