// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// 
    /// </summary>
    [JsonConverter(typeof(ActivityTypeConverter))]
    public class ActivityType : IEquatable<ActivityType>
    {
        private readonly string _type;

        private static readonly ActivityType _contactRelationUpdate = new("contactRelationUpdate");
        private static readonly ActivityType _conversationUpdate = new("conversationUpdate");
        private static readonly ActivityType _endOfConversation = new("endOfConversation");
        private static readonly ActivityType _event = new("event");
        private static readonly ActivityType _deleteUserData = new("deleteUserData");
        private static readonly ActivityType _handoff = new("handoff");
        private static readonly ActivityType _installationUpdate = new("installationUpdate");
        private static readonly ActivityType _invoke = new("invoke");
        private static readonly ActivityType _message = new("message");
        private static readonly ActivityType _messageDelete = new("messageDelete");
        private static readonly ActivityType _messageReaction = new("messageReaction");
        private static readonly ActivityType _messageUpdate = new("messageUpdate");
        private static readonly ActivityType _suggestion = new("suggestion");
        private static readonly ActivityType _trace = new("trace");
        private static readonly ActivityType _typing = new("typing");
        private static readonly ActivityType _command = new("command");
        private static readonly ActivityType _commandResult = new("commandResult");
        private static readonly ActivityType _invokeResponse = new("invokeResponse");

        /// <summary>
        /// The type value for contact relation update activities.
        /// </summary>
        public static ActivityType ContactRelationUpdate => _contactRelationUpdate;

        /// <summary>
        /// The type value for conversation update activities.
        /// </summary>
        public static ActivityType ConversationUpdate => _conversationUpdate;

        /// <summary>
        /// The type value for end of conversation activities.
        /// </summary>
        public static ActivityType EndOfConversation => _endOfConversation;

        /// <summary>
        /// The type value for event activities.
        /// </summary>
        public static ActivityType Event => _event;

        /// <summary>
        /// The type value for delete user data activities.
        /// </summary>
        public static ActivityType DeleteUserData => _deleteUserData;

        /// <summary>
        /// The type value for handoff activities.
        /// </summary>
        public static ActivityType Handoff => _handoff;

        /// <summary>
        /// The type value for installation update activities.
        /// </summary>
        public static ActivityType InstallationUpdate => _installationUpdate;

        /// <summary>
        /// The type value for invoke activities.
        /// </summary>
        public static ActivityType Invoke => _invoke;

        /// <summary>
        /// The type value for message activities.
        /// </summary>
        public static ActivityType Message => _message;

        /// <summary>
        /// The type value for message delete activities.
        /// </summary>
        public static ActivityType MessageDelete => _messageDelete;

        /// <summary>
        /// The type value for message reaction activities.
        /// </summary>
        public static ActivityType MessageReaction => _messageReaction;

        /// <summary>
        /// The type value for message update activities.
        /// </summary>
        public static ActivityType MessageUpdate => _messageUpdate;

        /// <summary>
        /// The type value for suggestion activities.
        /// </summary>
        public static ActivityType Suggestion => _suggestion;

        /// <summary>
        /// The type value for trace activities.
        /// </summary>
        public static ActivityType Trace => _trace;

        /// <summary>
        /// The type value for typing activities.
        /// </summary>
        public static ActivityType Typing => _typing;

        /// <summary>
        /// The type value for command activities.
        /// </summary>
        public static ActivityType Command => _command;

        /// <summary>
        /// The type value for command result activities.
        /// </summary>
        public static ActivityType CommandResult => _commandResult;

        /// <summary>
        /// The type value for invoke response activities.
        /// </summary>
        /// <remarks>This is used for a return payload in response to an invoke activity.
        /// Invoke activities communicate programmatic information from a client or channel to an Agent, and
        /// have a corresponding return payload for use within the channel. The meaning of an invoke activity
        /// is defined by the <see cref="Activity.Name"/> field, which is meaningful within the scope of a channel.
        /// </remarks>
        public static ActivityType InvokeResponse => _invokeResponse;

        [JsonConstructor]
        public ActivityType(string type)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(nameof(type), "Activity type cannot be null or whitespace");
            _type = type;
        }

        public static bool operator ==(ActivityType left, ActivityType right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        public static bool operator !=(ActivityType left, ActivityType right)
            => !(left == right);

        public bool Equals(ActivityType other)
        {
            if (other is null)
            {
                return false;
            }

            if (object.ReferenceEquals(_type, other._type))
            {
                // Strings are interned, so there is a good chance that two equal methods use the same reference
                // (unless they differ in case).
                return true;
            }

            return string.Compare(_type, other._type, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ActivityType);
        }

        public override int GetHashCode()
        {
            return _type.ToUpperInvariant().GetHashCode();
        }

        public override string ToString()
        {
            return _type.ToString();
        }

        public static implicit operator ActivityType(string value)
        {
            return new ActivityType(value);
        }

        public static implicit operator string(ActivityType type)
        {
            return type?.ToString();
        }

        public static ActivityType FromString(string type)
        {
            return (ActivityType)typeof(ActivityType).GetProperty(type, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(null) ?? new ActivityType(type);
        }

        internal sealed class ActivityTypeConverter : JsonConverter<ActivityType>
        {
            public override ActivityType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("Expected a string for ActivityType.");
                }

                var protocol = reader.GetString();
                if (string.IsNullOrWhiteSpace(protocol))
                {
                    return null;
                }

                return FromString(protocol!);
            }

            public override void Write(Utf8JsonWriter writer, ActivityType value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value._type);
            }
        }
    }
}