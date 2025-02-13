﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Storage;
using System;

namespace Microsoft.Agents.State
{
    /// <summary>
    /// Defines a state management object for user state.
    /// </summary>
    /// <remarks>
    /// User state is available in any turn that the bot is conversing with that user on that
    /// channel, regardless of the conversation.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="UserState"/> class.
    /// </remarks>
    /// <param name="storage">The storage layer to use.</param>
    public class UserState(IStorage storage) : BotState(storage, nameof(UserState))
    {
        /// <summary>
        /// Gets the key to use when reading and writing state to and from storage.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <returns>The storage key.</returns>
        /// <remarks>
        /// User state includes the channel ID and user ID as part of its storage key.
        /// </remarks>
        protected override string GetStorageKey(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var userId = turnContext.Activity.From?.Id ?? throw new InvalidOperationException("invalid activity-missing From.Id");
            return $"{channelId}/users/{userId}";
        }
    }
}
