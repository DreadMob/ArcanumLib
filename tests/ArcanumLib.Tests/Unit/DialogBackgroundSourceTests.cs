using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArcanumLib.Tests.Unit;

/// <summary>A dialog background that sizes itself to its children (FitToChildren) throws on compose when nothing was
/// added inside it ("Cant build bounds from children elements"): the contract board crashed the client this way.
/// Client windows cannot be composed in tests, so the sources are checked instead.</summary>
public class DialogBackgroundSourceTests
{
    private static string Src()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(DialogBackgroundSourceTests).Assembly.Location)!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ArcanumLib.csproj"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src");
    }

    [Fact]
    public void Every_Fit_To_Children_Background_Gets_Children()
    {
        var bgCall = new Regex(@"\.Add(?:Shaded)?DialogBG\(\s*(\w+)");
        var wrong = Directory.EnumerateFiles(Src(), "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                string text = File.ReadAllText(f);
                return bgCall.Matches(text).Select(m => m.Groups[1].Value).Distinct().Any(bg =>
                    Regex.IsMatch(text, $@"\b{bg}\.BothSizing\s*=\s*ElementSizing\.FitToChildren")
                    && !Regex.IsMatch(text, $@"BeginChildElements\(\s*{bg}\s*\)|\b{bg}\.WithChildren?\("));
            })
            .Select(Path.GetFileName).ToList();
        Assert.True(wrong.Count == 0, "background without children: " + string.Join(", ", wrong));
    }
}
