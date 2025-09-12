// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Core.Serialization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class SerializationInitAttribute : Attribute
    {
        internal static void InitSerialization()
        {
            var go = Stopwatch.StartNew();

            // Register handler for new assembly loads.  This is needed because
            // C# doesn't load a package until accessed.

            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);
            System.Diagnostics.Trace.WriteLine($"SerializationInitAttribute.Register Assembly Loader: At {go.ElapsedMilliseconds}ms");

            // Call serialization init on currently loaded assemblies.
            var listOfAssembliesForScaning = AppDomain.CurrentDomain.GetAssemblies().Where(w => w.GetTypes().Where(a => Attribute.IsDefined(a, typeof(SerializationInitAttribute))).Any());
            System.Diagnostics.Trace.WriteLine($"SerializationInitAttribute.FilterAssemblies: At {go.ElapsedMilliseconds}ms - Found: {l.Count()}");
            foreach (var assembly in listOfAssembliesForScaning)
            {
                InitAssembly(assembly);
            }
            System.Diagnostics.Trace.WriteLine($"SerializationInitAttribute.RunInit: At {go.ElapsedMilliseconds}ms");

            go.Stop();
            System.Diagnostics.Trace.WriteLine($"SerializationInitAttribute.InitSerialization total: {go.ElapsedMilliseconds}ms");
        }

        private static void InitAssembly(Assembly assembly)
        {
            var b = Stopwatch.StartNew();
            if (!assembly.GetTypes().Any(a => Attribute.IsDefined(a, typeof(SerializationInitAttribute))))
            {
                b.Stop();
                return;
            }

            foreach (var type in GetLoadOnInitTypes(assembly))
            {
                var init = type.GetMethod("Init", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                System.Diagnostics.Trace.WriteLine($"InitAssembly.GetMethod {assembly.FullName}: At {b.ElapsedMilliseconds}ms");

                init?.Invoke(assembly, null);
                System.Diagnostics.Trace.WriteLine($"InitAssembly.InvokeInitMethod {assembly.FullName}: AT {b.ElapsedMilliseconds}ms");
            }
            b.Stop();
            System.Diagnostics.Trace.WriteLine($"InitAssembly {assembly.FullName}: {b.ElapsedMilliseconds}ms");
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
