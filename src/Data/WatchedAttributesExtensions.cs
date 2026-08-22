using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace ArcanumLib.Data
{
    /// <summary>
    /// Extension methods for <see cref="ITreeAttribute"/> / <see cref="Entity.WatchedAttributes"/>.
    /// </summary>
    public static class WatchedAttributesExtensions
    {
        /// <summary>
        /// Returns an existing tree attribute or creates and attaches a new one.
        /// </summary>
        public static ITreeAttribute GetOrCreateTreeAttribute(this ITreeAttribute tree, string key)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
            return tree.GetOrAddTreeAttribute(key);
        }

        /// <summary>
        /// Returns an existing tree attribute on the entity or creates one.
        /// </summary>
        public static ITreeAttribute? GetOrCreateTreeAttribute(this Entity? entity, string key)
            => entity?.WatchedAttributes?.GetOrCreateTreeAttribute(key);

        /// <summary>
        /// Gets an existing integer or writes and returns <paramref name="defaultValue"/>.
        /// </summary>
        public static int GetOrCreateInt(this ITreeAttribute tree, string key, int defaultValue = 0)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (tree.HasAttribute(key)) return tree.GetInt(key, defaultValue);
            tree.SetInt(key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Gets an existing long or writes and returns <paramref name="defaultValue"/>.
        /// </summary>
        public static long GetOrCreateLong(this ITreeAttribute tree, string key, long defaultValue = 0)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (tree.HasAttribute(key)) return tree.GetLong(key, defaultValue);
            tree.SetLong(key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Gets an existing float or writes and returns <paramref name="defaultValue"/>.
        /// </summary>
        public static float GetOrCreateFloat(this ITreeAttribute tree, string key, float defaultValue = 0f)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (tree.HasAttribute(key)) return tree.GetFloat(key, defaultValue);
            tree.SetFloat(key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Gets an existing double or writes and returns <paramref name="defaultValue"/>.
        /// </summary>
        public static double GetOrCreateDouble(this ITreeAttribute tree, string key, double defaultValue = 0.0)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (tree.HasAttribute(key)) return tree.GetDouble(key, defaultValue);
            tree.SetDouble(key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Gets an existing boolean or writes and returns <paramref name="defaultValue"/>.
        /// </summary>
        public static bool GetOrCreateBool(this ITreeAttribute tree, string key, bool defaultValue = false)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (tree.HasAttribute(key)) return tree.GetBool(key, defaultValue);
            tree.SetBool(key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Gets an existing string or writes and returns <paramref name="defaultValue"/>.
        /// </summary>
        public static string GetOrCreateString(this ITreeAttribute tree, string key, string defaultValue = "")
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (tree.HasAttribute(key)) return tree.GetString(key, defaultValue) ?? defaultValue;
            tree.SetString(key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Sets an integer only if the key does not already exist.
        /// </summary>
        public static void SetIntIfMissing(this ITreeAttribute tree, string key, int value)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (!tree.HasAttribute(key)) tree.SetInt(key, value);
        }

        /// <summary>
        /// Sets a long only if the key does not already exist.
        /// </summary>
        public static void SetLongIfMissing(this ITreeAttribute tree, string key, long value)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (!tree.HasAttribute(key)) tree.SetLong(key, value);
        }

        /// <summary>
        /// Sets a float only if the key does not already exist.
        /// </summary>
        public static void SetFloatIfMissing(this ITreeAttribute tree, string key, float value)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (!tree.HasAttribute(key)) tree.SetFloat(key, value);
        }

        /// <summary>
        /// Sets a double only if the key does not already exist.
        /// </summary>
        public static void SetDoubleIfMissing(this ITreeAttribute tree, string key, double value)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (!tree.HasAttribute(key)) tree.SetDouble(key, value);
        }

        /// <summary>
        /// Sets a boolean only if the key does not already exist.
        /// </summary>
        public static void SetBoolIfMissing(this ITreeAttribute tree, string key, bool value)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (!tree.HasAttribute(key)) tree.SetBool(key, value);
        }

        /// <summary>
        /// Sets a string only if the key does not already exist.
        /// </summary>
        public static void SetStringIfMissing(this ITreeAttribute tree, string key, string value)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (!tree.HasAttribute(key)) tree.SetString(key, value);
        }
    }
}
