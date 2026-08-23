using System;
using System.Collections.Generic;

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
