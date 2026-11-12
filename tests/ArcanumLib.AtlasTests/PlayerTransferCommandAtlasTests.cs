using System.Collections.Concurrent;
using System.Threading.Tasks;
using ArcanumLib.PlayerTransfer;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.AtlasTests;

/// <summary><c>/arcanum transfer</c>: asks for confirmation, then runs every participant and every after-transfer step
/// for the two accounts, and refuses unknown and identical accounts.</summary>
[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class PlayerTransferCommandAtlasTests : AtlasScenarioBase
{
    private ICoreServerAPI Sapi => (ICoreServerAPI)World.Api;

    private async Task<TextCommandResult> Run(string command)
    {
        TextCommandResult? res = null;
        Sapi.ChatCommands.ExecuteUnparsed(command, new TextCommandCallingArgs
        {
            Caller = new Caller { Type = EnumCallerType.Console, CallerRole = "admin", CallerPrivileges = new[] { "*" }, FromChatGroupId = GlobalConstants.ConsoleGroup },
        }, r => res = r);
        await World.Until(() => res != null, timeoutTicks: 100);
        return res!;
    }

    [AtlasScenario]
    public async Task Transfer_Asks_First_Then_Runs_Participants_And_After_Steps()
    {
        var from = await World.JoinPlayer("transferold");
        var to = await World.JoinPlayer("transfernew");
        var moved = new ConcurrentQueue<string>();
        PlayerDataTransfer.Register("test-data", ctx => { moved.Enqueue($"{ctx.FromUid}>{ctx.ToUid}"); ctx.Note("test-data", "moved"); });
        PlayerDataTransfer.RegisterAfterTransfer("test-site", (ctx, reply) => { moved.Enqueue("site"); reply("site done"); });

        var ask = await Run("/arcanum transfer transferold transfernew");
        Assert.Equal(EnumCommandStatus.Success, ask.Status);
        Assert.Contains("/arcanum transfer transferold transfernew confirm", ask.StatusMessage);
        Assert.Contains("test-data", ask.StatusMessage);
        Assert.Contains("test-site", ask.StatusMessage);
        Assert.Empty(moved);

        var done = await Run("/arcanum transfer transferold transfernew confirm");
        Assert.Equal(EnumCommandStatus.Success, done.Status);
        Assert.Contains("test-data: moved", done.StatusMessage);
        Assert.Contains($"{from.Player.PlayerUID}>{to.Player.PlayerUID}", moved);
        Assert.Contains("site", moved);

        Assert.Equal(EnumCommandStatus.Error, (await Run("/arcanum transfer transferold nobodyever confirm")).Status);
        Assert.Equal(EnumCommandStatus.Error, (await Run("/arcanum transfer transferold transferold confirm")).Status);
        Assert.Equal(EnumCommandStatus.Success, (await Run("/arcanum diagnose")).Status);   // the other subcommands keep working
    }
}
