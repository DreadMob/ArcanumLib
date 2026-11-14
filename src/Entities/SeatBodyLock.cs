using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace ArcanumLib.Entities;

/// <summary>
/// A player on a block seat or bed (a seat with no <see cref="IMountableSeat.Entity" /> under it and a fixed yaw) is
/// drawn turned with the seat, whichever way the player looks. The game keeps the body there only in the rider's own
/// third person view: in first person the body yaw follows the camera and the server passes that yaw to everyone else,
/// so others saw a sleeper sweep out through the side of a coffin or a sitter spin on a chair. Riders of entities are
/// left to the game, which turns them with the mount.
/// <para>The renderer reads <see cref="EntityPos.Yaw" /> and <see cref="EntityAgent.BodyYaw" />; both get the seat's
/// yaw for the draw and are put back after it, so the camera and what goes to the server stay as they were. The rider's
/// own first person view is not touched (hands follow the camera, the immersive eye follows the head).</para>
/// </summary>
public static class SeatBodyLock
{
    private const string HarmonyId = "arcanumlib.seatbodylock";

    private static Harmony? _harmony;

    internal static void Apply()
    {
        if (_harmony != null) return;
        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(AccessTools.Method(typeof(EntityPlayerShapeRenderer), nameof(EntityPlayerShapeRenderer.loadModelMatrixForPlayer)),
            prefix: new HarmonyMethod(typeof(SeatBodyLock), nameof(Prefix)), postfix: new HarmonyMethod(typeof(SeatBodyLock), nameof(Postfix)));
    }

    internal static void Dispose()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
    }

    /// <summary>The yaw the body of <paramref name="seat" />'s rider is drawn with, or null when the game turns it.</summary>
    public static float? FixedYaw(IMountableSeat? seat) =>
        seat is { Entity: null, AngleMode: EnumMountAngleMode.FixateYaw or EnumMountAngleMode.Fixate } ? seat.SeatPosition?.Yaw : null;

    private static void Prefix(Entity entity, bool isSelf, out (float yaw, float bodyYaw)? __state)
    {
        __state = null;
        if (entity is not EntityPlayer player || FixedYaw(player.MountedOn) is not { } yaw) return;
        if (isSelf && (entity.Api as ICoreClientAPI)?.World.Player is { CameraMode: EnumCameraMode.FirstPerson }) return;
        __state = (player.Pos.Yaw, player.BodyYaw);
        player.Pos.Yaw = yaw;
        player.BodyYaw = yaw;
    }

    private static void Postfix(Entity entity, (float yaw, float bodyYaw)? __state)
    {
        if (__state is not { } was || entity is not EntityPlayer player) return;
        player.Pos.Yaw = was.yaw;
        player.BodyYaw = was.bodyYaw;
    }
}
