// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Holds the default type and any registered resolvers for a given activity type discriminator.
    /// </summary>
    public sealed class ActivityTypeEntry
    {
        private readonly List<(IActivityTypeResolver Resolver, Type TargetType)> _resolvers = [];
        private readonly object _lock = new();

        /// <summary>
        /// The default concrete type to deserialize to when no resolver matches.
        /// </summary>
        public Type BaseType { get; internal set; }

        internal void AddResolver(IActivityTypeResolver resolver, Type targetType)
        {
            lock (_lock)
            {
                // Maintain descending priority order so higher-priority resolvers are checked first.
                var index = _resolvers.FindIndex(r => r.Resolver.Priority < resolver.Priority);
                _resolvers.Insert(index < 0 ? _resolvers.Count : index, (resolver, targetType));
            }
        }

        /// <summary>
        /// Returns a snapshot of registered resolvers in descending priority order.
        /// </summary>
        public (IActivityTypeResolver Resolver, Type TargetType)[] GetResolvers()
        {
            lock (_lock)
            {
                return _resolvers.ToArray();
            }
        }
    }
}
