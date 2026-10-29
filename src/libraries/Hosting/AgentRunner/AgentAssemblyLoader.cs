// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.Agents.Hosting.AgentRunner;

internal static class AgentAssemblyLoader
{
    private static readonly string[] SharedAssemblyPrefixes =
        ["Microsoft.Agents", "Microsoft.AspNetCore", "Microsoft.Extensions"];

    internal static void LoadAndRegister(WebApplicationBuilder builder, string agentAssemblyPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentAssemblyPath);

        if (!File.Exists(agentAssemblyPath))
        {
            throw new FileNotFoundException($"Agent assembly not found: {agentAssemblyPath}", agentAssemblyPath);
        }

        var loadContext = new AgentAssemblyLoadContext(Path.GetFullPath(agentAssemblyPath));
        var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(agentAssemblyPath));
        var programType = assembly.GetType("Program")
            ?? throw new InvalidOperationException(
                $"Assembly '{agentAssemblyPath}' does not define a global-namespace 'Program' type.");
        var registerServices = programType.GetMethod(
            "RegisterServices", BindingFlags.Static | BindingFlags.Public, null,
            [typeof(WebApplicationBuilder)], null);
        if (registerServices is null || registerServices.ReturnType != typeof(void) ||
            registerServices.ContainsGenericParameters)
        {
            throw new InvalidOperationException(
                $"Type 'Program' in assembly '{agentAssemblyPath}' must define public static void RegisterServices(WebApplicationBuilder).");
        }

        registerServices.Invoke(null, [builder]);
    }

    private sealed class AgentAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            foreach (var prefix in SharedAssemblyPrefixes)
            {
                if (assemblyName.Name == prefix ||
                    assemblyName.Name?.StartsWith(prefix + ".", StringComparison.Ordinal) == true)
                {
                    try
                    {
                        return Default.LoadFromAssemblyName(assemblyName);
                    }
                    catch (FileNotFoundException)
                    {
                        // Customer bundles may carry SDK extensions absent from the runner.
                        break;
                    }
                }
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
