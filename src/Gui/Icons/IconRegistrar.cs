using System;
using System.Reflection;

namespace ArcanumLib.Gui.Icons
{
    /// <summary>
    /// Scans an assembly for classes decorated with <see cref="IconKeyAttribute"/>
    /// that implement <see cref="ICustomIconRenderer"/> and registers them into
    /// <see cref="CustomIconRegistry"/>. Call once during mod startup.
    /// </summary>
    public static class IconRegistrar
    {
        /// <summary>
        /// Scans the given assembly for <see cref="IconKeyAttribute"/>-decorated
        /// icon classes and registers each one (plus any aliases) into
        /// <see cref="CustomIconRegistry"/>. Types without a parameterless
        /// constructor are skipped with a warning.
        /// </summary>
        /// <param name="assembly">The assembly to scan (e.g. typeof(MyMod).Assembly).</param>
        /// <returns>The number of icons registered (primary keys only).</returns>
        public static int ScanAndRegister(Assembly assembly)
        {
            if (assembly == null) return 0;
            int count = 0;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types ?? Array.Empty<Type>();
            }

            foreach (var type in types)
            {
                if (type == null) continue;
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(ICustomIconRenderer).IsAssignableFrom(type)) continue;

                var attr = type.GetCustomAttribute<IconKeyAttribute>();
                if (attr == null) continue;

                ICustomIconRenderer? instance;
                try
                {
                    instance = (ICustomIconRenderer?)Activator.CreateInstance(type, nonPublic: true);
                }
                catch (MissingMethodException)
                {
                    continue;
                }
                if (instance == null) continue;

                CustomIconRegistry.Register(attr.Key, instance);
                count++;

                if (attr.Aliases != null)
                {
                    foreach (var alias in attr.Aliases)
                    {
                        if (!string.IsNullOrWhiteSpace(alias))
                            CustomIconRegistry.Register(alias, instance);
                    }
                }
            }

            return count;
        }
    }
}
