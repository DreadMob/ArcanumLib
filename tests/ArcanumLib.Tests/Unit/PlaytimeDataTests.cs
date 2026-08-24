using ArcanumLib.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class PlaytimeDataTests
{
    [Fact]
    public void DefaultPlayers_Dictionary_IsCaseInsensitive()
    {
        var data = new PlaytimeData();
        data.Players["PlayerOne"] = new PlayerPlaytimeData { TotalMs = 1000 };

        Assert.True(data.Players.TryGetValue("playerone", out var player));
        Assert.Equal(1000, player!.TotalMs);
    }

    [Fact]
    public void PlayerPlaytimeData_Defaults_AreZero()
    {
        var player = new PlayerPlaytimeData();

        Assert.Equal(0, player.TotalMs);
        Assert.Equal(0, player.FirstJoinMs);
        Assert.Equal(0, player.LastOnlineMs);
        Assert.Equal(0, player.LoginStreak);
        Assert.Equal(0, player.LastLoginDayMs);
    }
}
