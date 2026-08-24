using System;

namespace ArcanumLib.Gui.Icons
{
    /// <summary>
    /// Marks a class implementing <see cref="ICustomIconRenderer" /> for automatic
    /// registration into <see cref="CustomIconRegistry" /> via
    /// <see cref="IconRegistrar.ScanAndRegister" />.
    /// The key should be a globally unique string, typically prefixed with the mod id
    /// (e.g. <c>mymod:myicon</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class IconKeyAttribute : Attribute
    {
        /// <summary>The registry key under which the icon will be registered.</summary>
        public string Key { get; }

        /// <summary>Optional aliases that map to the same icon.</summary>
        public string[] Aliases { get; }

        /// <summary>
        /// Create an <see cref="IconKeyAttribute" />.
        /// </summary>
        /// <param name="key">The primary registry key.</param>
        /// <param name="aliases">Optional alias keys that resolve to the same icon.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key" /> is <see langword="null" />.</exception>
        public IconKeyAttribute(string key, params string[] aliases)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Aliases = aliases ?? Array.Empty<string>();
        }
    }
}
