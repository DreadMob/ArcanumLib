using System.IO;
using System.Linq;
using ArcanumLib.Blocks;
using Newtonsoft.Json.Linq;

namespace ArcanumLib.Tests.Unit;

public class MultiblockReachTests
{
    private static JObject Vanilla()
    {
        var root = System.Environment.GetEnvironmentVariable("VINTAGE_STORY");
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(MultiblockReachTests).Assembly.Location)!);
        while (string.IsNullOrEmpty(root) && dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "vintagestory", "assets"))) root = Path.Combine(dir.FullName, "vintagestory");
            dir = dir.Parent;
        }
        return JObject.Parse(File.ReadAllText(Path.Combine(root!, "assets", "game", "blocktypes", "multiblock.json")));
    }

    private static string[] States(JObject bt, string group)
        => bt["variantgroups"]!.Single(g => (string?)g["code"] == group)["states"]!.Values<string>().ToArray()!;

    [Fact]
    public void Widens_Vanilla_To_Five_Sideways_And_Eight_Up()
    {
        var bt = Vanilla();
        Assert.True(MultiblockReach.Widen(bt));
        foreach (var g in new[] { "dx", "dz" })
            Assert.Equal(Enumerable.Range(-5, 11).Select(MultiblockReach.Code).OrderBy(s => s), States(bt, g).OrderBy(s => s));
        Assert.Equal(Enumerable.Range(-2, 11).Select(MultiblockReach.Code).OrderBy(s => s), States(bt, "dy").OrderBy(s => s));
        Assert.Equal(new[] { "monolithic" }, States(bt, "type"));
        Assert.False(MultiblockReach.Widen(bt));       // second run changes nothing
    }

    /// <summary>A mod that still patches the same states, and one that goes further, both survive without repeats.</summary>
    [Fact]
    public void Drops_Repeats_From_Other_Patches_And_Keeps_Their_Extra_Offsets()
    {
        var bt = Vanilla();
        var dx = (JArray)bt["variantgroups"]!.Single(g => (string?)g["code"] == "dx")["states"]!;
        dx.Add("p3"); dx.Add("n4"); dx.Add("p3"); dx.Add("p7");
        MultiblockReach.Widen(bt);
        var states = States(bt, "dx");
        Assert.Equal(states.Length, states.Distinct().Count());
        Assert.Contains("p7", states);
        Assert.Contains("n5", states);
    }

    [Theory]
    [InlineData(-5, "n5")]
    [InlineData(0, "0")]
    [InlineData(8, "p8")]
    public void Codes_Read_Like_The_Variants(int offset, string code) => Assert.Equal(code, MultiblockReach.Code(offset));
}
