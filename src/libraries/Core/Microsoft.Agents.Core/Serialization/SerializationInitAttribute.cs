// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Agents.Core.Serialization
{
    public class SerializationInitAttribute : Attribute
    {
        internal static void InitSerialization()
        {
            //init newly loaded assemblies
            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);
            //and all the ones we currently have loaded
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
                if (init != null)
                {
                    init.Invoke(assembly, null);
                }
            }
        }

        private static IEnumerable<Type> GetLoadOnInitTypes(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(SerializationInitAttribute), true).Length > 0)
                {
                    yield return type;
                }
            }
        }
    }
}
