using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Entities;

/// <summary>
/// Tuning values for <see cref="EntityBehaviorDrivable" />. Values are read from the
/// <c>drive</c> object inside the entity behavior configuration; every key is optional
/// and falls back to the defaults defined here.
/// </summary>
/// <example>
/// Entity json:
/// <code language="json">
/// { "code": "drivable", "drive": { "maxSpeed": 5, "turnSpeed": 1.4, "steerK": 3 } }
/// </code>
/// </example>
public sealed class DrivableParams
{
    /// <summary>
    /// Top forward speed in blocks per second. Default: 4.
    /// </summary>
    public float MaxSpeed = 4f;

    /// <summary>
    /// Fraction of <see cref="MaxSpeed" /> that is reachable while driving backwards (0..1). Default: 0.5.
    /// </summary>
    public float ReverseFactor = 0.5f;

    /// <summary>
    /// Exponential approach rate in 1/seconds used while accelerating toward a larger target speed. Default: 0.5.
    /// </summary>
    public float AccelK = 0.5f;

    /// <summary>
    /// Exponential approach rate in 1/seconds used while slowing down or reversing direction. Default: 1.
    /// </summary>
    public float BrakeK = 1f;

    /// <summary>
    /// Per-second speed multiplier applied while coasting without drive input (0..1). Default: 0.98.
    /// </summary>
    public float CoastDrag = 0.98f;

    /// <summary>
    /// Peak demanded yaw rate in radians per second. Default: 1.
    /// </summary>
    public float TurnSpeed = 1f;

    /// <summary>
    /// Exponential approach rate in 1/seconds of the yaw rate toward its demand. Default: 2.
    /// </summary>
    public float SteerK = 2f;

    /// <summary>
    /// Fraction (0..1) of the demanded yaw rate that is discarded before being applied
    /// to the entity. Default: 0.
    /// </summary>
    public float Understeer = 0f;

    /// <summary>
    /// Maximum height in blocks the step-up assist may lift the entity when forward
    /// motion is blocked by terrain. Default: 0.55.
    /// </summary>
    public float StepUpMax = 0.55f;

    /// <summary>
    /// When <see langword="true" /> the entity may rotate in place and the turn curve no
    /// longer depends on speed. Default: <see langword="false" />.
    /// </summary>
    public bool TurnAtStandstill;

    /// <summary>
    /// When <see langword="true" /> drive input is ignored while the entity's feet are
    /// not in a liquid. Default: <see langword="false" />.
    /// </summary>
    public bool WaterOnly;

    /// <summary>
    /// Fraction of <see cref="TurnSpeed" /> still reachable at full speed (0..1):
    /// 1 keeps speed-invariant steering, lower values numb the wheel the faster
    /// the vehicle goes. Default: 1.
    /// </summary>
    public float SteerFalloff = 1f;

    /// <summary>
    /// Per-second speed bleed while steering hard at speed (0..1 of current speed
    /// lost per second at full deflection and full speed) — turning scrubs speed.
    /// Default: 0.
    /// </summary>
    public float SteerDragK = 0f;

    /// <summary>
    /// Reads a parameter set from a json object. Missing or unparsable keys keep their
    /// defaults; rates are clamped to non-negative finite values and factors to their
    /// documented ranges so bad config data cannot propagate NaN or infinity.
    /// </summary>
    /// <param name="attributes">The <c>drive</c> object of the behavior configuration. May be missing.</param>
    /// <returns>A populated <see cref="DrivableParams" /> instance.</returns>
    public static DrivableParams FromAttributes(JsonObject attributes)
    {
        var p = new DrivableParams();
        if (!attributes.Exists) return p;

        p.MaxSpeed = Clean(attributes["maxSpeed"].AsFloat(p.MaxSpeed), p.MaxSpeed, 0f, 1000f);
        p.ReverseFactor = Clean(attributes["reverseFactor"].AsFloat(p.ReverseFactor), p.ReverseFactor, 0f, 1f);
        p.AccelK = Clean(attributes["accelK"].AsFloat(p.AccelK), p.AccelK, 0f, 1000f);
        p.BrakeK = Clean(attributes["brakeK"].AsFloat(p.BrakeK), p.BrakeK, 0f, 1000f);
        p.CoastDrag = Clean(attributes["coastDrag"].AsFloat(p.CoastDrag), p.CoastDrag, 0f, 1f);
        p.TurnSpeed = Clean(attributes["turnSpeed"].AsFloat(p.TurnSpeed), p.TurnSpeed, 0f, 1000f);
        p.SteerK = Clean(attributes["steerK"].AsFloat(p.SteerK), p.SteerK, 0f, 1000f);
        p.Understeer = Clean(attributes["understeer"].AsFloat(p.Understeer), p.Understeer, 0f, 1f);
        p.StepUpMax = Clean(attributes["stepUpMax"].AsFloat(p.StepUpMax), p.StepUpMax, 0f, 10f);
        p.TurnAtStandstill = attributes["turnAtStandstill"].AsBool(p.TurnAtStandstill);
        p.WaterOnly = attributes["waterOnly"].AsBool(p.WaterOnly);
        p.SteerFalloff = Clean(attributes["steerFalloff"].AsFloat(p.SteerFalloff), p.SteerFalloff, 0f, 1f);
        p.SteerDragK = Clean(attributes["steerDragK"].AsFloat(p.SteerDragK), p.SteerDragK, 0f, 1f);
        return p;
    }

    private static float Clean(float value, float fallback, float min, float max)
        => float.IsFinite(value) ? GameMath.Clamp(value, min, max) : fallback;
}
