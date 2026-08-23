using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ArcanumLib.Gui.Theme;
using Cairo;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ArcanumLib.Gui.Icons;

public enum IconFit
{
    None,
    Circle,
    Hexagon,
    Diamond
}

/// <summary>
/// Caches and renders icon image surfaces from the Vintage Story asset pipeline.
/// Supports PNG, JPEG, GIF, BMP, ICO, WBMP, WebP, HEIF, DNG, KTX, PKM and ASTC
/// through <see cref="SkiaSharp.SKCodec"/>, then converts decoded pixels to a
/// Cairo ARGB32 surface with alpha pre-multiplication and near-transparent noise removal.
/// </summary>
public static class ImageIconCache
{
    private static ICoreClientAPI? _capi;
    private static readonly Dictionary<string, ImageSurface> _surfaces = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, long> _missing = new(StringComparer.OrdinalIgnoreCase);
    private const long MissingRetryMs = 60000;

    public static void Init(ICoreClientAPI capi)
    {
        if (_capi == capi) return;

        // Dispose any existing surfaces before switching contexts.
        Dispose();

        _capi = capi;
        _missing.Clear();
    }

    public static void Dispose()
    {
        foreach (var s in _surfaces.Values)
        {
            try
            {
                s?.Dispose();
            }
            catch (Exception ex)
            {
                _capi?.Logger.Warning("[ImageIconCache] surface dispose error: {0}", ex.Message);
                /* non-critical */
            }
        }
        _surfaces.Clear();
        _missing.Clear();
    }

