// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Declares the activity type string for an <see cref="Microsoft.Agents.Core.Models.Activity"/> subclass,
    /// enabling automatic registration with <see cref="ProtocolJsonSerializer"/> via
    /// <see cref="ActivityInitAssemblyAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For simple type-string matching, omit <see cref="Resolver"/>:
    /// <code>
    /// [assembly: ActivityInitAssembly(typeof(MyActivity))]
    ///
    /// [ActivityType("myType")]
    /// public class MyActivity : Activity { }
    /// </code>
    /// </para>
    /// <para>
    /// For matching on additional fields (e.g. channelId), supply a <see cref="Resolver"/>:
    /// <code>
    /// [assembly: ActivityInitAssembly(typeof(MyChannelActivity))]
    ///
    /// [ActivityType(ActivityTypes.Message, Resolver = typeof(MyChannelResolver))]
    /// public class MyChannelActivity : MessageActivity { }
    /// </code>
    /// Resolvers are evaluated before the default type mapping for the same activity type string.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ActivityTypeAttribute(string activityType) : Attribute
    {
        /// <summary>
        /// The activity type string (e.g. <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Message"/>)
        /// that this class represents.
        /// </summary>
        public string ActivityType { get; } = activityType;

        /// <summary>
        /// Optional. A type implementing <see cref="IActivityTypeResolver"/> that provides additional
        /// matching logic beyond the activity type string. The type must have a parameterless constructor.
        /// When set, this class is registered as a resolver candidate rather than the default for the type string.
        /// </summary>
        public Type Resolver { get; set; }
    }
}
