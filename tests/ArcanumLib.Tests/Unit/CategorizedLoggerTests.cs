using System.IO;
using ArcanumLib.Core;
using ArcanumLib.Logging;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class CategorizedLoggerTests : IDisposable
{
    private readonly string _tempRoot;

    public CategorizedLoggerTests()
    {
        ArcanumRuntime.Activate();
        _tempRoot = Path.Combine(Path.GetTempPath(), "arcanum-logger-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        ArcanumRuntime.Current?.Dispose();
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Init_RegistersSingletonAndDisposesPrevious()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod1");
        var first = CategorizedLogger.Instance;
        Assert.NotNull(first);

        CategorizedLogger.Init(api, logFolderName: "mod2");
        var second = CategorizedLogger.Instance;
        Assert.NotNull(second);
        Assert.NotSame(first, second);

        CategorizedLogger.Instance!.Dispose();
    }

    [Fact]
    public void Init_RegistersUnderConcreteAndInterface()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        var concrete = ArcanumServices.Get<CategorizedLogger>();
        var @interface = ArcanumServices.Get<ICategorizedLogger>();
        Assert.NotNull(concrete);
        Assert.NotNull(@interface);
        Assert.Same(CategorizedLogger.Instance, @interface);
        Assert.Same(CategorizedLogger.Instance, concrete);

        CategorizedLogger.Instance!.Dispose();
    }

    [Fact]
    public void Dispose_ClearsSingleton()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api);

        CategorizedLogger.Instance!.Dispose();

        Assert.Null(CategorizedLogger.Instance);
    }

    [Fact]
    public void Info_WritesToCategoryFile()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Info("combat", "player hit for 10");
        CategorizedLogger.Instance.Dispose();

        var file = Path.Combine(_tempRoot, "mod", "combat.log");
        Assert.True(File.Exists(file));
        var content = File.ReadAllText(file);
        Assert.Contains("player hit for 10", content);
        Assert.Contains("[INFO]", content);
    }

    [Fact]
    public void Important_WritesToBothCategoryAndImportantFile()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Important("economy", "trade completed");
        CategorizedLogger.Instance.Dispose();

        var cat = File.ReadAllText(Path.Combine(_tempRoot, "mod", "economy.log"));
        var imp = File.ReadAllText(Path.Combine(_tempRoot, "mod", "important.log"));

        Assert.Contains("trade completed", cat);
        Assert.Contains("trade completed", imp);
        Assert.Contains("[economy]", imp);
    }

    [Fact]
    public void Error_WritesToCategoryAndImportantAndMirrorsToConsole()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Error("system", "boom", new System.InvalidOperationException("x"));
        CategorizedLogger.Instance.Dispose();

        var cat = File.ReadAllText(Path.Combine(_tempRoot, "mod", "system.log"));
        var imp = File.ReadAllText(Path.Combine(_tempRoot, "mod", "important.log"));

        Assert.Contains("boom", cat);
        Assert.Contains("[ERROR]", cat);
        Assert.Contains("boom", imp);
        Assert.Contains("System.InvalidOperationException", cat);

        api.Logger.Received().Error(Arg.Any<string>());
    }

    [Fact]
    public void Warning_WritesToCategoryAndImportant()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Warning("combat", "low health");
        CategorizedLogger.Instance.Dispose();

        var cat = File.ReadAllText(Path.Combine(_tempRoot, "mod", "combat.log"));
        var imp = File.ReadAllText(Path.Combine(_tempRoot, "mod", "important.log"));

        Assert.Contains("low health", cat);
        Assert.Contains("[WARN]", cat);
        Assert.Contains("low health", imp);
    }

    [Fact]
    public void Debug_InProductionMode_DoesNotWriteToFile()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, new LogConfig { Mode = LogMode.Production, EnableFileLog = true }, "mod");

        CategorizedLogger.Instance!.Debug("combat", "tick");
        CategorizedLogger.Instance.Dispose();

        Assert.False(File.Exists(Path.Combine(_tempRoot, "mod", "combat.log")));
    }

    [Fact]
    public void Debug_InDebugMode_WritesToFile()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, new LogConfig { Mode = LogMode.Debug, EnableFileLog = true }, "mod");

        CategorizedLogger.Instance!.Debug("combat", "tick");
        CategorizedLogger.Instance.Dispose();

        var cat = File.ReadAllText(Path.Combine(_tempRoot, "mod", "combat.log"));
        Assert.Contains("[DEBUG]", cat);
        Assert.Contains("tick", cat);
    }

    [Fact]
    public void FileLogDisabled_DoesNotCreateFiles()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, new LogConfig { EnableFileLog = false }, "mod");

        CategorizedLogger.Instance!.Info("combat", "hello");
        CategorizedLogger.Instance!.Error("combat", "boom");
        CategorizedLogger.Instance.Dispose();

        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "mod")));
    }

    [Fact]
    public void Structured_WritesEventTypeAndFields()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Structured("combat", "Hit",
            ("target", "goblin"), ("damage", "42"));
        CategorizedLogger.Instance.Dispose();

        var cat = File.ReadAllText(Path.Combine(_tempRoot, "mod", "combat.log"));
        Assert.Contains("[Hit]", cat);
        Assert.Contains("target=goblin", cat);
        Assert.Contains("damage=42", cat);
    }

    [Fact]
    public void ApplyConfig_UpdatesMode()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, new LogConfig { Mode = LogMode.Production, EnableFileLog = true }, "mod");

        CategorizedLogger.ApplyConfig(new LogConfig { Mode = LogMode.Debug, EnableFileLog = true });

        Assert.Equal(LogMode.Debug, CategorizedLogger.Instance!.Config.Mode);
        CategorizedLogger.Instance.Dispose();
    }

    [Fact]
    public void ApplyConfig_NullInstance_DoesNothing()
    {
        CategorizedLogger.ApplyConfig(new LogConfig { Mode = LogMode.Verbose });
        Assert.Null(CategorizedLogger.Instance);
    }

    [Fact]
    public void Subcategory_CreatesNestedDirectory()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Info("economy/trades", "deal done");
        CategorizedLogger.Instance.Dispose();

        var file = Path.Combine(_tempRoot, "mod", "economy", "trades.log");
        Assert.True(File.Exists(file));
        Assert.Contains("deal done", File.ReadAllText(file));
    }

    [Fact]
    public void CategorySanitization_RemovesTraversalAndBackslashes()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Info("..\\evil", "payload");
        CategorizedLogger.Instance.Dispose();

        var file = Path.Combine(_tempRoot, "mod", "evil.log");
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void MultipleLogs_AreAppendedNotTruncated()
    {
        var api = CreateApi(_tempRoot);
        CategorizedLogger.Init(api, logFolderName: "mod");

        CategorizedLogger.Instance!.Info("combat", "first");
        CategorizedLogger.Instance!.Info("combat", "second");
        CategorizedLogger.Instance.Dispose();

        var cat = File.ReadAllText(Path.Combine(_tempRoot, "mod", "combat.log"));
        Assert.Contains("first", cat);
        Assert.Contains("second", cat);
    }

    private static ICoreAPI CreateApi(string tempRoot)
    {
        var api = Substitute.For<ICoreAPI>();
        api.GetOrCreateDataPath("Logs").Returns(tempRoot);

        var logger = Substitute.For<ILogger>();
        api.Logger.Returns(logger);

        var world = Substitute.For<IWorldAccessor>();
        world.ElapsedMilliseconds.Returns(0, 10000, 20000, 30000, 40000, 50000, 60000, 70000, 80000, 90000, 100000);
        api.World.Returns(world);

        return api;
    }
}
