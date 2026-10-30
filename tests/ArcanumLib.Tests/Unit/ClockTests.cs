using ArcanumLib.Blocks.Clock;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Tests.Unit;

public class ClockTests
{
    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 1)]
    [InlineData(12, 12)]
    [InlineData(13, 1)]
    [InlineData(23, 11)]
    [InlineData(-1, 11)]
    [InlineData(24 * 100 + 5, 5)]
    public void ChimeCount_FollowsTwelveHourDial(long totalHour, int expected)
    {
        Assert.Equal(expected, ClockTime.GetChimeCount(totalHour));
    }

    [Fact]
    public void HandAngles_RunClockwiseForPlusZViewer()
    {
        Assert.Equal(0f, ClockTime.GetHourAngle(0), 4);
        Assert.Equal(-GameMath.PIHALF, ClockTime.GetHourAngle(3), 4);
        Assert.Equal(-GameMath.PI, ClockTime.GetMinuteAngle(10.5), 4);
        Assert.Equal(ClockTime.GetHourAngle(1), ClockTime.GetHourAngle(13), 4);
    }

    [Fact]
    public void Config_ReadsBlockAttributes_AndFallsBackOnDefaults()
    {
        var block = new Block
        {
            Attributes = new JsonObject(JToken.Parse("""
                { "clock": { "hourHand": "m:shapes/h.json", "dials": [ { "center": [8, 9, 3], "yaw": 90 }, { "facing": "south" } ],
                             "parts": [ { "shape": "m:shapes/p.json", "swingAmplitude": 10, "swingPeriod": 2 } ],
                             "chimeSound": "m:sounds/bell", "tickSound": "m:sounds/tick", "chimeRange": 48 } }
                """))
        };
        var cfg = ClockConfig.From(block);
        Assert.Equal("m:shapes/h.json", cfg.HourHand);
        Assert.Null(cfg.MinuteHand);
        Assert.Equal(2, cfg.Dials.Length);
        Assert.Equal(new[] { 8f, 9f, 3f }, cfg.Dials[0].Center);
        Assert.Equal(90f, cfg.Dials[0].Yaw);
        Assert.False(cfg.Dials[0].FacesSouth);
        Assert.True(cfg.Dials[1].FacesSouth);
        Assert.Equal(48f, cfg.ChimeRange);
        Assert.Equal(600f, cfg.NewYearRange);
        Assert.Equal(ClockConfig.DefaultLangPrefix, cfg.LangPrefix);
        // pendulum: zero at rest, full amplitude a quarter period later
        Assert.Equal(0f, cfg.Parts[0].AngleAt(0), 4);
        Assert.Equal(10 * GameMath.DEG2RAD, cfg.Parts[0].AngleAt(0.5), 4);

        var bare = ClockConfig.From(new Block());
        Assert.Single(bare.Dials);
        Assert.Empty(bare.Parts);
        Assert.Null(ClockConfig.Sound(bare.ChimeSound));
    }

    // The alegacy_patches clocks moved from their own renderer to ClockRenderer + attributes.clock.
    // Their blocktypes now carry: hands pivot [8,8,0], dial facing south at [8,8,8+16·depth], minute −0.32 px,
    // gears at 16·(0.05+offset) with pivot 0 and scale 0.9. These must land exactly where the old renderer drew them.
    public static IEnumerable<object[]> Sides() => new[]
    {
        new object[] { 0f, 0f, 0f, 1f }, new object[] { 270f, -GameMath.PIHALF, -1f, 0f },
        new object[] { 180f, GameMath.PI, 0f, -1f }, new object[] { 90f, GameMath.PIHALF, 1f, 0f },
    };

    [Theory]
    [MemberData(nameof(Sides))]
    public void LegacyPatchesHands_LandWhereTheOldRendererDrewThem(float rotateY, float legacyYaw, float nx, float nz)
    {
        foreach (float scale in new[] { 1f, 0.2f })
        {
            float depth = -0.5f + 0.375f * scale;
            float angle = ClockTime.GetHourAngle(7.3);
            foreach (bool minute in new[] { false, true })
            {
                float d = minute ? depth - 0.02f : depth;
                var legacy = new Matrixf().Translate(nx * d, 0, nz * d).RotateY(legacyYaw).RotateZ(angle).Translate(-0.5f, -0.5f, 0f);
                var now = ClockRenderer.Compose(new Matrixf(), 0, 0, 0, rotateY * GameMath.DEG2RAD,
                    new[] { 8f, 8f, 8 + 16 * depth }, 0f, minute ? -0.32f : 0f, angle, 1f, new[] { 8f, 8f, 0f });
                AssertSameTransform(legacy, now);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sides))]
    public void LegacyPatchesGears_LandWhereTheOldRendererDrewThem(float rotateY, float legacyYaw, float nx, float nz)
    {
        float gearDepth = -0.125f * 1.333f;
        foreach (var (ox, oy, sign) in new[] { (-2.1f, -2.1f, 1f), (2.1f, -2.1f, -1f), (-2.1f, 2.1f, 1f), (2.1f, 2.1f, -1f) })
        {
            float spin = sign * 1.234f;
            var legacy = new Matrixf().Translate(nx * gearDepth, 0, nz * gearDepth).RotateY(legacyYaw)
                .Scale(1, 1, 1).Translate(ox, oy, 0f).Scale(0.9f, 0.9f, 0.9f).Translate(-0.5f, -0.5f, -0.5f).RotateZ(spin);
            var now = ClockRenderer.Compose(new Matrixf(), 0, 0, 0, rotateY * GameMath.DEG2RAD,
                new[] { 16 * (0.05f + ox), 16 * (0.05f + oy), 16 * (0.05f + gearDepth) }, 0f, 0f, spin, 0.9f, new[] { 0f, 0f, 0f });
            AssertSameTransform(legacy, now);
        }
    }

    [Fact]
    public void NorthFacingDial_HourHandTurnsClockwiseForViewerOnMinusZ()
    {
        // 3 o'clock on a −Z dial: the hand (pointing +Y at rest) must point to the viewer's right, which is −X.
        float angle = -ClockTime.GetHourAngle(3);
        var m = ClockRenderer.Compose(new Matrixf(), 0, 0, 0, 0f, new[] { 8f, 8f, 8f }, 0f, 0f, angle, 1f, new[] { 8f, 8f, 8f });
        var tip = Mat4f.MulWithVec4(m.Values, new float[] { 0.5f, 0.75f, 0.5f, 1f });
        Assert.True(tip[0] < -0.2f, $"tip x={tip[0]}");
        Assert.Equal(0f, tip[1], 3);
    }

    private static void AssertSameTransform(Matrixf expected, Matrixf actual)
    {
        foreach (var p in new[] { new float[] { 0, 0, 0, 1 }, new float[] { 1, 0, 0, 1 }, new float[] { 0, 1, 0, 1 }, new float[] { 0.3f, 0.7f, 0.1f, 1 } })
        {
            var a = Mat4f.MulWithVec4(expected.Values, p);
            var b = Mat4f.MulWithVec4(actual.Values, p);
            for (int i = 0; i < 3; i++) Assert.Equal(a[i], b[i], 3);
        }
    }
}
