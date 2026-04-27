// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines constant string values for Activity types.
    /// </summary>
    /// <remarks>
    /// Use <see cref="ActivityType"/> instead, which provides both typed values and
    /// compile-time constants via <see cref="ActivityType.Names"/>.
    /// </remarks>
    [Obsolete("Use ActivityType or ActivityType.Names instead.")]
    public static class ActivityTypes
    {
        /// <summary>The type value for contact relation update activities.</summary>
        public const string ContactRelationUpdate = ActivityType.Names.ContactRelationUpdate;

        /// <summary>The type value for conversation update activities.</summary>
        public const string ConversationUpdate = ActivityType.Names.ConversationUpdate;

        /// <summary>The type value for end of conversation activities.</summary>
        public const string EndOfConversation = ActivityType.Names.EndOfConversation;

        /// <summary>The type value for event activities.</summary>
        public const string Event = ActivityType.Names.Event;

        /// <summary>The type value for delete user data activities.</summary>
        public const string DeleteUserData = ActivityType.Names.DeleteUserData;

        /// <summary>The type value for handoff activities.</summary>
        public const string Handoff = ActivityType.Names.Handoff;

        /// <summary>The type value for installation update activities.</summary>
        public const string InstallationUpdate = ActivityType.Names.InstallationUpdate;

        /// <summary>The type value for invoke activities.</summary>
        public const string Invoke = ActivityType.Names.Invoke;

        /// <summary>The type value for message activities.</summary>
        public const string Message = ActivityType.Names.Message;

        /// <summary>The type value for message delete activities.</summary>
        public const string MessageDelete = ActivityType.Names.MessageDelete;

        /// <summary>The type value for message reaction activities.</summary>
        public const string MessageReaction = ActivityType.Names.MessageReaction;

        /// <summary>The type value for message update activities.</summary>
        public const string MessageUpdate = ActivityType.Names.MessageUpdate;

        /// <summary>The type value for suggestion activities.</summary>
        public const string Suggestion = ActivityType.Names.Suggestion;

        /// <summary>The type value for trace activities.</summary>
        public const string Trace = ActivityType.Names.Trace;

        /// <summary>The type value for typing activities.</summary>
        public const string Typing = ActivityType.Names.Typing;

        /// <summary>The type value for command activities.</summary>
        public const string Command = ActivityType.Names.Command;

        /// <summary>The type value for command result activities.</summary>
        public const string CommandResult = ActivityType.Names.CommandResult;

        /// <summary>The type value for invoke response activities.</summary>
        public const string InvokeResponse = ActivityType.Names.InvokeResponse;
    }
}
