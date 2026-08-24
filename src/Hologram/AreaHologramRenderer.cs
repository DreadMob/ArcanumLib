using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Theme;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Hologram;

/// <summary>
/// Renders multiple floating holograms in an area by scanning nearby chunks for <see cref="IHologramTextSource" />.
/// </summary>
public class AreaHologramRenderer : IRenderer, IDisposable
{
    private const int CacheRefreshIntervalMs = 1000;
    private const int CacheRefreshMoveThresholdBlocks = 4;
    private const int OcclusionCheckIntervalMs = 100;

    private readonly ICoreClientAPI _capi;
    private readonly System.Func<BlockEntity, IHologramTextSource?> _sourceFactory;
    private readonly HologramTextureOptions _options;
    private readonly int _range;
    private readonly int _yRange;
    private readonly int _maxSources;
    private readonly string? _renderKey;
    private readonly Dictionary<BlockPos, AreaHologramCache> _cache = new();

    private long _lastCacheRefreshMs;
    private int _lastCacheRefreshPx;
    private int _lastCacheRefreshPy;
    private int _lastCacheRefreshPz;

    /// <summary>Render priority within the Ortho stage.</summary>
    public double RenderOrder => 1.0;

    /// <summary>Recommended cull range for the renderer.</summary>
    public int RenderRange => _range;

