// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Declares that a custom <see cref="Microsoft.Agents.Core.Models.Activity"/> subclass should be
    /// deserialized when an inbound Activity matches the specified discriminators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At least one discriminator (<see cref="Type"/>, <see cref="ChannelId"/>, or <see cref="Name"/>)
    /// must be set. Discriminators are combined with AND: an Activity matches only when every set
    /// discriminator equals the corresponding field on the wire (case-insensitive). Unset
    /// discriminators are wildcards.
    /// </para>
    /// <para>
    /// A single subclass may be annotated multiple times to match more than one shape. When several
    /// registrations match, the most specific one (the greatest number of set discriminators) wins.
    /// Annotated subclasses are auto-registered at assembly load time (a source generator emits an
    /// <see cref="ActivityTypeInitAssemblyAttribute"/> per annotated class), so no manual registration
    /// call is needed. For matching logic that these declarative discriminators cannot express, register
    /// an imperative <see cref="ActivityTypeResolver"/> via
    /// <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.RegisterActivityTypeResolver"/>.
    /// </para>
    /// <example>
    /// <code>
    /// // Any activity on the msteams channel deserializes to TeamsActivity:
    /// [ActivityType(ChannelId = "msteams")]
    /// public class TeamsActivity : Activity { }
    ///
    /// // Only "message" activities on msteams deserialize to TeamsMessageActivity:
    /// [ActivityType("message", ChannelId = "msteams")]
    /// public class TeamsMessageActivity : Activity { }
    ///
    /// // A brand-new protocol type:
    /// [ActivityType("x-workflowTrigger")]
    /// public class WorkflowTriggerActivity : Activity { }
    /// </code>
    /// </example>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ActivityTypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityTypeAttribute"/> class.
        /// Set at least one of <see cref="Type"/>, <see cref="ChannelId"/>, or <see cref="Name"/>.
        /// </summary>
        public ActivityTypeAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityTypeAttribute"/> class that matches
        /// the given activity <c>type</c> string.
        /// </summary>
        /// <param name="type">The activity type string (e.g., "message", "event", "x-myCustomType").</param>
        public ActivityTypeAttribute(string type)
        {
            Type = type;
        }

        /// <summary>
        /// The activity <c>type</c> string to match (e.g., "message", "event", "x-myCustomType").
        /// When <see langword="null"/>, the type is not considered (matches any type).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The Activity <c>channelId</c> to match (e.g., "msteams"). When <see langword="null"/>,
        /// the channel is not considered.
        /// </summary>
        public string ChannelId { get; set; }

        /// <summary>
        /// The Activity <c>name</c> to match (e.g., an invoke name like "task/fetch"). When
        /// <see langword="null"/>, the name is not considered.
        /// </summary>
        public string Name { get; set; }
    }
}
