// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Represents an activity type value. Provides IntelliSense for well-known values via
    /// static properties and compile-time constants via <see cref="Names"/>, while still
    /// accepting any custom string value. Equality comparisons are case-insensitive.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ActivityTypeJsonConverter))]
    public class ActivityType : IEquatable<ActivityType>
    {
        private readonly string? _value;

        /// <summary>Initializes a new <see cref="ActivityType"/> with the specified string value.</summary>
        public ActivityType(string? value) => _value = value;

        /// <summary>Implicitly converts a <see langword="string"/> to an <see cref="ActivityType"/>.</summary>
        public static implicit operator ActivityType(string? s) => new(s);

        /// <summary>Implicitly converts an <see cref="ActivityType"/> to a <see langword="string"/>.</summary>
        public static implicit operator string?(ActivityType? t) => t?._value;

        /// <inheritdoc/>
        public bool Equals(ActivityType? other) =>
            string.Equals(_value, other?._value, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as ActivityType);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            _value == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(_value);

        /// <summary>Returns <see langword="true"/> if two <see cref="ActivityType"/> values are equal (case-insensitive).</summary>
        public static bool operator ==(ActivityType? a, ActivityType? b) =>
            string.Equals(a?._value, b?._value, StringComparison.OrdinalIgnoreCase);

        /// <summary>Returns <see langword="true"/> if two <see cref="ActivityType"/> values are not equal (case-insensitive).</summary>
        public static bool operator !=(ActivityType? a, ActivityType? b) =>
            !string.Equals(a?._value, b?._value, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override string ToString() => _value ?? string.Empty;

        /// <summary>
        /// Constant string values for each well-known activity type.
        /// Use these in <c>switch</c> case labels and attribute arguments where a compile-time constant is required.
        /// </summary>
        public static class Names
        {
            /// <summary>The type value for contact relation update activities.</summary>
            public const string ContactRelationUpdate = "contactRelationUpdate";

            /// <summary>The type value for conversation update activities.</summary>
            public const string ConversationUpdate = "conversationUpdate";

            /// <summary>The type value for end of conversation activities.</summary>
            public const string EndOfConversation = "endOfConversation";

            /// <summary>The type value for event activities.</summary>
            public const string Event = "event";

            /// <summary>The type value for delete user data activities.</summary>
            public const string DeleteUserData = "deleteUserData";

            /// <summary>The type value for handoff activities.</summary>
            public const string Handoff = "handoff";

            /// <summary>The type value for installation update activities.</summary>
            public const string InstallationUpdate = "installationUpdate";

            /// <summary>The type value for invoke activities.</summary>
            public const string Invoke = "invoke";

            /// <summary>The type value for message activities.</summary>
            public const string Message = "message";

            /// <summary>The type value for message delete activities.</summary>
            public const string MessageDelete = "messageDelete";

            /// <summary>The type value for message reaction activities.</summary>
            public const string MessageReaction = "messageReaction";

            /// <summary>The type value for message update activities.</summary>
            public const string MessageUpdate = "messageUpdate";

            /// <summary>The type value for suggestion activities.</summary>
            public const string Suggestion = "suggestion";

            /// <summary>The type value for trace activities.</summary>
            public const string Trace = "trace";

            /// <summary>The type value for typing activities.</summary>
            public const string Typing = "typing";

            /// <summary>The type value for command activities.</summary>
            public const string Command = "command";

            /// <summary>The type value for command result activities.</summary>
            public const string CommandResult = "commandResult";

            /// <summary>The type value for invoke response activities.</summary>
            public const string InvokeResponse = "invokeResponse";
        }

        /// <summary>Activity type for contact relation update activities.</summary>
        public static readonly ActivityType ContactRelationUpdate = new(Names.ContactRelationUpdate);

        /// <summary>Activity type for conversation update activities.</summary>
        public static readonly ActivityType ConversationUpdate = new(Names.ConversationUpdate);

        /// <summary>Activity type for end of conversation activities.</summary>
        public static readonly ActivityType EndOfConversation = new(Names.EndOfConversation);

        /// <summary>Activity type for event activities.</summary>
        public static readonly ActivityType Event = new(Names.Event);

        /// <summary>Activity type for delete user data activities.</summary>
        public static readonly ActivityType DeleteUserData = new(Names.DeleteUserData);

        /// <summary>Activity type for handoff activities.</summary>
        public static readonly ActivityType Handoff = new(Names.Handoff);

        /// <summary>Activity type for installation update activities.</summary>
        public static readonly ActivityType InstallationUpdate = new(Names.InstallationUpdate);

        /// <summary>Activity type for invoke activities.</summary>
        public static readonly ActivityType Invoke = new(Names.Invoke);

        /// <summary>Activity type for message activities.</summary>
        public static readonly ActivityType Message = new(Names.Message);

        /// <summary>Activity type for message delete activities.</summary>
        public static readonly ActivityType MessageDelete = new(Names.MessageDelete);

        /// <summary>Activity type for message reaction activities.</summary>
        public static readonly ActivityType MessageReaction = new(Names.MessageReaction);

        /// <summary>Activity type for message update activities.</summary>
        public static readonly ActivityType MessageUpdate = new(Names.MessageUpdate);

        /// <summary>Activity type for suggestion activities.</summary>
        public static readonly ActivityType Suggestion = new(Names.Suggestion);

        /// <summary>Activity type for trace activities.</summary>
        public static readonly ActivityType Trace = new(Names.Trace);

        /// <summary>Activity type for typing activities.</summary>
        public static readonly ActivityType Typing = new(Names.Typing);

        /// <summary>Activity type for command activities.</summary>
        public static readonly ActivityType Command = new(Names.Command);

        /// <summary>Activity type for command result activities.</summary>
        public static readonly ActivityType CommandResult = new(Names.CommandResult);

        /// <summary>Activity type for invoke response activities.</summary>
        public static readonly ActivityType InvokeResponse = new(Names.InvokeResponse);

        internal sealed class ActivityTypeJsonConverter
            : System.Text.Json.Serialization.JsonConverter<ActivityType>
        {
            public override ActivityType? Read(
                ref System.Text.Json.Utf8JsonReader reader,
                System.Type typeToConvert,
                System.Text.Json.JsonSerializerOptions options)
                => reader.TokenType == System.Text.Json.JsonTokenType.Null ? null : new(reader.GetString());

            public override void Write(
                System.Text.Json.Utf8JsonWriter writer,
                ActivityType value,
                System.Text.Json.JsonSerializerOptions options)
                => writer.WriteStringValue(value._value);
        }
    }
}