    /// <summary>
    /// Pre-loads an icon surface so the first paint does not stall the render thread.
    /// </summary>
    public static void Preload(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || _capi == null) return;
        if (IsRecentlyMissing(assetPath)) return;
        var surface = GetOrLoadSurface(assetPath);
        if (surface == null) RecordMissing(assetPath);
    }

    public static bool TryDrawIcon(Context ctx, string assetPath, double cx, double cy, double radius, RGBA color, IconFit fit = IconFit.None, bool tint = false)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || _capi == null || radius <= 0)
            return false;

        if (IsRecentlyMissing(assetPath))
            return false;

        var surface = GetOrLoadSurface(assetPath);
        if (surface == null)
        {
            RecordMissing(assetPath);
            return false;
        }

        ctx.Save();

        if (fit != IconFit.None)
        {
            if (fit == IconFit.Circle)
            {
                ctx.Arc(cx, cy, radius * 0.92, 0, 2 * Math.PI);
            }
            else if (fit == IconFit.Hexagon)
            {
                DrawHexagonPath(ctx, cx, cy, radius * 0.92);
            }
            else if (fit == IconFit.Diamond)
            {
                DrawDiamondPath(ctx, cx, cy, radius * 0.92);
            }
            ctx.Clip();
        }

        // Preserve aspect ratio and center the image within the fit radius.
        double scale = Math.Min((radius * 2) / surface.Width, (radius * 2) / surface.Height);
        if (scale <= 0) scale = 0.0001;
        double drawW = surface.Width * scale;
        double drawH = surface.Height * scale;
        ctx.Translate(cx - drawW / 2, cy - drawH / 2);
        ctx.Scale(scale, scale);

        if (tint)
        {
            ctx.SetSourceRGBA(color.R, color.G, color.B, color.A);
            ctx.MaskSurface(surface, 0, 0);
        }
        else
        {
            ctx.SetSourceSurface(surface, 0, 0);
            if (color.A < 0.995)
                ctx.PaintWithAlpha(color.A);
            else
                ctx.Paint();
        }

        ctx.Restore();
        return true;
    }

    private static ImageSurface? GetOrLoadSurface(string assetPath)
    {
        if (_surfaces.TryGetValue(assetPath, out var cached) && cached != null)
            return cached;

        try
        {
            var loc = new AssetLocation(assetPath.ToLowerInvariant());
            var surface = LoadSurface(loc);
            if (surface != null)
            {
                if (_surfaces.TryGetValue(assetPath, out var existing) && existing != null)
                {
                    try { existing.Dispose(); }
                    catch { /* non-critical */ }
                }
                _surfaces[assetPath] = surface;
            }
            else
            {
                RecordMissing(assetPath);
            }

            return surface;
        }
        catch (Exception ex)
        {
            _capi?.Logger.Warning("[ImageIconCache] failed to load icon '{0}': {1}", assetPath, ex.Message);
            RecordMissing(assetPath);
            return null;
        }
    }

    private static ImageSurface? LoadSurface(AssetLocation loc)
    {
        if (_capi == null) return null;

        var fullLoc = loc.Clone().WithPathPrefixOnce("textures/");
        IAsset asset;
        try { asset = _capi.Assets.Get(fullLoc); }
        catch (Exception ex) { _capi?.Logger?.Warning("[ImageIconCache] asset lookup failed for '{0}': {1}", fullLoc, ex.Message); return null; }

        byte[]? data = asset?.Data;
        if (data == null || data.Length == 0) return null;

        using var skData = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(skData);
        if (codec == null) return null;

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        if (bitmap.GetPixels() == IntPtr.Zero) return null;

        if (codec.GetPixels(info, bitmap.GetPixels(out _)) != SKCodecResult.Success)
            return null;

        var surface = new ImageSurface(Format.Argb32, info.Width, info.Height);
        CopyBitmapToSurface(bitmap, surface);
        PremultiplyAndCleanSurface(surface);
        return surface;
    }

    private static void CopyBitmapToSurface(SKBitmap bitmap, ImageSurface surface)
    {
        int width = Math.Min(bitmap.Width, surface.Width);
        int height = Math.Min(bitmap.Height, surface.Height);
        int srcRowBytes = bitmap.RowBytes;
        int dstStride = surface.Stride;

        var row = new int[width];
        for (int y = 0; y < height; y++)
        {
            IntPtr srcRow = IntPtr.Add(bitmap.GetPixels(out _), y * srcRowBytes);
            IntPtr dstRow = IntPtr.Add(surface.DataPtr, y * dstStride);
            Marshal.Copy(srcRow, row, 0, width);
            Marshal.Copy(row, 0, dstRow, width);
        }
    }

    /// <summary>
    /// Fixes decoded icons that contain un-multiplied alpha or colored transparent pixels.
    /// Cairo ARGB32 expects pre-multiplied color, so we pre-multiply RGB by alpha
    /// and drop isolated nearly-transparent noise pixels.
    /// </summary>
    private static void PremultiplyAndCleanSurface(ImageSurface surface)
    {
        if (surface == null || surface.Format != Format.Argb32) return;

        int width = surface.Width;
        int height = surface.Height;
        int stride = surface.Stride;
        int rowPixels = stride / 4;
        int length = height * rowPixels;

        int[] pixels = new int[length];
        try
        {
            Marshal.Copy(surface.DataPtr, pixels, 0, length);
        }
        catch (Exception ex)
        {
            _capi?.Logger.Warning("[ImageIconCache] cannot read surface pixels, leaving as-is: {0}", ex.Message);
            return;
        }

        // First pass: determine whether the data is already pre-multiplied.
        bool needsPremultiply = false;
        for (int i = 0; i < length; i++)
        {
            int p = pixels[i];
            int a = (p >> 24) & 0xFF;
            if (a == 0) continue;
            int r = (p >> 16) & 0xFF;
            int g = (p >> 8) & 0xFF;
            int b = p & 0xFF;
            if (r > a || g > a || b > a)
            {
                needsPremultiply = true;
                break;
            }
        }

        if (!needsPremultiply)
        {
            // Already pre-multiplied (or very close). Just remove fully transparent color noise.
            for (int i = 0; i < length; i++)
            {
                int p = pixels[i];
                int a = (p >> 24) & 0xFF;
                if (a <= 2)
                    pixels[i] = 0;
            }
            Marshal.Copy(pixels, 0, surface.DataPtr, length);
            surface.MarkDirty();
            return;
        }

        // Second pass: pre-multiply and strip isolated near-transparent pixels.
        const int noiseThreshold = 2;
        for (int i = 0; i < length; i++)
        {
            int p = pixels[i];
            int a = (p >> 24) & 0xFF;
            if (a == 0)
            {
                pixels[i] = 0;
                continue;
            }

            if (a <= noiseThreshold)
            {
                pixels[i] = 0;
                continue;
            }

            int r = (p >> 16) & 0xFF;
            int g = (p >> 8) & 0xFF;
            int b = p & 0xFF;

            r = r * a / 255;
            g = g * a / 255;
            b = b * a / 255;

            pixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
        }

        Marshal.Copy(pixels, 0, surface.DataPtr, length);
        surface.MarkDirty();
    }

    private static bool IsRecentlyMissing(string assetPath)
    {
        if (!_missing.TryGetValue(assetPath, out long failedAt)) return false;
        long now = _capi?.World?.ElapsedMilliseconds ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - failedAt < MissingRetryMs) return true;
        _missing.Remove(assetPath);
        return false;
    }

    private static void RecordMissing(string assetPath)
    {
        long now = _capi?.World?.ElapsedMilliseconds ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _missing[assetPath] = now;
    }

    private static void DrawHexagonPath(Context ctx, double cx, double cy, double r)
    {
        for (int i = 0; i < 6; i++)
        {
            double angle = -Math.PI / 2 + i * 2 * Math.PI / 6;
            double px = cx + Math.Cos(angle) * r;
            double py = cy + Math.Sin(angle) * r;
            if (i == 0) ctx.MoveTo(px, py);
            else ctx.LineTo(px, py);
        }
        ctx.ClosePath();
    }

    private static void DrawDiamondPath(Context ctx, double cx, double cy, double r)
    {
        ctx.MoveTo(cx, cy - r);
        ctx.LineTo(cx + r, cy);
        ctx.LineTo(cx, cy + r);
        ctx.LineTo(cx - r, cy);
        ctx.ClosePath();
    }
}
