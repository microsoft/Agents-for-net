// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Builder.Adapters
{
    /// <summary>
    /// Assembly-level attribute that points at an <see cref="IChannelAdapter"/> implementation annotated
    /// with a <see cref="ChannelAdapterAttribute"/>, together with the <c>channelId</c> it handles.
    /// </summary>
    /// <remarks>
    /// One instance is emitted by the <c>ChannelAdapterInitSourceGenerator</c> for each
    /// <c>[ChannelAdapter]</c> declaration in an assembly. At <see cref="IChannelAdapterRegistry"/>
    /// construction these attributes are read off the loaded assemblies and their adapters registered, so
    /// adapter authors do not need to call an explicit DI registration method and the runtime never has to
    /// scan every type to find channel adapters. Follows the same discovery pattern as
    /// <c>Microsoft.Agents.Core.Serialization.ActivityTypeInitAssemblyAttribute</c>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ChannelAdapterInitAssemblyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelAdapterInitAssemblyAttribute"/> class.
        /// </summary>
        /// <param name="adapterType">The annotated adapter type. Must implement <see cref="IChannelAdapter"/>.</param>
        /// <param name="channelId">The <c>channelId</c> the adapter handles.</param>
        public ChannelAdapterInitAssemblyAttribute(Type adapterType, string channelId)
        {
            AdapterType = adapterType;
            ChannelId = channelId;
        }

        /// <summary>The annotated adapter type.</summary>
        public Type AdapterType { get; }

        /// <summary>The <c>channelId</c> the adapter handles.</summary>
        public string ChannelId { get; }

        /// <summary>
        /// Reads every <see cref="ChannelAdapterInitAssemblyAttribute"/> off the currently loaded assemblies
        /// and projects them to <see cref="ChannelAdapterRegistration"/> descriptors. Malformed
        /// registrations (missing type or channelId, or an adapter that does not implement
        /// <see cref="IChannelAdapter"/>) are ignored so one bad declaration cannot break discovery for
        /// everything else.
        /// </summary>
        internal static IReadOnlyList<ChannelAdapterRegistration> GetRegistrations()
        {
            var registrations = new List<ChannelAdapterRegistration>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                IEnumerable<ChannelAdapterInitAssemblyAttribute> attributes;
                try
                {
                    attributes = assembly
                        .GetCustomAttributes(typeof(ChannelAdapterInitAssemblyAttribute), false)
                        .OfType<ChannelAdapterInitAssemblyAttribute>();
                }
                catch (Exception)
                {
                    // A single unloadable/reflection-only assembly must not break discovery.
                    continue;
                }

                foreach (var attribute in attributes)
                {
                    if (attribute.AdapterType == null
                        || string.IsNullOrEmpty(attribute.ChannelId)
                        || !typeof(IChannelAdapter).IsAssignableFrom(attribute.AdapterType))
                    {
                        continue;
                    }

                    registrations.Add(new ChannelAdapterRegistration(attribute.ChannelId, attribute.AdapterType));
                }
            }

            return registrations;
        }
    }
}
