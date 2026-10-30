using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace ArcanumLib.Blocks.Clock;

/// <summary>
/// Data-driven clock setup, read from the block's <c>attributes.clock</c>. Everything a clock looks or
/// sounds like lives here, so mods only ship shapes, sounds and JSON:
/// <code>
/// "attributes": { "clock": {
///   "hourHand": "mymod:shapes/block/clock/hourhand.json", "minuteHand": "mymod:shapes/block/clock/minutehand.json",
///   "dials": [ { "center": [8, 8, 7.4] } ],
///   "parts": [ { "shape": "mymod:shapes/block/clock/pendulum.json", "center": [8, 6, 9], "pivot": [8, 16, 8], "swingAmplitude": 8, "swingPeriod": 2 } ],
///   "chimeSound": "mymod:sounds/clock-chime", "tickSound": "mymod:sounds/clock-tick", "windSound": "mymod:sounds/clock-wind"
/// } }
/// </code>
/// Positions are in shape pixels (0…16 per block) of the block's own shape before its <c>rotateY</c>;
/// the renderer applies the placed variant's <c>rotateY</c> itself, so one config serves all four facings.
/// Hand shapes are tesselated with the block's textures — list their texture codes in the blocktype.
/// </summary>
public class ClockConfig
{
    public const string DefaultLangPrefix = "arcanumlib:";

    [JsonProperty] public string? HourHand;
    [JsonProperty] public string? MinuteHand;
    /// <summary>Point of the hand shapes (px) that sits on the dial axis.</summary>
    [JsonProperty] public float[] HandPivot = { 8, 8, 8 };
    /// <summary>Minute hand distance from the hour hand along the dial's outward normal (px). Negative = behind it.</summary>
    [JsonProperty] public float MinuteHandOffset = 0.3f;
    [JsonProperty] public ClockDial[] Dials = { new() };
    /// <summary>Extra moving parts: spinning gears, swinging pendulums.</summary>
    [JsonProperty] public ClockPart[] Parts = System.Array.Empty<ClockPart>();
    /// <summary>Hands whirl while a temporal storm rages.</summary>
    [JsonProperty] public bool StormSpin = true;

    [JsonProperty] public string? ChimeSound;
    [JsonProperty] public float ChimeRange = 300f;
    [JsonProperty] public float ChimeVolume = 1f;
    [JsonProperty] public int ChimeIntervalMs = 1500;
    /// <summary>New-year melody and storm warning; falls back to <see cref="ChimeSound"/>.</summary>
    [JsonProperty] public string? MelodySound;
    [JsonProperty] public float NewYearRange = 600f;
    [JsonProperty] public float WarningRange = 300f;

    /// <summary>Client-side ticking, played locally only to players within <see cref="TickRange"/>.</summary>
    [JsonProperty] public string? TickSound;
    [JsonProperty] public float TickRange = 10f;
    [JsonProperty] public float TickVolume = 0.35f;
    [JsonProperty] public int TickIntervalMs = 1000;
    /// <summary>Played for everyone nearby when a player opens the chime settings (winding the clock).</summary>
    [JsonProperty] public string? WindSound;
    [JsonProperty] public float WindRange = 16f;

    [JsonProperty] public string LangPrefix = DefaultLangPrefix;

    public static ClockConfig From(Block? block)
    {
        var json = block?.Attributes?["clock"];
        ClockConfig? cfg = null;
        if (json?.Exists == true)
        {
            try { cfg = json.AsObject<ClockConfig>(); }
            catch (System.Exception) { cfg = null; }
        }
        cfg ??= new ClockConfig();
        cfg.Dials ??= System.Array.Empty<ClockDial>();
        cfg.Parts ??= System.Array.Empty<ClockPart>();
        if (cfg.HandPivot is not { Length: 3 }) cfg.HandPivot = new float[] { 8, 8, 8 };
        return cfg;
    }

    public static AssetLocation? Sound(string? code) => string.IsNullOrEmpty(code) ? null : new AssetLocation(code);
}

/// <summary>One clock face. Several dials (a street clock's four faces) share the same hands.</summary>
public class ClockDial
{
    /// <summary>Hand axis position (px, block shape space).</summary>
    [JsonProperty] public float[] Center = { 8, 8, 8 };
    /// <summary>Extra Y rotation of this dial (degrees) — turns a north-facing dial to the other sides.</summary>
    [JsonProperty] public float Yaw;
    /// <summary>Side the dial faces before <see cref="Yaw"/>: "north" (−Z) or "south" (+Z). Hand shapes are authored facing the same way.</summary>
    [JsonProperty] public string Facing = "north";
    [JsonProperty] public float HandScale = 1f;

    public bool FacesSouth => Facing == "south";
}

/// <summary>Moving part: angle = spin·t + swingAmplitude·sin(2π·t / swingPeriod), about the part's local Z axis.</summary>
public class ClockPart
{
    [JsonProperty] public string? Shape;
    /// <summary>Where the pivot lands (px, block shape space).</summary>
    [JsonProperty] public float[] Center = { 8, 8, 8 };
    /// <summary>Rotation axis point inside the part's shape (px).</summary>
    [JsonProperty] public float[] Pivot = { 8, 8, 8 };
    [JsonProperty] public float Scale = 1f;
    [JsonProperty] public float Yaw;
    /// <summary>Radians per second.</summary>
    [JsonProperty] public float Spin;
    /// <summary>Degrees.</summary>
    [JsonProperty] public float SwingAmplitude;
    /// <summary>Seconds per full swing.</summary>
    [JsonProperty] public float SwingPeriod = 2f;

    public float AngleAt(double seconds)
    {
        double a = Spin * seconds;
        if (SwingAmplitude != 0 && SwingPeriod > 0)
            a += SwingAmplitude * Vintagestory.API.MathTools.GameMath.DEG2RAD * System.Math.Sin(2 * System.Math.PI * seconds / SwingPeriod);
        return (float)a;
    }
}
