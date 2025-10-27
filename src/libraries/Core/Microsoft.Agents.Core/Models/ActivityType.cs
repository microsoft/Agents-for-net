// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// <list type="bullet">
    ///   <item>
    ///     <term>ContactRelationUpdate</term>
    ///     <description>Contact relation update activities</description>
    ///   </item>
    ///   <item>
    ///     <term>ConversationUpdate</term>
    ///     <description>A conversation update occurred</description>
    ///   </item>
    ///   <item>
    ///     <term>EndOfConversation</term>
    ///     <description>Notify/Receive end of conversation</description>
    ///   </item>
    ///   <item>
    ///     <term>Event</term>
    ///     <description>Notify/Receive an event Activity.</description>
    ///   </item>
    ///   <item>
    ///     <term>DeleteUserData</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>Handoff</term>
    ///     <description><seealso cref="Handoff"/></description>
    ///   </item>
    ///   <item>
    ///     <term>InstallationUpdate</term>
    ///     <description>Indicates the agents was added to a conversation in Teams</description>
    ///   </item>
    ///   <item>
    ///     <term>Invoke</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>Message</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>MessageDelete</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>MessageReaction</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>MessageUpdate</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>Suggestion</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>Trace</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>Typing</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>Command</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    ///   <item>
    ///     <term>CommandResult</term>
    ///     <description>The type value for contact relation update activities.</description>
    ///   </item>
    /// </list>
    /// </summary>
    [JsonConverter(typeof(ActivityTypeConverter))]
    public class ActivityType : IEquatable<ActivityType>
    {
        private readonly string _type;

        /// <summary>
        /// Contact relation update occurred.
        /// </summary>
        public const string ContactRelationUpdate = "contactRelationUpdate";

        /// <summary>
        /// A conversation update occurred.
        /// </summary>
        public const string ConversationUpdate = "conversationUpdate";

        /// <summary>
        /// Notify/Receive end of conversation.
        /// </summary>
        /// <remarks>
        /// Received: Channel side is ending conversation.
        /// Send: Agent side is ending conversation.
        /// </remarks>
        public const string EndOfConversation = "endOfConversation";

        /// <summary>
        /// Notify/Receive an event Activity.
        /// </summary>
        /// <remarks>
        /// The IActivity.Name contains the unique event name.
        /// </remarks>
        public const string Event = "event";

        /// <summary>
        /// The type value for delete user data activities.
        /// </summary>
        public const string DeleteUserData = "deleteUserData";

        /// <summary>
        /// The type value for handoff activities.
        /// </summary>
        public const string Handoff = "handoff";

        /// <summary>
        /// The agent receives an installationUpdate event when you install an agent to a conversation thread in Teams. 
        /// </summary>
        public const string InstallationUpdate = "installationUpdate";

        /// <summary>
        /// The type value for invoke activities.
        /// </summary>
        /// <remarks>
        /// The IActivity.Name contains the unique Invoke name.
        /// </remarks>
        public const string Invoke = "invoke";

        /// <summary>
        /// The type value for message activities.
        /// </summary>
        public const string Message = "message";

        /// <summary>
        /// The type value for message delete activities.
        /// </summary>
        public const string MessageDelete = "messageDelete";

        /// <summary>
        /// The type value for message reaction activities.
        /// </summary>
        public const string MessageReaction = "messageReaction";

        /// <summary>
        /// The type value for message update activities.
        /// </summary>
        public const string MessageUpdate = "messageUpdate";

        /// <summary>
        /// The type value for suggestion activities.
        /// </summary>
        public const string Suggestion = "suggestion";

        /// <summary>
        /// The type value for trace activities.
        /// </summary>
        public const string Trace = "trace";

        /// <summary>
        /// The type value for typing activities.
        /// </summary>
        public const string Typing = "typing";

        /// <summary>
        /// The type value for command activities.
        /// </summary>
        public const string Command = "command";

        /// <summary>
        /// The type value for command result activities.
        /// </summary>
        public const string CommandResult = "commandResult";

        /// <summary>
        /// The type value for invoke response activities.
        /// </summary>
        /// <remarks>This is used for a return payload in response to an invoke activity.
        /// Invoke activities communicate programmatic information from a client or channel to an Agent, and
        /// have a corresponding return payload for use within the channel. The meaning of an invoke activity
        /// is defined by the <see cref="Activity.Name"/> field, which is meaningful within the scope of a channel.
        /// </remarks>
        public const string InvokeResponse = "invokeResponse";

        [JsonConstructor]
        public ActivityType(string type)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(nameof(type), "Activity type cannot be null or whitespace");
            _type = FromString(type);
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

        private static string FromString(string type)
        {
            var propertyInfo = typeof(ActivityType).GetField(type, BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (propertyInfo != null)
            {
                return propertyInfo.GetValue(null).ToString();
            }
            
            return type;
        }

        internal sealed class ActivityTypeConverter : JsonConverter<ActivityType>
        {
            public override ActivityType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("Expected a string for ActivityType.");
                }

                var activityType = reader.GetString();
                if (string.IsNullOrWhiteSpace(activityType))
                {
                    return null;
                }

                return new ActivityType(activityType!);
            }

            public override void Write(Utf8JsonWriter writer, ActivityType value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value._type);
            }
        }
    }
}