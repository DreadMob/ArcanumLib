using System;
using System.Collections.Generic;
using ArcanumLib.Common;
using ArcanumLib.PlayerTransfer;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PlayerDataMergeTests
{
    [Theory]
    [InlineData("kills", NumberMerge.Sum)]
    [InlineData("vsquest:points", NumberMerge.Sum)]
    [InlineData("TotalMs", NumberMerge.Sum)]
    [InlineData("playtime_ms", NumberMerge.Sum)]
    [InlineData("cur", NumberMerge.Sum)]
    [InlineData("combat", NumberMerge.Sum)]
    [InlineData("items", NumberMerge.Sum)]
    [InlineData("unlockedAt", NumberMerge.Max)]
    [InlineData("LastOnlineMs", NumberMerge.Max)]
    [InlineData("lvl", NumberMerge.Max)]
    [InlineData("LoginStreak", NumberMerge.Max)]
    [InlineData("bestWave", NumberMerge.Max)]
    [InlineData("HighestKillStreak", NumberMerge.Max)]
    [InlineData("happiness", NumberMerge.Max)]
    [InlineData("FirstJoinMs", NumberMerge.MinNonZero)]
    [InlineData("firstClearAt", NumberMerge.MinNonZero)]
    public void Classify_PicksRuleByKeyName(string key, NumberMerge expected)
        => Assert.Equal(expected, PlayerDataMerge.Classify(key));

    [Fact]
    public void MergeTree_AchievementsAndRunes_MergeLikeTwoAccounts()
    {
        var target = new TreeAttribute();
        var tAch = target.GetOrAddTreeAttribute("vsquest:ach");
        var a1 = tAch.GetOrAddTreeAttribute("a1");
        a1.SetBool("unlocked", false);
        a1.SetInt("progress", 2);
        var runesT = target.GetOrAddTreeAttribute("vsquest:runes").GetOrAddTreeAttribute("unlocked").GetOrAddTreeAttribute("sand");
        runesT.SetInt("lvl", 3);
        runesT.SetInt("cur", 100);
        target.SetString("core", "mine");

        var source = new TreeAttribute();
        var sAch = source.GetOrAddTreeAttribute("vsquest:ach");
        var s1 = sAch.GetOrAddTreeAttribute("a1");
        s1.SetBool("unlocked", true);
        s1.SetLong("unlockedAt", 1000);
        s1.SetInt("progress", 5);
        sAch.GetOrAddTreeAttribute("a2").SetBool("unlocked", true);
        var runesS = source.GetOrAddTreeAttribute("vsquest:runes").GetOrAddTreeAttribute("unlocked").GetOrAddTreeAttribute("sand");
        runesS.SetInt("lvl", 5);
        runesS.SetInt("cur", 40);
        source.SetString("core", "theirs");
        source.SetStringArray("list", new[] { "x", "y" });
        target.SetStringArray("list", new[] { "y", "z" });

        PlayerDataMerge.MergeTree(target, source);

        var ach = target.GetTreeAttribute("vsquest:ach");
        Assert.True(ach.GetTreeAttribute("a1").GetBool("unlocked"));
        Assert.Equal(1000, ach.GetTreeAttribute("a1").GetLong("unlockedAt"));
        Assert.Equal(7, ach.GetTreeAttribute("a1").GetInt("progress"));
        Assert.True(ach.GetTreeAttribute("a2").GetBool("unlocked"));

        var sand = target.GetTreeAttribute("vsquest:runes").GetTreeAttribute("unlocked").GetTreeAttribute("sand");
        Assert.Equal(5, sand.GetInt("lvl"));
        Assert.Equal(140, sand.GetInt("cur"));
        Assert.Equal("mine", target.GetString("core"));
        Assert.Equal(new[] { "y", "z", "x" }, target.GetStringArray("list"));
    }

    [Fact]
    public void MoveEntry_Playtime_AddsTimeKeepsEarliestJoin()
    {
        var players = new Dictionary<string, PlayerPlaytimeData>(StringComparer.OrdinalIgnoreCase)
        {
            ["old"] = new() { TotalMs = 3_600_000, FirstJoinMs = 100, LastOnlineMs = 500, LoginStreak = 4, DailyMs = new() { ["2026-10-01"] = 10, ["2026-10-02"] = 5 } },
            ["new"] = new() { TotalMs = 600_000, FirstJoinMs = 300, LastOnlineMs = 900, LoginStreak = 1, DailyMs = new() { ["2026-10-02"] = 7 } },
        };

        Assert.True(PlayerDataMerge.MoveEntry(players, "OLD", "new"));

        Assert.False(players.ContainsKey("old"));
        var merged = players["new"];
        Assert.Equal(4_200_000, merged.TotalMs);
        Assert.Equal(100, merged.FirstJoinMs);
        Assert.Equal(900, merged.LastOnlineMs);
        Assert.Equal(4, merged.LoginStreak);
        Assert.Equal(10, merged.DailyMs!["2026-10-01"]);
        Assert.Equal(12, merged.DailyMs["2026-10-02"]);
    }

    [Fact]
    public void MoveEntry_TargetMissing_Renames()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["a"] = 5 };
        Assert.True(PlayerDataMerge.MoveEntry(d, "a", "b"));
        Assert.Equal(5, d["b"]);
        Assert.False(PlayerDataMerge.MoveEntry(d, "missing", "b"));
    }

    private class WithComparer
    {
        public Dictionary<string, int> Counts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Clears { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MergeValues_KeepsCaseInsensitiveCollections()
    {
        var t = new WithComparer();
        t.Counts["Wolf"] = 2;
        t.Clears.Add("Duat");
        var s = new WithComparer();
        s.Counts["wolf"] = 3;
        s.Clears.Add("ossuary");

        var merged = PlayerDataMerge.MergeValues(t, s);

        Assert.Equal(StringComparer.OrdinalIgnoreCase, merged.Counts.Comparer);
        Assert.True(merged.Clears.Contains("DUAT") && merged.Clears.Contains("Ossuary"));
    }
}
