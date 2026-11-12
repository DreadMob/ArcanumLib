using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ArcanumLib.Entities;

/// <summary>
/// The game starts a seat's rider pose (<see cref="IMountableSeat.SuggestedAnimation"/>) once, when the rider mounts;
/// a re-tesselation, a wearable or model change or an animation reset later drops it and nothing starts it again, so
/// the rider stands waist-deep in the seat. Call these from a client tick of whatever owns the seats (a few times a
/// second is plenty). Seats whose pose the caller changes on purpose (a carried rider, a pose synced to a clip) are
/// left to the caller.
/// </summary>
public static class RiderPoses
{
    /// <summary>Starts <paramref name="pose"/> on <paramref name="rider"/> when it is not running. Returns whether it
    /// had to.</summary>
    public static bool Keep(Entity? rider, AnimationMetaData? pose)
    {
        var anims = rider?.AnimManager;
        if (anims == null || pose?.Code == null || anims.IsAnimationActive(pose.Code)) return false;
        anims.StartAnimation(pose);
        return true;
    }

    /// <summary><see cref="Keep(Entity, AnimationMetaData)"/> for the passenger of each seat.</summary>
    public static void Keep(IEnumerable<IMountableSeat?>? seats)
    {
        if (seats == null) return;
        foreach (var seat in seats)
            if (seat?.Passenger != null) Keep(seat.Passenger, seat.SuggestedAnimation);
    }

    /// <summary>Every seat of a mountable entity (a mount, a vehicle).</summary>
    public static void Keep(Entity mountable) => Keep(mountable.GetInterface<IMountable>()?.Seats);
}
