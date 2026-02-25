// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides a builder for configuring and creating a new conversation with specified conversation parameters.
    /// </summary>
    public class CreateConversationBuilder
    {
        private readonly CreateConversation _record = new();

        /// <summary>
        /// Creates a new instance of the CreateConversationBuilder class for initializing a conversation with the
        /// specified agent and channel.
        /// </summary>
        /// <remarks>If the parameters argument is null, default conversation parameters are used. If the
        /// Agent property of parameters is not set, it is initialized with the provided agentClientId.</remarks>
        /// <param name="agentClientId">The unique identifier of the agent client to associate with the conversation. Cannot be null or whitespace.</param>
        /// <param name="channelId">The identifier of the channel where the conversation will take place. Cannot be null or whitespace.</param>
        /// <param name="serviceUrl">The service URL to use for the conversation. If null, a default value may be used.</param>
        /// <param name="parameters">Optional parameters for configuring the conversation. If null, default parameters are used.</param>
        /// <returns>A CreateConversationBuilder instance configured with the specified agent, channel, and parameters.</returns>
        public static CreateConversationBuilder Create(string agentClientId, ChannelId channelId, string serviceUrl = null, ConversationParameters parameters = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentClientId, nameof(agentClientId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));

            var builder = new CreateConversationBuilder();

            builder._record.Conversation = ConversationBuilder.Create()
                .WithReference(ConversationReferenceBuilder.Create(agentClientId, channelId, serviceUrl).Build())
                .WithClaimsForClientId(agentClientId)
                .Build();

            builder._record.Parameters = parameters ?? new ConversationParameters();
            if (builder._record.Parameters.Agent == null)
            {
                builder._record.Parameters.Agent = new ChannelAccount(agentClientId);
            }

            return builder;
        }

        /// <summary>
        /// Creates a new instance of the CreateConversationBuilder class for initializing a conversation with the
        /// specified identity, channel, and parameters.
        /// </summary>
        /// <param name="claims">The ClaimsIdentity.Claims representing the agent or user initiating the conversation. Cannot be null.</param>
        /// <param name="channelId">The identifier of the channel where the conversation will be created. Cannot be null or whitespace.</param>
        /// <param name="serviceUrl">The service URL for the channel. If null, the default service URL is used.</param>
        /// <param name="parameters">Optional parameters for the conversation, such as participants or conversation metadata. If null, default
        /// parameters are used.</param>
        /// <returns>A CreateConversationBuilder instance configured with the specified identity, channel, and parameters.</returns>
        public static CreateConversationBuilder Create(IDictionary<string, string> claims, ChannelId channelId, string serviceUrl = null, ConversationParameters parameters = null)
        {
            AssertionHelpers.ThrowIfNull(claims, nameof(claims));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));

            var builder = new CreateConversationBuilder();

            var agentClientId = claims.FirstOrDefault(c => c.Key == "aud").Value;
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentClientId, "The claims dictionary must contain an 'aud' claim with the agent client ID as its value.");

            builder._record.Conversation = ConversationBuilder.Create()
                .WithReference(ConversationReferenceBuilder.Create(agentClientId, channelId, serviceUrl).Build())
                .WithClaims(claims)
                .Build();

            builder._record.Parameters = parameters ?? new ConversationParameters();
            if (builder._record.Parameters.Agent == null)
            {
                builder._record.Parameters.Agent = new ChannelAccount(agentClientId);
            }

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
            return WithUser(new ChannelAccount(userId, userName));
        }

        /// <summary>
        /// Specifies a user to include as a member in the conversation being created.
        /// </summary>
        /// <param name="user">The user account to add as a member of the conversation. Cannot be null.</param>
        /// <returns>The current <see cref="CreateConversationBuilder"/> instance for method chaining.</returns>
        public CreateConversationBuilder WithUser(ChannelAccount user)
        {
            if (user != null)
            {
                _record.Conversation.Reference.User = user;
                _record.Parameters.Members =
                [
                    user
                ];
            }
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
            _record.Scope = scope;
            return this;
        }

        /// <summary>
        /// Adds an Activity to the conversation being constructed.
        /// </summary>
        /// <param name="message">The activity representing the message to include in the conversation. Cannot be null.</param>
        /// <returns>The current instance of <see cref="CreateConversationBuilder"/> with the specified message added.</returns>
        public CreateConversationBuilder WithActivity(IActivity message)
        {
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
        /// Sets the tenant identifier for the conversation being created and returns the builder instance for method
        /// chaining.
        /// </summary>
        /// <param name="tenantId">The unique identifier of the tenant to associate with the conversation. Cannot be null or empty.</param>
        /// <returns>The current <see cref="CreateConversationBuilder"/> instance with the specified tenant identifier applied.</returns>
        public CreateConversationBuilder WithTenantId(string tenantId)
        {
            _record.Parameters.TenantId = tenantId;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured instance of the CreateConversation object.
        /// </summary>
        /// <returns>A CreateConversation instance containing the configured create conversation parameters.</returns>
        public CreateConversation Build()
        {
            if (_record.Parameters.Members?.Count == 0)
            {
                throw new ArgumentException("Parameters.Members must contain at least one member. Specify User before Build.");
            }

            if (string.IsNullOrWhiteSpace(_record.Scope))
            {
                _record.Scope = CreateConversation.AzureBotScope;
            }
            if (_record.Parameters.Activity != null && string.IsNullOrWhiteSpace(_record.Parameters.Activity.Type))
            {
                _record.Parameters.Activity.Type = ActivityTypes.Message;
            }
            return _record;
        }
    }
}
