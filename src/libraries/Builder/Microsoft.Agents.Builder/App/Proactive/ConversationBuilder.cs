// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides a builder for constructing instances of the Conversation class with configurable
    /// reference and claims information.
    /// </summary>
    /// <remarks>Use RecordBuilder to incrementally specify details of a conversation reference record, such as
    /// the user, agent, service URL, activity ID, and locale. This class is intended to simplify the creation of
    /// Conversation instances for use in Proactive scenarios. The builder ensures required
    /// fields are set and applies sensible defaults for optional properties if not specified.</remarks>
    public class ConversationBuilder
    {
        private Conversation _conversation = new();

        /// <summary>
        /// Creates a new instance of the RecordBuilder class for constructing record objects.
        /// </summary>
        /// <returns>A new RecordBuilder instance that can be used to configure and build record objects.</returns>
        public static ConversationBuilder Create()
        {
            return new ConversationBuilder();
        }

        /// <summary>
        /// Sets the conversation reference for the record being built.
        /// </summary>
        /// <param name="reference">The conversation reference to associate with the record. Cannot be null.</param>
        /// <returns>The current <see cref="ConversationBuilder"/> instance for method chaining.</returns>
        public ConversationBuilder WithReference(ConversationReference reference)
        {
            _conversation.Reference = reference;
            return this;
        }

        /// <summary>
        /// Adds claims to the record based on the specified client identifier and optional requestor identifier.
        /// </summary>
        /// <remarks>This method replaces any existing claims on the record with a new set containing the
        /// specified client and, if provided, requestor identifiers. Use this method to set claims relevant for
        /// authentication or authorization scenarios.</remarks>
        /// <param name="agentClientId">The client identifier to associate with the 'azp' claim. Cannot be null.</param>
        /// <param name="requestorId">An optional requestor identifier to associate with the 'appid' claim. If null or empty, the 'appid' claim is
        /// not added.</param>
        /// <returns>The current <see cref="ConversationBuilder"/> instance with the updated claims.</returns>
        public ConversationBuilder WithClaimsForClientId(string agentClientId, string requestorId = null)
        {        
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentClientId, nameof(agentClientId));
            var claims = new Dictionary<string, string>
            {
                { "aud", agentClientId },
            };
            if (!string.IsNullOrEmpty(requestorId))
            {
                claims["appid"] = requestorId;
            }
            _conversation = new Conversation(_conversation, claims);
            return this;
        }

        /// <summary>
        /// Sets the claims to associate with the record being built.
        /// </summary>
        /// <param name="claims">A dictionary containing claim types and their corresponding values to assign to the record. Cannot be null.</param>
        /// <returns>The current <see cref="ConversationBuilder"/> instance with the specified claims applied.</returns>
        public ConversationBuilder WithClaims(IDictionary<string, string> claims)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(claims, nameof(claims));
            _conversation = new Conversation(_conversation, claims);
            return this;
        }

        /// <summary>
        /// Sets the identity information for the record using the specified claims identity.
        /// </summary>
        /// <param name="identity">The claims identity to associate with the record. Cannot be null.</param>
        /// <returns>The current <see cref="ConversationBuilder"/> instance with the updated identity information.</returns>
        public ConversationBuilder WithIdentity(ClaimsIdentity identity)
        {
            _conversation = new Conversation(identity, _conversation.Reference);
            return this;
        }

        /// <summary>
        /// Builds and returns the configured conversation reference record.
        /// </summary>
        /// <returns>The constructed <see cref="Conversation"/> instance representing the current state of the
        /// builder.</returns>
        public Conversation Build()
        {
            if (!_conversation.IsValid())
            {
                throw new ArgumentException("Cannot build Conversation: missing required fields.");
            }

            return _conversation;
        }
    }
}
