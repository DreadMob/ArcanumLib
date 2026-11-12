#nullable enable
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Common;

/// <summary>
/// May a working machine (a drill, a dredge, a vehicle's front module, any block or entity that edits the world on its
/// own) touch this cell. Never <c>Claims.TryAccess</c> in such a tick path: every refusal sends an error, resyncs the
/// player's held item and marks the block dirty, so the client rebuilds the chunk. A machine asks for dozens of cells
/// several times a second, which froze and crashed the players near a claim. This asks quietly
/// (<c>Claims.TestAccess</c>) and tells the driver or owner why at most every few seconds per machine. Clicks keep
/// using <c>TryAccess</c>.
/// </summary>
public static class MachineClaims
{
    /// <summary>Shortest gap between two refusal messages of one machine.</summary>
    public const long TellEveryMs = 4000;
    private static readonly ConditionalWeakTable<object, StrongBox<long>> LastTold = new();

    /// <summary>A moving machine works for <paramref name="player"/> (its driver; null = unclaimed land only).</summary>
    public static bool MayWork(Entity machine, IPlayer? player, BlockPos pos, EnumBlockAccessFlags flag)
        => MayWork(machine, machine.World, player, pos, flag);

    /// <summary>A machine standing in the world (a block entity) works for its owner: <paramref name="player"/> when
    /// in the game with a body, else only claims that <paramref name="ownerUid"/> owns let it in (none: unclaimed land
    /// only). The engine's access test reads the player's entity and world data, which are missing while they join or
    /// leave, and throws.</summary>
    public static bool MayWork(BlockEntity machine, IPlayer? player, string? ownerUid, BlockPos pos, EnumBlockAccessFlags flag)
    {
        var world = machine.Api.World;
        if (player?.Entity == null || player.WorldData == null)
        {
            var here = world.Claims.Get(pos);
            if (here == null || here.Length == 0) return true;
            return !string.IsNullOrEmpty(ownerUid) && here.All(c => c.OwnedByPlayerUid == ownerUid);
        }
        return MayWork(machine, world, player, pos, flag);
    }

    private static bool MayWork(object machine, IWorldAccessor world, IPlayer? player, BlockPos pos, EnumBlockAccessFlags flag)
    {
        var claims = world.Claims;
        var answer = claims.TestAccess(player, pos, flag);
        if (answer == EnumWorldAccessResponse.Granted) return true;
        if (player is IServerPlayer driver && TimeToTell(machine, world.ElapsedMilliseconds))
        {
            string action = flag == EnumBlockAccessFlags.Use ? "use" : "buildbreak";
            driver.SendIngameError($"noprivilege-{action}-{answer.ToString().ToLowerInvariant()}", null,
                claims.Get(pos)?.FirstOrDefault()?.LastKnownOwnerName);
        }
        return false;
    }

    /// <summary>Whether this machine may send its next refusal at <paramref name="nowMs"/>; marks it told when so.</summary>
    internal static bool TimeToTell(object machine, long nowMs)
    {
        var told = LastTold.GetOrCreateValue(machine);
        if (told.Value != 0 && nowMs - told.Value < TellEveryMs) return false;
        told.Value = nowMs;
        return true;
    }
}
