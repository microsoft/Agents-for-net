// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// A single declarative custom-Activity registration: the CLR subclass plus the discriminators
    /// (type / channelId / name) an inbound Activity must match. Built from <see cref="ActivityTypeAttribute"/>.
    /// </summary>
    internal sealed class ActivityTypeRegistration
    {
        public ActivityTypeRegistration(Type clrType, string type, string channelId, string name)
        {
            ClrType = clrType;
            Type = string.IsNullOrWhiteSpace(type) ? null : type;
            ChannelId = string.IsNullOrWhiteSpace(channelId) ? null : channelId;
            Name = string.IsNullOrWhiteSpace(name) ? null : name;
        }

        public Type ClrType { get; }

        public string Type { get; }

        public string ChannelId { get; }

        public string Name { get; }

        /// <summary>The number of set discriminators — higher wins when multiple registrations match.</summary>
        public int Specificity =>
            (Type != null ? 1 : 0) + (ChannelId != null ? 1 : 0) + (Name != null ? 1 : 0);

        public bool Matches(in ActivityResolutionContext context)
        {
            return (Type == null || string.Equals(Type, context.Type, StringComparison.OrdinalIgnoreCase))
                && (ChannelId == null || string.Equals(ChannelId, context.ChannelId, StringComparison.OrdinalIgnoreCase))
                && (Name == null || string.Equals(Name, context.Name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
