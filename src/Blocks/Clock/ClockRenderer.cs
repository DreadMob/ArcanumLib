using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ArcanumLib.Blocks.Clock;

/// <summary>
/// Draws the hands on every dial and the moving parts of a <see cref="BlockEntityClock"/>.
/// Every mesh goes through: block centre → placed variant's shape <c>rotateY</c> → configured centre
/// → part yaw → spin about Z → scale → minus pivot.
/// </summary>
public sealed class ClockRenderer : IRenderer, IDisposable
{
    private readonly ICoreClientAPI capi;
    private readonly BlockEntityClock blockEntity;
    private readonly ClockConfig config;
    private readonly Matrixf modelMatrix = new();
    private readonly float shapeYaw;
    private MultiTextureMeshRef? hourMesh;
    private MultiTextureMeshRef? minuteMesh;
    private readonly MultiTextureMeshRef?[] partMeshes;
    private double partTime;
    private double smoothedTotalHours;
    private float stormHourAngle;
    private float stormMinuteAngle;
    private SystemTemporalStability? temporalStability;

    private const float StormSpinSpeed = GameMath.TWOPI * 1.5f;
    private const float StormMinuteSpinSpeed = -StormSpinSpeed * 2f;

    public double RenderOrder => 0.5;
    public int RenderRange => 64;

    public ClockRenderer(ICoreClientAPI capi, BlockEntityClock blockEntity)
    {
        this.capi = capi;
        this.blockEntity = blockEntity;
        config = blockEntity.Config;
        shapeYaw = (blockEntity.Block?.Shape?.rotateY ?? 0f) * GameMath.DEG2RAD;
        hourMesh = LoadMesh(config.HourHand, "hour");
        minuteMesh = LoadMesh(config.MinuteHand, "minute");
        partMeshes = config.Parts.Select((p, i) => LoadMesh(p.Shape, $"part{i}")).ToArray();
        smoothedTotalHours = capi.World.Calendar.TotalHours;
        stormHourAngle = ClockTime.GetHourAngle(smoothedTotalHours);
        stormMinuteAngle = ClockTime.GetMinuteAngle(smoothedTotalHours);
        temporalStability = capi.ModLoader.GetModSystem<SystemTemporalStability>();
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, $"clock-{blockEntity.Pos}");
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (blockEntity.Block == null) return;

        var cameraPos = capi.World.Player.Entity.CameraPos;
        if (cameraPos.SquareDistanceTo(blockEntity.Pos.ToVec3d()) > RenderRange * RenderRange) return;

        partTime += deltaTime;

        double targetHours = capi.World.Calendar.TotalHours;
        double diff = targetHours - smoothedTotalHours;

        const double SnapThreshold = 0.05;
        if (Math.Abs(diff) > SnapThreshold)
        {
            smoothedTotalHours = targetHours;
        }
        else
        {
            const double SmoothSpeed = 8.0;
            double lerpFactor = 1.0 - Math.Exp(-SmoothSpeed * deltaTime);
            smoothedTotalHours += diff * lerpFactor;
        }

        float hourAngle = ClockTime.GetHourAngle(smoothedTotalHours);
        float minuteAngle = ClockTime.GetMinuteAngle(smoothedTotalHours);

        bool stormActive = config.StormSpin && temporalStability?.StormData?.nowStormActive == true;
        if (stormActive)
        {
            stormHourAngle = (stormHourAngle + deltaTime * StormSpinSpeed) % GameMath.TWOPI;
            stormMinuteAngle = GameMath.Mod(stormMinuteAngle + deltaTime * StormMinuteSpinSpeed, GameMath.TWOPI);
            hourAngle = stormHourAngle;
            minuteAngle = stormMinuteAngle;
        }
        else
        {
            stormHourAngle = hourAngle;
            stormMinuteAngle = minuteAngle;
        }

