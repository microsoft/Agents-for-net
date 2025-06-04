// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Agents.Core.Serialization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class SerializationInitAttribute : Attribute
    {
        internal static void InitSerialization()
        {
            // Register handler for new assembly loads.  This is needed because
            // C# doesn't load a package until accessed.
            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);

            // Call serialization init on currently loaded assemblies.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                InitAssembly(assembly);
            }
        }

        private static void InitAssembly(Assembly assembly)
        {
            foreach (var type in GetLoadOnInitTypes(assembly))
            {
                var init = type.GetMethod("Init", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                init?.Invoke(assembly, null);
            }
        }

        private static IEnumerable<Type> GetLoadOnInitTypes(Assembly assembly)
        {
            IList<Type> result = [];

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"SerializationInitAttribute.GetLoadOnInitTypes: {ex.Message}");
                return result;
            }

            foreach (Type type in types)
            {
                if (type.GetCustomAttributes(typeof(SerializationInitAttribute), true).Length > 0)
                {
                    result.Add(type);
                }
            }

            return result;
        }
    }
}
