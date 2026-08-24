using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Hologram;

/// <summary>
/// Shared helper functions used by the hologram renderers.
/// </summary>
public static class HologramRenderUtils
{
    /// <summary>
    /// Raycasts from the eye position to the hologram and returns true if any solid block (except <paramref name="ignorePos"/>) blocks the line of sight.
    /// </summary>
    public static bool IsOccluded(ICoreClientAPI capi, Vec3d eyePos, Vec3d targetPos, BlockPos? ignorePos)
    {
        if (capi?.World == null) return false;

        try
        {
            BlockSelection? bs = null;
            EntitySelection? es = null;
            capi.World.RayTraceForSelection(eyePos, targetPos, ref bs, ref es,
                (pos, block) => pos != null && (ignorePos == null || !pos.Equals(ignorePos)),
                entity => false);
            return bs != null;
        }
        catch (Exception ex)
        {
            capi.Logger.Warning("[ArcanumLib] Hologram occlusion ray-trace failed: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Computes a distance-based scale for a projected hologram.
    /// </summary>
    public static float ComputeScale(float distance)
    {
        float scale = 4f / Math.Max(1f, distance);
        float capped = Math.Min(1f, scale);
        if (capped > 0.75f)
            capped = 0.75f + (capped - 0.75f) / 2f;
        return capped;
    }
}
