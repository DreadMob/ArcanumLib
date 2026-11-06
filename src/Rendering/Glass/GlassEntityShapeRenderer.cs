#nullable enable
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ArcanumLib.Rendering.Glass;

/// <summary>
/// Entity shape renderer that draws real see-through glass. The vanilla renderer draws a whole entity in
/// one opaque pass: a half-transparent pixel blends but still writes depth, so everything drawn later behind
/// the pane (cab, passengers, other entities, water) is hidden. Shape elements marked
/// <c>"renderPass": 3</c> (Transparent) are split off into their own mesh and drawn after the world's
/// transparent pass, blended, without writing depth, back faces culled. Everything else renders exactly as
/// with the vanilla "Shape" renderer. Entities only: chunk-meshed blocks get the same from the game itself,
/// which sorts faces of an element with <c>"renderPass": 3</c> into its transparent pool.
/// Use from data with <c>"renderer": "ArcanumGlassShape"</c> in the entity's client section.
/// </summary>
public class GlassEntityShapeRenderer : EntityShapeRenderer
{
    /// <summary>Entity renderer class name registered by ArcanumLib on the client.</summary>
    public const string ClassName = "ArcanumGlassShape";

    private const int TransparentPass = (int)EnumChunkRenderPass.Transparent;

    private MultiTextureMeshRef? _glassRef;

    /// <summary>Creates the renderer for <paramref name="entity"/>.</summary>
    public GlassEntityShapeRenderer(Entity entity, ICoreClientAPI api) : base(entity, api) { }

    /// <summary>Glass faces of <paramref name="mesh"/> (null when there are none) and the rest.</summary>
    public static (MeshData solid, MeshData? glass) Split(MeshData mesh)
    {
        var passes = mesh.RenderPassesAndExtraBits;
        int faces = mesh.VerticesCount / mesh.VerticesPerFace;
        if (passes == null || mesh.RenderPassCount < faces) return (mesh, null);

        bool any = false;
        for (int i = 0; i < faces && !any; i++) any = IsGlass(passes[i]);
        if (!any) return (mesh, null);

        var solid = mesh.EmptyClone();
        solid.AddMeshData(mesh, i => !IsGlass(passes[i]));
        var glass = mesh.EmptyClone();
        glass.AddMeshData(mesh, i => IsGlass(passes[i]));
        return (solid, glass);
    }

    // the low bits hold the pass; +1024 marks DisableRandomDrawOffset
    private static bool IsGlass(short pass) => (pass & 0x3ff) == TransparentPass;

    /// <inheritdoc />
    protected override void onMeshReady(MeshData meshData)
    {
        var (solid, glass) = Split(meshData);
        _glassRef?.Dispose();
        _glassRef = null;
        if (glass != null && glass.VerticesCount > 0 && !capi.IsShuttingDown)
            _glassRef = capi.Render.UploadMultiTextureMesh(glass);
        base.onMeshReady(solid);
    }

    /// <inheritdoc />
    public override void DoRender3DAfterOIT(float dt, bool isShadowPass)
    {
        base.DoRender3DAfterOIT(dt, isShadowPass);
        // glass casts no shadow
        if (isShadowPass || isSpectator || _glassRef == null) return;
        var animator = entity.AnimManager?.Animator;
        if (animator == null) return;

        var render = capi.Render;
        var prog = render.GetEngineShader(EnumShaderProgram.Entityanimated);
        prog.Use();
        // the same uniforms the game sets for its batched entity pass (SystemRenderEntities)
        prog.Uniform("rgbaAmbientIn", render.AmbientColor);
        prog.Uniform("rgbaFogIn", render.FogColor);
        prog.Uniform("fogMinIn", render.FogMin);
        prog.Uniform("fogDensityIn", render.FogDensity);
        prog.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
        prog.Uniform("alphaTest", 0.01f);
        prog.Uniform("lightPosition", capi.Render.ShaderUniforms.LightPosition3D);
        prog.Uniform("rgbaLightIn", lightrgbs);
        prog.Uniform("extraGlow", entity.Properties.Client.GlowLevel);
        prog.UniformMatrix("modelMatrix", ModelMat);
        prog.UniformMatrix("viewMatrix", render.CameraMatrixOriginf);
        prog.Uniform("addRenderFlags", AddRenderFlags);
        prog.Uniform("windWaveIntensity", (float)WindWaveIntensity);
        prog.Uniform("entityId", (int)entity.EntityId);
        prog.Uniform("glitchFlicker", 0);
        prog.Uniform("glitchEffectStrength", 0f);
        prog.Uniform("frostAlpha", 0f);
        prog.Uniform("waterWaveCounter", render.ShaderUniforms.WaterWaveCounter);
        prog.Uniform("renderColor", ColorUtil.WhiteArgbVec);
        prog.UBOs["Animation"].Update(animator.Matrices, 0, animator.MaxJointId * 16 * 4);

        render.GlToggleBlend(true, EnumBlendMode.Standard);
        render.GlEnableCullFace();
        render.GLDepthMask(false);
        render.RenderMultiTextureMesh(_glassRef, "entityTex", 0);
        render.GLDepthMask(true);
        render.GlDisableCullFace();
        prog.Stop();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
        _glassRef?.Dispose();
        _glassRef = null;
    }
}
