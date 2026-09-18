using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ArcanumLib.Logging;
using Cairo;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui
{
    /// <summary>
    /// Shared pool of baked <see cref="LoadedTexture" /> instances keyed by a
    /// caller-supplied string. Many controls bake identical surfaces (every toggle
    /// bakes the same plate, every list row the same frame) — pooling them means the
    /// Cairo bake and the GL upload each happen once per unique key instead of once
    /// per control instance.
    /// </summary>
    /// <remarks>
    /// Keys must fully describe the baked content, e.g.
    /// <c>"arcanum-toggle-plate-24"</c>. Returned textures are shared — callers must
    /// not dispose them; use <see cref="Invalidate" /> or <see cref="DisposeAll" />
    /// instead. GUI code is main-thread affine; the backing store is a
    /// <see cref="ConcurrentDictionary{TKey,TValue}" /> so off-thread access is at
    /// least memory-safe.
    /// </remarks>
    public static class ArcanumTexturePool
    {
        private static readonly ConcurrentDictionary<string, LoadedTexture> _textures = new(StringComparer.Ordinal);
        private static ICoreClientAPI? _capi;

        /// <summary>Number of pooled textures.</summary>
        public static int Count => _textures.Count;

        /// <summary>
        /// Returns the pooled texture for <paramref name="key" />, baking and
        /// uploading it via <paramref name="bake" /> on first use.
        /// </summary>
        /// <param name="capi">The client API used for the texture upload.</param>
        /// <param name="key">A key fully describing the baked content.</param>
        /// <param name="bake">
        /// Produces the Cairo surface to upload. The returned surface is disposed
        /// after upload; a <see langword="null" /> return or a thrown exception
        /// yields an empty, unuploaded texture that is not cached.
        /// </param>
        /// <returns>The shared texture for <paramref name="key" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="capi" /> or <paramref name="bake" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null or whitespace.</exception>
        public static LoadedTexture GetOrCreate(ICoreClientAPI capi, string key, System.Func<ImageSurface> bake)
        {
            if (capi == null) throw new ArgumentNullException(nameof(capi));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Texture key cannot be empty.", nameof(key));
            if (bake == null) throw new ArgumentNullException(nameof(bake));

            EnsureApi(capi);

            while (true)
            {
                if (_textures.TryGetValue(key, out var existing))
                {
                    if (!existing.Disposed && existing.TextureId != 0) return existing;
                    // Stale entry (disposed externally or never uploaded): replace it.
                    _textures.TryRemove(new KeyValuePair<string, LoadedTexture>(key, existing));
                    continue;
                }

                var created = BakeTexture(capi, key, bake);
                if (created == null) return new LoadedTexture(capi);

                if (_textures.TryAdd(key, created)) return created;

                // Lost a concurrent add: drop the duplicate upload and retry.
                try { created.Dispose(); } catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Returns the pooled texture for <paramref name="key" /> if one is already
        /// uploaded, without baking.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="texture">When this method returns, contains the pooled texture, or <c>null</c>.</param>
        /// <returns><c>true</c> if a live pooled texture exists; otherwise <c>false</c>.</returns>
        public static bool TryGet(string key, out LoadedTexture? texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (_textures.TryGetValue(key, out var tex) && !tex.Disposed && tex.TextureId != 0)
            {
                texture = tex;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes and disposes the pooled texture for <paramref name="key" />, so
        /// the next <see cref="GetOrCreate" /> rebakes it.
        /// </summary>
        /// <param name="key">The key to invalidate.</param>
        /// <returns><c>true</c> if a pooled texture was removed; otherwise <c>false</c>.</returns>
        public static bool Invalidate(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!_textures.TryRemove(key, out var tex)) return false;

            try { tex.Dispose(); }
            catch (Exception ex)
            {
                StaticLogSink.Log($"[ArcanumLib] [ArcanumTexturePool] Dispose failed for '{key}': {ex.Message}");
            }
            return true;
        }

        /// <summary>
        /// Disposes every pooled texture and clears the pool. Intended for world
        /// unload or full GUI teardown; entries are rebaked on next access.
        /// </summary>
        public static void DisposeAll()
        {
            foreach (var kvp in _textures)
            {
                try { kvp.Value.Dispose(); }
                catch (Exception ex)
                {
                    StaticLogSink.Log($"[ArcanumLib] [ArcanumTexturePool] Dispose failed for '{kvp.Key}': {ex.Message}");
                }
            }
            _textures.Clear();
        }

        private static void EnsureApi(ICoreClientAPI capi)
        {
            if (ReferenceEquals(_capi, capi)) return;
            // Texture ids are GL-context bound; a new client API means the old
            // uploads are dead — drop them before baking against the new api.
            DisposeAll();
            _capi = capi;
        }

        private static LoadedTexture? BakeTexture(ICoreClientAPI capi, string key, System.Func<ImageSurface> bake)
        {
            ImageSurface? surface = null;
            try
            {
                surface = bake();
                if (surface == null)
                {
                    capi.Logger?.Warning("[ArcanumLib] [ArcanumTexturePool] Bake for '{0}' returned no surface.", key);
                    return null;
                }

                var tex = new LoadedTexture(capi)
                {
                    // Pooled textures are intentionally shared and only released via
                    // Invalidate/DisposeAll — silence VS's undisposed-texture tracker.
                    IgnoreUndisposed = true,
                };
                capi.Gui.LoadOrUpdateCairoTexture(surface, false, ref tex);
                return tex;
            }
            catch (Exception ex)
            {
                capi.Logger?.Warning("[ArcanumLib] [ArcanumTexturePool] Bake for '{0}' failed: {1}", key, ex.Message);
                return null;
            }
            finally
            {
                surface?.Dispose();
            }
        }
    }
}
