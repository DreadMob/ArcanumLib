using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Hologram;

/// <summary>
/// Renders a single floating hologram above one <see cref="IHologramTextSource"/>.
/// </summary>
public class SingleHologramRenderer : IRenderer, IDisposable
{
    private readonly ICoreClientAPI _capi;
    private readonly IHologramTextSource _source;
    private readonly HologramTextureOptions _options;
    private readonly string? _renderKey;
    private HologramTexture? _texture;

    /// <summary>Render priority within the Ortho stage.</summary>
    public double RenderOrder => 1.0;

    /// <summary>Recommended cull range for the renderer.</summary>
    public int RenderRange => (int)_source.GetHologramRange();

    /// <summary>
    /// Creates a renderer for the given source and options.
    /// </summary>
    /// <param name="capi">Client API.</param>
    /// <param name="source">The hologram data source.</param>
    /// <param name="options">Texture generation options.</param>
    /// <param name="renderKey">Optional render key. When provided the renderer is registered automatically.</param>
    public SingleHologramRenderer(ICoreClientAPI capi, IHologramTextSource source, HologramTextureOptions options, string? renderKey = null)
    {
        _capi = capi ?? throw new ArgumentNullException(nameof(capi));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _renderKey = renderKey;

        if (!string.IsNullOrEmpty(_renderKey))
            _capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, _renderKey);
    }

    /// <summary>Renders the hologram for this frame.</summary>
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho) return;
        if (_capi?.World?.Player?.Entity?.Pos == null) return;
        if (!_source.IsHologramVisible()) return;

        var plrPos = _capi.World.Player.Entity.Pos;
        var pos = _source.Position;
        var worldPos = new Vec3d(pos.X + 0.5, pos.Y + _source.GetHologramHeightOffset(), pos.Z + 0.5);

        double dx = plrPos.X - worldPos.X;
        double dy = plrPos.Y - worldPos.Y;
        double dz = plrPos.Z - worldPos.Z;
        float range = _source.GetHologramRange();
        if (dx * dx + dy * dy + dz * dz > range * range) return;

        if (!_source.IsHologramVisibleThroughWalls())
        {
            var eyePos = plrPos.XYZ.AddCopy(0, _capi.World.Player.Entity.LocalEyePos.Y, 0);
            if (HologramRenderUtils.IsOccluded(_capi, eyePos, worldPos, pos)) return;
        }

        var screenPos = MatrixToolsd.Project(worldPos,
            _capi.Render.PerspectiveProjectionMat,
            _capi.Render.PerspectiveViewMat,
            _capi.Render.FrameWidth,
            _capi.Render.FrameHeight);
        if (screenPos.Z < 0.0) return;

        string? text = _source.GetHologramText();
        if (string.IsNullOrWhiteSpace(text)) return;

        long version = _source.GetHologramVersion();
        if (_texture == null || _texture.Version != version || !_texture.IsValid)
        {
            _texture?.Dispose();
            _texture = HologramTextureGenerator.Generate(_capi, text, _options, version);
        }

        if (_texture?.Texture == null || _texture.Texture.TextureId == 0) return;

        float scale = HologramRenderUtils.ComputeScale((float)screenPos.Z);
        float w = scale * _texture.Width;
        float h = scale * _texture.Height;
        float posx = (float)screenPos.X - w / 2f;
        float posy = _capi.Render.FrameHeight - (float)screenPos.Y - h;

        _capi.Render.Render2DTexture(_texture.Texture.TextureId, posx, posy, w, h, 20f);
    }

    /// <summary>Invalidates the cached texture so it is rebuilt on the next frame.</summary>
    public void UpdateSettings()
    {
        _texture?.Dispose();
        _texture = null;
    }

    /// <summary>Immediately invalidates the cached texture.</summary>
    public void ForceRegen()
    {
        UpdateSettings();
    }

    /// <summary>Disposes the texture and unregisters the renderer when it was registered.</summary>
    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;

        if (!string.IsNullOrEmpty(_renderKey))
            _capi?.Event?.UnregisterRenderer(this, EnumRenderStage.Ortho);
    }
}