    /// <summary>
    /// Creates an area hologram renderer.
    /// </summary>
    /// <param name="capi">Client API.</param>
    /// <param name="sourceFactory">Converts a block entity to a hologram source, or null if it does not provide one.</param>
    /// <param name="options">Texture generation options.</param>
    /// <param name="range">Horizontal scan range in blocks.</param>
    /// <param name="yRange">Vertical scan range in blocks.</param>
    /// <param name="maxSources">Maximum number of holograms to keep in the cache.</param>
    /// <param name="renderKey">Optional render key. When provided the renderer is registered automatically.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capi" /> is <see langword="null" />.</exception>
    public AreaHologramRenderer(ICoreClientAPI capi, System.Func<BlockEntity, IHologramTextSource?> sourceFactory, HologramTextureOptions options, int range = 48, int yRange = 8, int maxSources = 20, string? renderKey = null)
    {
        _capi = capi ?? throw new ArgumentNullException(nameof(capi));
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _range = range;
        _yRange = yRange;
        _maxSources = maxSources;
        _renderKey = renderKey;

        if (!string.IsNullOrEmpty(_renderKey))
            _capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, _renderKey);
    }

    /// <summary>Renders all visible holograms in the cache for this frame.</summary>
    /// <param name="deltaTime">The delta time value.</param>
    /// <param name="stage">The stage value.</param>
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho) return;
        if (_capi?.World?.Player?.Entity?.Pos == null) return;

        var plrPos = _capi.World.Player.Entity.Pos;
        int px = (int)Math.Floor(plrPos.X);
        int py = (int)Math.Floor(plrPos.Y);
        int pz = (int)Math.Floor(plrPos.Z);

        long nowMs = _capi.InWorldEllapsedMilliseconds;
        RefreshCacheIfNeeded(px, py, pz, nowMs);

        if (_cache.Count == 0) return;

        var rapi = _capi.Render;
        var eyePos = plrPos.XYZ.AddCopy(0, _capi.World.Player.Entity.LocalEyePos.Y, 0);

        foreach (var entry in _cache.Values)
        {
            if (entry.Source == null || !entry.Source.IsHologramVisible()) continue;

            var pos = entry.Source.Position;
            if (pos == null) continue;

            var worldPos = new Vec3d(pos.X + 0.5, pos.Y + entry.Source.GetHologramHeightOffset(), pos.Z + 0.5);

            double dx = plrPos.X - worldPos.X;
            double dy = plrPos.Y - worldPos.Y;
            double dz = plrPos.Z - worldPos.Z;
            float range = entry.Source.GetHologramRange();
            if (dx * dx + dy * dy + dz * dz > range * range) continue;

            if (!entry.Source.IsHologramVisibleThroughWalls())
            {
                if (nowMs - entry.LastOcclusionCheckMs >= OcclusionCheckIntervalMs)
                {
                    entry.IsOccluded = HologramRenderUtils.IsOccluded(_capi, eyePos, worldPos, pos);
                    entry.LastOcclusionCheckMs = nowMs;
                }

                if (entry.IsOccluded) continue;
            }

            var screenPos = MatrixToolsd.Project(worldPos,
                rapi.PerspectiveProjectionMat,
                rapi.PerspectiveViewMat,
                rapi.FrameWidth,
                rapi.FrameHeight);
            if (screenPos.Z < 0.0) continue;

            string? text = entry.Source.GetHologramText();
            if (string.IsNullOrWhiteSpace(text)) continue;

            long version = entry.Source.GetHologramVersion();
            if (entry.Texture == null || entry.Texture.Version != version || !entry.Texture.IsValid)
            {
                entry.Texture?.Dispose();
                var options = GetOptionsForSource(entry.Source);
                entry.Texture = HologramTextureGenerator.Generate(_capi, text, options, version);
            }

            if (entry.Texture?.Texture == null || entry.Texture.Texture.TextureId == 0) continue;

            float scale = HologramRenderUtils.ComputeScale((float)screenPos.Z);
            float w = scale * entry.Texture.Width;
            float h = scale * entry.Texture.Height;
            float posx = (float)screenPos.X - w / 2f;
            float posy = rapi.FrameHeight - (float)screenPos.Y - h;

            rapi.Render2DTexture(entry.Texture.Texture.TextureId, posx, posy, w, h, 20f);
        }
    }

    private void RefreshCacheIfNeeded(int px, int py, int pz, long nowMs)
    {
        bool timeExpired = nowMs - _lastCacheRefreshMs >= CacheRefreshIntervalMs;
        bool movedFarEnough =
            Math.Abs(px - _lastCacheRefreshPx) >= CacheRefreshMoveThresholdBlocks
            || Math.Abs(py - _lastCacheRefreshPy) >= CacheRefreshMoveThresholdBlocks
            || Math.Abs(pz - _lastCacheRefreshPz) >= CacheRefreshMoveThresholdBlocks;

        if (!timeExpired && !movedFarEnough) return;

        RefreshCache(px, py, pz, nowMs);
    }

    private void RefreshCache(int px, int py, int pz, long nowMs)
    {
        var next = new Dictionary<BlockPos, AreaHologramCache>();
        if (_capi == null) return;
        var blockAccessor = _capi.World.BlockAccessor;
        if (blockAccessor == null) return;

        int chunkSize = GlobalConstants.ChunkSize;
        int minCx = DivFloor(px - _range, chunkSize);
        int maxCx = DivFloor(px + _range, chunkSize);
        int minCy = DivFloor(py - _yRange, chunkSize);
        int maxCy = DivFloor(py + _yRange, chunkSize);
        int minCz = DivFloor(pz - _range, chunkSize);
        int maxCz = DivFloor(pz + _range, chunkSize);

        for (int cx = minCx; cx <= maxCx; cx++)
        {
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    var chunk = blockAccessor.GetChunk(cx, cy, cz);
                    if (chunk == null || chunk.Disposed) continue;

                    var blockEntities = chunk.BlockEntities;
                    if (blockEntities == null || blockEntities.Count == 0) continue;

                    foreach (var kvp in blockEntities)
                    {
                        if (next.Count >= _maxSources) break;

                        var bePos = kvp.Key;
                        if (bePos == null) continue;

                        int dx = bePos.X - px;
                        if (dx < -_range || dx > _range) continue;
                        int dz = bePos.Z - pz;
                        if (dz < -_range || dz > _range) continue;
                        int dy = bePos.Y - py;
                        if (dy < -_yRange || dy > _yRange) continue;

                        var source = _sourceFactory(kvp.Value);
                        if (source == null || !source.IsHologramVisible()) continue;

                        string? text = source.GetHologramText();
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        if (next.ContainsKey(bePos)) continue;

                        if (_cache.TryGetValue(bePos, out var existing)
                            && existing.Texture != null
                            && existing.Texture.Version == source.GetHologramVersion())
                        {
                            next[bePos] = existing;
                            continue;
                        }

                        long version = source.GetHologramVersion();
                        var options = GetOptionsForSource(source);
                        var texture = HologramTextureGenerator.Generate(_capi, text, options, version);
                        next[bePos] = new AreaHologramCache
                        {
                            Source = source,
                            Texture = texture,
                            LastOcclusionCheckMs = long.MinValue
                        };
                    }
                }
            }
        }

        foreach (var kvp in _cache)
        {
            if (!next.ContainsKey(kvp.Key))
                kvp.Value.Texture?.Dispose();
        }

        _cache.Clear();
        foreach (var kvp in next)
            _cache[kvp.Key] = kvp.Value;

        _lastCacheRefreshMs = nowMs;
        _lastCacheRefreshPx = px;
        _lastCacheRefreshPy = py;
        _lastCacheRefreshPz = pz;
    }

    private HologramTextureOptions GetOptionsForSource(IHologramTextSource source)
    {
        var options = _options.Clone();
        var color = source.GetHologramColor();
        if (color is { Length: >= 4 })
            options.TextColor = new RGBA(color[0], color[1], color[2], color[3]);
        return options;
    }

    private static int DivFloor(int a, int b)
    {
        int q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0)) q--;
        return q;
    }

    /// <summary>Disposes all cached textures and unregisters the renderer when it was registered.</summary>
    public void Dispose()
    {
        foreach (var entry in _cache.Values)
            entry.Texture?.Dispose();
        _cache.Clear();

        if (!string.IsNullOrEmpty(_renderKey))
            _capi?.Event?.UnregisterRenderer(this, EnumRenderStage.Ortho);
    }

    private class AreaHologramCache
    {
        public IHologramTextSource Source = null!;
        public HologramTexture? Texture;
        public bool IsOccluded;
        public long LastOcclusionCheckMs = long.MinValue;
    }
}
