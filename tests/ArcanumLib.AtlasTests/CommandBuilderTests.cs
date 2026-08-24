using ArcanumLib.Commands;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.AtlasTests;

[AtlasWorld(SaveFile = "fixtures/world.vcdbs")]
public class CommandBuilderTests : AtlasScenarioBase
{
    [AtlasScenario]
    public async Task CommandBuilder_Registers_And_Executes_TypedCommand()
    {
        var player = await World.JoinPlayer("testcommand");
        var sapi = World.Api;

        string? received = null;
        int receivedNumber = 0;

        CommandBuilder.Create(sapi, "arcanumlib_test")
            .WithDescription("Atlas test command")
            .WithPermission(Privilege.controlserver)
            .Arg<string>("message")
            .Arg<int>("number")
            .OnExecute((_, _, args) =>
            {
                received = args.String("message");
                receivedNumber = args.Int("number");
            })
            .Register();

        await World.Ticks(5);

        var tcs = new TaskCompletionSource<TextCommandResult>();
        sapi.ChatCommands.ExecuteUnparsed("/arcanumlib_test hello 42", new TextCommandCallingArgs
        {
            Caller = new Caller
            {
                Type = EnumCallerType.Player,
                Player = player.Player,
                CallerRole = player.Player.Role.Code,
                CallerPrivileges = player.Player.Role.Privileges.ToArray()
            }
        }, result => tcs.TrySetResult(result));

        var raw = await tcs.Task;

        await World.Ticks(2);

        Assert.True(raw.Status == EnumCommandStatus.Success, $"Command failed: {raw.Status} {raw.StatusMessage}");
        Assert.Equal("hello", received);
        Assert.Equal(42, receivedNumber);
    }
}
