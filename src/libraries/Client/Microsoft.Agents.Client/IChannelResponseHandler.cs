// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Activity Protocol callback implementation.
    /// </summary>
    public interface IChannelResponseHandler
    {
        /// <summary>
        /// SendToConversation() API.
        /// </summary>
        /// <remarks>
        /// This method allows you to send an activity to the end of a conversation.
        ///
        /// This is slightly different from ReplyToActivity().
        /// * SendToConversation(conversationId) - will append the activity to the end
        /// of the conversation according to the timestamp or semantics of the channel.
        /// * ReplyToActivity(conversationId,ActivityId) - adds the activity as a reply
        /// to another activity, if the channel supports it. If the channel does not
        /// support nested replies, ReplyToActivity falls back to SendToConversation.
        ///
        /// Use ReplyToActivity when replying to a specific activity in the
        /// conversation.
        ///
        /// Use SendToConversation in all other cases.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>conversationId.</param> 
        /// <param name='activity'>Activity to send.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a resource response.</returns>
        Task<ResourceResponse> OnSendActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, Activity activity, CancellationToken cancellationToken = default);
    }
}
