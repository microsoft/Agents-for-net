// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Provides custom matching logic for deserializing an incoming Activity to a specific subclass,
    /// beyond matching on the <c>type</c> discriminator alone.
    /// </summary>
    /// <remarks>
    /// Implement this interface and reference it via <see cref="ActivityTypeAttribute.Resolver"/>
    /// to resolve a subclass based on additional JSON fields (e.g. channelId, name).
    /// The resolver is only invoked when the activity's <c>type</c> field already matches the
    /// <see cref="ActivityTypeAttribute.ActivityType"/> declared on the decorated class.
    /// </remarks>
    public interface IActivityTypeResolver
    {
        /// <summary>
        /// Returns <see langword="true"/> if the activity JSON should be deserialized to the
        /// associated subclass; <see langword="false"/> to defer to the next resolver or the default mapping.
        /// </summary>
        bool Matches(JsonElement activityJson);

        /// <summary>
        /// Resolvers with higher priority are evaluated before those with lower priority
        /// within the same activity type bucket.
        /// </summary>
        int Priority { get; }
    }
}
