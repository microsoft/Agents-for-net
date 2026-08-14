// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Core
{
    /// <summary>
    /// Loads referenced SDK extension assemblies before feature-specific discovery runs.
    /// </summary>
    public static class AgentSdkInitializer
    {
        private static readonly ConcurrentDictionary<Type, byte> _initializedTypes = new();
        private static readonly Lazy<bool> _initializer = new(Initialize);

        /// <summary>
        /// Ensures SDK assembly initialization has run for all currently loaded assemblies and will run
        /// for assemblies loaded later.
        /// </summary>
        public static void EnsureInitialized()
        {
            _ = _initializer.Value;
        }

        private static bool Initialize()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => InitializeAssembly(args.LoadedAssembly);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                InitializeAssembly(assembly);
            }

            return true;
        }

        private static void InitializeAssembly(Assembly assembly)
        {
            foreach (var attribute in assembly
                .GetCustomAttributes(typeof(AgentSdkInitAssemblyAttribute), false)
                .OfType<AgentSdkInitAssemblyAttribute>())
            {
                var type = attribute.InitType;
                if (type == null || !_initializedTypes.TryAdd(type, 0))
                {
                    continue;
                }

                var init = type.GetMethod("Init", BindingFlags.Static | BindingFlags.Public);
                init?.Invoke(null, null);
            }
        }
    }
}