        for (int i = 0; i < config.Parts.Length; i++)
        {
            var part = config.Parts[i];
            Render(partMeshes[i], cameraPos, part.Center, part.Yaw, part.AngleAt(partTime), part.Scale, part.Pivot, 0f);
        }

        foreach (var dial in config.Dials)
        {
            // ClockTime angles run clockwise for a viewer on the +Z side; a north-facing dial is seen from −Z.
            float sign = dial.FacesSouth ? 1f : -1f;
            float outward = dial.FacesSouth ? 1f : -1f;
            Render(minuteMesh, cameraPos, dial.Center, dial.Yaw, sign * minuteAngle, dial.HandScale, config.HandPivot, outward * config.MinuteHandOffset);
            Render(hourMesh, cameraPos, dial.Center, dial.Yaw, sign * hourAngle, dial.HandScale, config.HandPivot, 0f);
        }
    }

    private MultiTextureMeshRef? LoadMesh(string? shapePath, string name)
    {
        if (string.IsNullOrEmpty(shapePath)) return null;
        try
        {
            Shape? shape = Shape.TryGet(capi, shapePath);
            if (shape == null)
            {
                capi.Logger.Error("[Clock] Missing {0} shape at {1}: {2}", name, blockEntity.Pos, shapePath);
                return null;
            }

            capi.Tesselator.TesselateShape(blockEntity.Block, shape, out MeshData meshData);
            return capi.Render.UploadMultiTextureMesh(meshData);
        }
        catch (Exception ex)
        {
            capi.Logger.Error("[Clock] Failed to tessellate {0} mesh at {1}: {2}", name, blockEntity.Pos, ex);
            return null;
        }
    }

    /// <param name="zOffset">Extra shift along the part's local Z before the spin (px).</param>
    private void Render(MultiTextureMeshRef? mesh, Vec3d cameraPos, float[] center, float yawDeg, float angle, float scale, float[] pivot, float zOffset)
    {
        if (mesh == null) return;
        Compose(modelMatrix.Identity(),
            (float)(blockEntity.Pos.X + 0.5 - cameraPos.X),
            (float)(blockEntity.Pos.Y + 0.5 - cameraPos.Y),
            (float)(blockEntity.Pos.Z + 0.5 - cameraPos.Z),
            shapeYaw, center, yawDeg, zOffset, angle, scale, pivot);

        IRenderAPI render = capi.Render;
        var shader = render.PreparedStandardShader(blockEntity.Pos.X, blockEntity.Pos.Y, blockEntity.Pos.Z);
        shader.ModelMatrix = modelMatrix.Values;
        shader.ViewMatrix = render.CameraMatrixOriginf;
        shader.ProjectionMatrix = render.CurrentProjectionMatrix;
        render.RenderMultiTextureMesh(mesh, "tex");
        shader.Stop();
    }

    /// <summary>
    /// Mesh → camera-relative transform: block centre (x, y, z) → shape rotateY (radians) → centre (px) → yaw (degrees)
    /// → zOffset (px) → spin about Z (radians) → scale → minus pivot (px).
    /// </summary>
    public static Matrixf Compose(Matrixf m, float x, float y, float z, float shapeYawRad, float[] center, float yawDeg, float zOffset, float angle, float scale, float[] pivot)
    {
        return m.Translate(x, y, z)
            .RotateY(shapeYawRad)
            .Translate(center[0] / 16f - 0.5f, center[1] / 16f - 0.5f, center[2] / 16f - 0.5f)
            .RotateY(yawDeg * GameMath.DEG2RAD)
            .Translate(0f, 0f, zOffset / 16f)
            .RotateZ(angle)
            .Scale(scale, scale, scale)
            .Translate(-pivot[0] / 16f, -pivot[1] / 16f, -pivot[2] / 16f);
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        hourMesh?.Dispose();
        minuteMesh?.Dispose();
        foreach (var m in partMeshes) m?.Dispose();
        hourMesh = null;
        minuteMesh = null;
    }
}
