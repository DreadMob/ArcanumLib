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
    /// Raycasts from the eye position to the hologram and returns true if any solid block (except <paramref name="ignorePos" />) blocks the line of sight.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="eyePos">The three-dimensional vector.</param>
    /// <param name="targetPos">The three-dimensional vector.</param>
    /// <param name="ignorePos">The block position.</param>
    /// <returns>true if occluded; otherwise, false.</returns>
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
    /// <param name="distance">The distance value.</param>
    /// <returns>The compute scale.</returns>
    public static float ComputeScale(float distance)
    {
        // Distance-based scale: larger close-up, smooth falloff, clamped floor/ceiling.
        // At 1 block ~1.8x, at 10 blocks ~1.0x, at 20 blocks ~0.5x, never below 0.5x.
        return Math.Min(1.8f, Math.Max(0.5f, 10f / Math.Max(1f, distance)));
    }
}
