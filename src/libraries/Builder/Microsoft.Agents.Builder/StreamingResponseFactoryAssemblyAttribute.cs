// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Assembly-level attribute emitted by the source generator. Maps a channelId to an
    /// <see cref="IStreamingResponseFactory"/> implementation type for runtime discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StreamingResponseFactoryAssemblyAttribute : Attribute
    {
        public StreamingResponseFactoryAssemblyAttribute(Type factoryType, string channelId)
        {
            FactoryType = factoryType;
            ChannelId = channelId;
        }

        /// <summary>The factory implementation type.</summary>
        public Type FactoryType { get; }

        /// <summary>The channel ID this factory handles.</summary>
        public string ChannelId { get; }

        /// <summary>
        /// Scans all loaded assemblies to discover factory registrations.
        /// </summary>
        internal static IReadOnlyList<(string ChannelId, Type FactoryType)> DiscoverFactories()
        {
            var results = new List<(string, Type)>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                ScanAssembly(assembly, results);
            }
            return results;
        }

        internal static void ScanAssembly(Assembly assembly, List<(string, Type)> results)
        {
            try
            {
                var attrs = assembly
                    .GetCustomAttributes(typeof(StreamingResponseFactoryAssemblyAttribute), false)
                    .OfType<StreamingResponseFactoryAssemblyAttribute>();

                foreach (var attr in attrs)
                {
                    results.Add((attr.ChannelId, attr.FactoryType));
                }
            }
            catch
            {
                // Ignore assemblies that can't be reflected (dynamic, etc.)
            }
        }
    }
}
