// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Agents.Core.HeaderPropagation;

public class HeaderPropagationAttribute : Attribute
{
    internal static void SetHeadersSerialization()
    {
        //init newly loaded assemblies
        AppDomain.CurrentDomain.AssemblyLoad += (s, o) => SetHeadersAssembly(o.LoadedAssembly);

        //and all the ones we currently have loaded
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            SetHeadersAssembly(assembly);
        }
    }

    private static void SetHeadersAssembly(Assembly assembly)
    {
        foreach (var type in GetLoadOnsetHeadersTypes(assembly))
        {
            var setHeaders = type.GetMethod("SetHeaders", BindingFlags.Static | BindingFlags.Public);
            if (setHeaders?.Invoke(assembly, null) is IDictionary<string, StringValues> headers)
            {
                HeaderPropagationContext.HeadersToPropagate = headers;
            }
        }
    }

    private static IEnumerable<Type> GetLoadOnsetHeadersTypes(Assembly assembly)
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
