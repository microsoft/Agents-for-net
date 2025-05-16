// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Attribute to load headers for header propagation.
/// This attribute should be applied to classes that implement the <see cref="IHeaderPropagationAttribute"/> interface.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HeaderPropagationAttribute : Attribute
{
    internal static void LoadHeaders()
    {
        // Init newly loaded assemblies
        AppDomain.CurrentDomain.AssemblyLoad += (s, o) => LoadHeadersAssembly(o.LoadedAssembly);

        // And all the ones we currently have loaded
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            LoadHeadersAssembly(assembly);
        }
    }

    private static void LoadHeadersAssembly(Assembly assembly)
    {
        foreach (var type in GetLoadHeadersTypes(assembly))
        {
#if !NETSTANDARD
            if (!typeof(IHeaderPropagationAttribute).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' is marked with [HeaderPropagation] but does not implement IHeaderPropagationAttribute.");
            }
#endif

            var loadHeaders = type.GetMethod(nameof(LoadHeaders), BindingFlags.Static | BindingFlags.Public);

            if (loadHeaders == null)
            {
                continue;
            }

            loadHeaders.Invoke(assembly, [HeaderPropagationContext.HeadersToPropagate]);
        }
    }

    private static IEnumerable<Type> GetLoadHeadersTypes(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HeaderPropagationAttribute), true).Length > 0)
            {
                yield return type;
            }
        }
    }
}
