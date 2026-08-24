using System;
using System.Collections.Generic;
using Cairo;
using ArcanumLib.Gui.Theme;

namespace ArcanumLib.Gui.Icons
{
    /// <summary>
    /// Global registry for custom Cairo-drawn GUI icons keyed by an arbitrary string.
    /// Mods register renderers at startup; GUI code queries them by key at compose/render time.
    /// </summary>
    public static class CustomIconRegistry
    {
        private static readonly Dictionary<string, ICustomIconRenderer> _renderers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Register a renderer for the given key. Overwrites any existing.</summary>
        public static void Register(string key, ICustomIconRenderer renderer)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (renderer == null) return;
            _renderers[key] = renderer;
        }

        /// <summary>
        /// Register a delegate-based vector icon under the given key.
        /// Convenience overload for simple stateless icons that only need ctx/cx/cy/radius/color.
        /// </summary>
        public static void Register(string key,
            Action<Context, double, double, double, RGBA> draw)
        {
            if (string.IsNullOrWhiteSpace(key) || draw == null) return;
            _renderers[key] = new VectorIcon(draw);
        }

        /// <summary>Try to get a renderer by key.</summary>
        public static bool TryGet(string key, out ICustomIconRenderer? renderer)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                renderer = null;
                return false;
            }
            return _renderers.TryGetValue(key, out renderer);
        }

        /// <summary>
        /// Try to draw a registered icon with an explicit color.
        /// Returns true if a renderer was found and invoked, false otherwise.
        /// </summary>
        public static bool TryDraw(string key, Context ctx, double cx, double cy, double radius, RGBA color)
        {
            if (!TryGet(key, out var renderer) || renderer == null) return false;
            renderer.Draw(ctx, cx, cy, radius, color);
            return true;
        }

        /// <summary>
        /// Try to draw a registered icon without an explicit color.
        /// The caller should set the source color on the Cairo context beforehand.
        /// Returns true if a renderer was found and invoked, false otherwise.
        /// </summary>
        public static bool TryDraw(string key, Context ctx, double cx, double cy, double radius)
        {
            if (!TryGet(key, out var renderer) || renderer == null) return false;
            renderer.Draw(ctx, cx, cy, radius);
            return true;
        }

        /// <summary>Check whether a renderer is registered for the key.</summary>
        public static bool Has(string key)
            => !string.IsNullOrWhiteSpace(key) && _renderers.ContainsKey(key);

        /// <summary>Remove a registered renderer.</summary>
        public static bool Unregister(string key)
            => _renderers.Remove(key);

        /// <summary>Clear every registered renderer.</summary>
        public static void Clear() => _renderers.Clear();
    }
}
