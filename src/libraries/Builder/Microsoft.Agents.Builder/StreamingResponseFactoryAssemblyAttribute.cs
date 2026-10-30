// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Assembly-level attribute that registers an <see cref="IStreamingResponseFactory"/> type for automatic,
    /// per-channel discovery.  One instance is emitted per <see cref="StreamingResponseFactoryAttribute"/>-decorated
    /// type by the streaming-response-factory source generator.
    /// </summary>
    /// <param name="type">The <see cref="IStreamingResponseFactory"/> implementation type to register.</param>
    /// <remarks>
    /// This mirrors <c>SerializationInitAssemblyAttribute</c>/<c>EntityInitAssemblyAttribute</c>:
    /// <see cref="InitStreamingResponseFactories"/> scans the loaded assemblies (and subscribes to
    /// <see cref="AppDomain.AssemblyLoad"/> so late-loaded extension packages are also picked up), reads the
    /// <see cref="StreamingResponseFactoryAttribute"/> from each registered type to obtain the channel id, and
    /// populates <see cref="StreamingResponseFactoryCatalog"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StreamingResponseFactoryAssemblyAttribute(Type type) : Attribute
    {
        /// <summary>The registered <see cref="IStreamingResponseFactory"/> implementation type.</summary>
        public readonly Type FactoryType = type;

        internal static void InitStreamingResponseFactories()
        {
            // Register handler for new assembly loads.  This is needed because C# does not load a package
            // until a type in it is accessed - an extension may load after this initial scan.
            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);

            // Register factories from currently loaded assemblies.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                InitAssembly(assembly);
            }
        }

        private static void InitAssembly(Assembly assembly)
        {
            foreach (var factoryType in GetRegisteredFactoryTypes(assembly))
            {
                try
                {
                    var factoryAttribute = factoryType.GetCustomAttribute<StreamingResponseFactoryAttribute>(false);
                    if (factoryAttribute != null)
                    {
                        StreamingResponseFactoryCatalog.Register(factoryAttribute.ChannelId, factoryType);
                    }
                }
                catch (Exception)
                {
                    // Ignore errors (e.g. reflection-only or unloadable types); a missing factory just falls
                    // back to the default StreamingResponse.
                }
            }
        }

        private static IEnumerable<Type> GetRegisteredFactoryTypes(Assembly assembly)
        {
            IEnumerable<StreamingResponseFactoryAssemblyAttribute> attributes;
            try
            {
                attributes = assembly
                    .GetCustomAttributes(typeof(StreamingResponseFactoryAssemblyAttribute), false)?
                    .OfType<StreamingResponseFactoryAssemblyAttribute>();
            }
            catch (Exception)
            {
                attributes = null;
            }

            return attributes?.Select(x => x.FactoryType) ?? Array.Empty<Type>();
        }
    }
}
