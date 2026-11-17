// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder.App.Proactive
{
    public static class ProactiveValidationExtensions
    {
        /// <summary>
        /// Validates a CreateConversation instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        [Obsolete("Use proactive validation on the selected adapter through IProactiveAdapterValidator.")]
        public static void Validate(this CreateConversationOptions createConversation)
        {
            ChannelServiceAdapterBase.ValidateCreateConversationOptions(createConversation);
        }

        /// <summary>
        /// Validates a Conversation instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        [Obsolete("Use proactive validation on the selected adapter through IProactiveAdapterValidator.")]
        public static void Validate(this Conversation conversation, bool validateConversation = true)
        {
            ChannelServiceAdapterBase.ValidateLegacyConversation(conversation, validateConversation);
        }

        /// <summary>
        /// Validates a ConversationParameters instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        [Obsolete("Use proactive validation on the selected adapter through IProactiveAdapterValidator.")]
        public static void Validate(this ConversationParameters parameters)
        {
            ChannelServiceAdapterBase.ValidateConversationParameters(parameters);
        }

        /// <summary>
        /// Validates a ConversationReference instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        [Obsolete("Use proactive validation on the selected adapter through IProactiveAdapterValidator.")]
        public static void Validate(this ConversationReference reference, bool validateConversation = true)
        {
            ChannelServiceAdapterBase.ValidateConversationReference(reference, validateConversation);
        }
    }
}
