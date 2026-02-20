// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides a builder for configuring and creating a new conversation with specified conversation parameters.
    /// </summary>
    public class CreateConversationBuilder
    {
        private readonly CreateConversationRecord _record = new();

        public static CreateConversationBuilder Create(string agentClientId, ChannelId channelId, string tenantId = null, string serviceUrl = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentClientId, nameof(agentClientId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));

            var builder = new CreateConversationBuilder();

            builder._record.ReferenceRecord = RecordBuilder.Create()
                .WithReference(ReferenceBuilder.Create(agentClientId, channelId, serviceUrl).Build())
                .WithClaimsForClientId(agentClientId)
                .Build();

            builder._record.Parameters = new ConversationParameters
            {
                TenantId = tenantId,
                Agent = new ChannelAccount(agentClientId, role: RoleTypes.Agent)
            };

            return builder;
        }

        /// <summary>
        /// Specifies the user to include as a participant in the conversation being built.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to add to the conversation. Cannot be null.</param>
        /// <param name="userName">The display name of the user to add to the conversation. This value is optional and can be null.</param>
        /// <returns>The current <see cref="CreateConversationBuilder"/> instance with the specified user set as a participant.</returns>
        public CreateConversationBuilder WithUser(string userId, string userName = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
            _record.Parameters.Members =
            [
                new ChannelAccount(userId, userName)
            ];
            return this;
        }

        /// <summary>
        /// Specifies the user to include as a member in the conversation being created.
        /// </summary>
        /// <param name="user">The user account to add as a member of the conversation. Cannot be null.</param>
        /// <returns>The current <see cref="CreateConversationBuilder"/> instance for method chaining.</returns>
        public CreateConversationBuilder WithUser(ChannelAccount user)
        {
            AssertionHelpers.ThrowIfNull(user, nameof(user));
            _record.Parameters.Members =
            [
                user
            ];
            return this;
        }

        /// <summary>
        /// Specifies the users to include as a member in the conversation being created.
        /// </summary>
        /// <param name="users">The user accounts to add as a member of the conversation. Cannot be null or empty.</param>
        /// <returns>The current <see cref="CreateConversationBuilder"/> instance for method chaining.</returns>
        public CreateConversationBuilder WithUsers(ChannelAccount[] users)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(users, nameof(users));
            _record.Parameters.Members = users;
            return this;
        }

        /// <summary>
        /// Sets the scope for the conversation being created.
        /// </summary>
        /// <remarks>Use this method to specify a custom scope for the conversation. This does not normally need to be set for Azure Bot Channels.</remarks>
        /// <param name="scope">The scope value to associate with the conversation. Cannot be null or empty.</param>
        /// <returns>The current <see cref="CreateConversationBuilder"/> instance with the updated scope.</returns>
        public CreateConversationBuilder WithScope(string scope)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(scope, nameof(scope));
            _record.Scope = scope;
            return this;
        }

        /// <summary>
        /// Adds a message to the conversation being constructed.
        /// </summary>
        /// <param name="message">The activity representing the message to include in the conversation. Cannot be null.</param>
        /// <returns>The current instance of <see cref="CreateConversationBuilder"/> with the specified message added.</returns>
        public CreateConversationBuilder WithMessage(IActivity message)
        {
            AssertionHelpers.ThrowIfNull(message, nameof(message));
            _record.Parameters.Activity = message;
            return this;
        }

        /// <summary>
        /// Sets the channel-specific data for the conversation and returns the updated builder instance.
        /// </summary>
        /// <param name="channelData">The channel-specific data to associate with the conversation. Can be any object required by the channel. May
        /// be null if no channel data is needed.</param>
        /// <returns>The current instance of <see cref="CreateConversationBuilder"/> with the specified channel data applied.</returns>
        public CreateConversationBuilder WithChannelData(object channelData)
        {
            _record.Parameters.ChannelData = channelData;
            return this;
        }

        /// <summary>
        /// Specifies whether the conversation being created is a group conversation.
        /// </summary>
        /// <param name="isGroup">A value indicating whether the conversation should be treated as a group conversation.</param>
        /// <returns>The current instance of <see cref="CreateConversationBuilder"/> with the group setting applied.</returns>
        public CreateConversationBuilder IsGroup(bool isGroup)
        {
            _record.Parameters.IsGroup = isGroup;
            return this;
        }

        /// <summary>
        /// Sets the topic name for the conversation being created.
        /// </summary>
        /// <remarks>Use this method to specify a topic for the conversation before finalizing its
        /// creation. Calling this method multiple times will overwrite the previously set topic name.</remarks>
        /// <param name="topicName">The name of the topic to associate with the conversation. Cannot be null or empty.</param>
        /// <returns>The current instance of <see cref="CreateConversationBuilder"/> with the updated topic name.</returns>
        public CreateConversationBuilder WithTopicName(string topicName)
        {
            _record.Parameters.TopicName = topicName;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured instance of the CreateConversationRecord object.
        /// </summary>
        /// <returns>A CreateConversationRecord instance containing the configured create conversation parameters.</returns>
        public CreateConversationRecord Build()
        {
            return _record;
        }
    }
}
