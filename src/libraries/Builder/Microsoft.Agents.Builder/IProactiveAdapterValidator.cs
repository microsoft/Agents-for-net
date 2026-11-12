// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System.Security.Claims;

namespace Microsoft.Agents.Builder
{
    // COMPAT(next-major): Keep this contract optional: adding required members to IChannelAdapter
    // would break existing custom adapters. Next major, review making validation part of
    // IChannelAdapter and removing Proactive's legacy validation fallbacks. Direct custom
    // IChannelAdapter implementations would need to implement the validation methods.
    /// <summary>
    /// Allows a channel adapter to validate proactive operations according to its transport.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="Microsoft.Agents.Builder.ChannelAdapter"/>.
    /// Adapters implementing only <see cref="Microsoft.Agents.Builder.IChannelAdapter"/> retain
    /// the legacy Activity Protocol validation in <see cref="Microsoft.Agents.Builder.App.Proactive.Proactive"/>.
    /// </remarks>
    public interface IProactiveAdapterValidator
    {
        /// <summary>
        /// Validates a conversation before a proactive send or continuation.
        /// </summary>
        /// <param name="identity">The identity used for the proactive operation.</param>
        /// <param name="reference">The conversation to continue.</param>
        void ValidateConversation(ClaimsIdentity identity, ConversationReference reference);

        /// <summary>
        /// Validates the options before creating a conversation.
        /// </summary>
        /// <param name="options">The options for the new conversation.</param>
        void ValidateCreateConversation(CreateConversationOptions options);
    }
}
