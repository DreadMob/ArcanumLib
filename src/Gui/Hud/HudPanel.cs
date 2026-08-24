using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic base class for a data-driven HUD panel rendered via Cairo.
/// Manages texture caching, invalidation and disposal. Derived types implement
/// <see cref="BuildCacheKey" /> and <see cref="DrawPanelContent" />.
/// </summary>
/// <typeparam name="TSnapshot">The type of the tsnapshot value.</typeparam>
/// <typeparam name="THudDefinition">The type of the thuddefinition value.</typeparam>
/// <typeparam name="TTheme">The type of the ttheme value.</typeparam>
public abstract class HudPanel<TSnapshot, THudDefinition, TTheme> : GuiElement, IDisposable
    where TSnapshot : class, IHudSnapshot
    where THudDefinition : class
    where TTheme : HudTheme
{
    /// <summary>Current snapshot to render.</summary>
    protected TSnapshot? _snapshot;

    /// <summary>Current HUD definition driving layout and elements.</summary>
    protected THudDefinition? _definition;

    /// <summary>Current theme driving colours and fonts.</summary>
    protected TTheme? _theme;

    /// <summary>Cached rendered texture.</summary>
    protected LoadedTexture _cachedTexture;

    /// <summary>Invalidation key; when it changes the texture is regenerated.</summary>
    protected string? _cacheKey;

    /// <summary>Client time in ms when the last snapshot was received, used for interpolation.</summary>
    protected long _snapshotReceivedMs;

    /// <summary>Creates the panel and its cached texture.</summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="bounds">The bounds value.</param>
    protected HudPanel(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
    {
        _cachedTexture = new LoadedTexture(capi);
    }

    /// <summary>
    /// Called after a new snapshot is received. Invalidates the cached texture and records the receive time.
    /// </summary>
    /// <param name="snapshot">The snapshot value.</param>
    /// <param name="definition">The definition value.</param>
    /// <param name="theme">The theme value.</param>
    public virtual void Update(TSnapshot? snapshot, THudDefinition? definition, TTheme? theme)
    {
        _snapshot = snapshot;
        _definition = definition;
        _theme = theme;
        OnSnapshotReceived(api?.World?.ElapsedMilliseconds ?? 0);
    }

    /// <summary>
    /// Called after a new snapshot is received. Invalidates the cached texture and records the receive time.
    /// </summary>
    /// <param name="receivedMs">The received ms value.</param>
    protected virtual void OnSnapshotReceived(long receivedMs)
    {
        _snapshotReceivedMs = receivedMs;
        _cacheKey = null;
    }

    /// <summary>Builds a cache key from the current snapshot. Must be implemented by derived panels.</summary>
    /// <returns>The cache key, or null if none is found.</returns>
    protected abstract string? BuildCacheKey();

    /// <summary>Renders the full panel content into the provided Cairo context.</summary>
    /// <param name="ctx">The ctx value.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    protected abstract void DrawPanelContent(Context ctx, int width, int height);

    /// <summary>Composes the static panel surface; the default implementation is empty.</summary>
    /// <param name="ctxStatic">The ctx static value.</param>
    /// <param name="surfaceStatic">The surface static value.</param>
    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic) { }

    /// <summary>Renders the cached panel texture and regenerates it when the cache key changes.</summary>
    /// <param name="deltaTime">The delta time value.</param>
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (Bounds?.ParentBounds == null) return;
        RegenerateIfNeeded();
        if (_cachedTexture?.TextureId > 0)
            api.Render.Render2DLoadedTexture(_cachedTexture, (float)Bounds.absX, (float)Bounds.absY);
    }

    private void RegenerateIfNeeded()
    {
        if (api?.Render == null || _cachedTexture == null || Bounds == null) return;

        Bounds.CalcWorldBounds();

        string? newKey = BuildCacheKey();
        if (string.IsNullOrEmpty(newKey)) return;
        newKey += $"|{(int)Bounds.OuterWidth}|{(int)Bounds.OuterHeight}";

        if (string.Equals(_cacheKey, newKey, StringComparison.Ordinal) && _cachedTexture.TextureId > 0) return;

        _cacheKey = newKey;
        int width = Math.Max(1, (int)Bounds.OuterWidth);
        int height = Math.Max(1, (int)Bounds.OuterHeight);

        ImageSurface? surface = null;
        Context? ctx = null;
        try
        {
            surface = new ImageSurface(Format.Argb32, width, height);
            ctx = new Context(surface);
            ctx.SetSourceRGBA(0, 0, 0, 0);
            ctx.Paint();

            DrawPanelContent(ctx, width, height);
            generateTexture(surface, ref _cachedTexture);
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning("[ArcanumLib] HudPanel render failed: {0}", ex.Message);
            _cacheKey = null;
            _cachedTexture?.Dispose();
            _cachedTexture = new LoadedTexture(api);
        }
        finally
        {
            ctx?.Dispose();
            surface?.Dispose();
        }
    }

    /// <summary>Disposes the cached texture.</summary>
    public new virtual void Dispose()
    {
        _cachedTexture?.Dispose();
        _cachedTexture = null!;
        base.Dispose();
    }
}
