﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Client;
using Microsoft.Agents.Protocols.Primitives;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// This contains the routes for a channel service controller.  These are the endpoints that
    /// RestConnectorClient calls back to in the case of a bot-to-bot or Bot Framework Skill.
    /// The implementation of this is via <see cref="IChannelResponseHandler"/>.
    /// See the BotFrameworkSkillHandler class for the Bot Framework Root bot handling of these.
    /// <see cref="IConnectorClient"/>
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ChannelServiceController"/> class.
    /// </remarks>
    /// <param name="handler">A <see cref="IChannelResponseHandler"/> that will handle the incoming request.</param>
    // Note: this class is marked as abstract to prevent the ASP runtime from registering it as a controller.
    [ChannelServiceExceptionFilter]
    public abstract class ChannelServiceController(IChannelResponseHandler handler) : ControllerBase
    {
        private readonly IChannelResponseHandler _handler = handler;

        /// <summary>
        /// SendToConversation.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="activity">Activity to send.</param>
        /// <returns>Task representing result of sending activity to conversation.</returns>
        [HttpPost("v3/conversations/{conversationId}/activities")]
        public virtual async Task<IActionResult> SendToConversationAsync(string conversationId)
        {
            var activity = await GetActivityAsync();
            if (activity == null)
            {
                return null;
            }

            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnSendToConversationAsync(claimsIdentity, conversationId, activity).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// ReplyToActivity.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="activityId">activityId the reply is to (OPTIONAL).</param>
        /// <param name="activity">Activity to send.</param>
        /// <returns>Task representing result of replying to activity.</returns>
        [HttpPost("v3/conversations/{conversationId}/activities/{activityId}")]
        public virtual async Task<IActionResult> ReplyToActivityAsync(string conversationId, string activityId)
        {
            var activity = await GetActivityAsync();
            if (activity == null)
            {
                return null;
            }

            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnReplyToActivityAsync(claimsIdentity, conversationId, activityId, activity).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// UpdateActivity.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="activityId">activityId to update.</param>
        /// <param name="activity">replacement Activity.</param>
        /// <returns>Task representing result of updating activity.</returns>
        [HttpPut("v3/conversations/{conversationId}/activities/{activityId}")]
        public virtual async Task<IActionResult> UpdateActivityAsync(string conversationId, string activityId)
        {
            var activity = await GetActivityAsync();
            if (activity == null)
            {
                return null;
            }

            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnUpdateActivityAsync(claimsIdentity, conversationId, activityId, activity).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// DeleteActivity.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="activityId">activityId to delete.</param>
        /// <returns>Task representing result of deleting activity.</returns>
        [HttpDelete("v3/conversations/{conversationId}/activities/{activityId}")]
        public virtual async Task DeleteActivityAsync(string conversationId, string activityId)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            await _handler.OnDeleteActivityAsync(claimsIdentity, conversationId, activityId).ConfigureAwait(false);
        }

        /// <summary>
        /// GetActivityMembers.
        /// </summary>
        /// <remarks>
        /// Markdown=Content\Methods\GetActivityMembers.md.
        /// </remarks>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="activityId">Activity ID.</param>
        /// <returns>Task representing result of getting activity members.</returns>
        [HttpGet("v3/conversations/{conversationId}/activities/{activityId}/members")]
        public virtual async Task<IActionResult> GetActivityMembersAsync(string conversationId, string activityId)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnGetActivityMembersAsync(claimsIdentity, conversationId, activityId).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// CreateConversation.
        /// </summary>
        /// <param name="parameters">Parameters to create the conversation from.</param>
        /// <returns>Task representing result of creating conversation.</returns>
        [HttpPost("v3/conversations")]
        public virtual async Task<IActionResult> CreateConversationAsync([FromBody] ConversationParameters parameters)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnCreateConversationAsync(claimsIdentity, parameters).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// GetConversations.
        /// </summary>
        /// <param name="continuationToken">skip or continuation token.</param>
        /// <returns>Task representing result of getting conversations.</returns>
        [HttpGet("v3/conversations")]
        public virtual async Task<IActionResult> GetConversationsAsync(string continuationToken = null)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnGetConversationsAsync(claimsIdentity, continuationToken).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// GetConversationMembers.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <returns>Task representing result of getting conversation members.</returns>
        [HttpGet("v3/conversations/{conversationId}/members")]
        public virtual async Task<IActionResult> GetConversationMembersAsync(string conversationId)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnGetConversationMembersAsync(claimsIdentity, conversationId).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// GetConversationMember.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="conversationId">Conversation ID.</param>
        /// <returns>Task representing result of getting ChannelAccount of specific conversation member.</returns>
        [HttpGet("v3/conversations/{conversationId}/members/{userId}")]
        public virtual async Task<IActionResult> GetConversationMemberAsync(string userId, string conversationId)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnGetConversationMemberAsync(claimsIdentity, userId, conversationId).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// GetConversationPagedMembers.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="pageSize">Suggested page size.</param>
        /// <param name="continuationToken">Continuation Token.</param>
        /// <returns>Task representing result of getting conversation members as paged result.</returns>
        [HttpGet("v3/conversations/{conversationId}/pagedmembers")]
        public virtual async Task<IActionResult> GetConversationPagedMembersAsync(string conversationId, int pageSize = -1, string continuationToken = null)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnGetConversationPagedMembersAsync(claimsIdentity, conversationId, pageSize, continuationToken).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// DeleteConversationMember.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="memberId">ID of the member to delete from this conversation.</param>
        /// <returns>Task representing result of deleting the conversation member.</returns>
        [HttpDelete("v3/conversations/{conversationId}/members/{memberId}")]
        public virtual async Task DeleteConversationMemberAsync(string conversationId, string memberId)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            await _handler.OnDeleteConversationMemberAsync(claimsIdentity, conversationId, memberId).ConfigureAwait(false);
        }

        /// <summary>
        /// SendConversationHistory.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="history">Historic activities.</param>
        /// <returns>Task representing the result of sending conversation history.</returns>
        [HttpPost("v3/conversations/{conversationId}/activities/history")]
        public virtual async Task<IActionResult> SendConversationHistoryAsync(string conversationId, [FromBody] Transcript history)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnSendConversationHistoryAsync(claimsIdentity, conversationId, history).ConfigureAwait(false);
            return new JsonResult(result);
        }

        /// <summary>
        /// UploadAttachment.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="attachmentUpload">Attachment data.</param>
        /// <returns>Task representing the result of uploading attachment.</returns>
        [HttpPost("v3/conversations/{conversationId}/attachments")]
        public virtual async Task<IActionResult> UploadAttachmentAsync(string conversationId, [FromBody] AttachmentData attachmentUpload)
        {
            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnUploadAttachmentAsync(claimsIdentity, conversationId, attachmentUpload).ConfigureAwait(false);
            return new JsonResult(result);
        }

        private async Task<Activity> GetActivityAsync()
        {
            //if (!User.Identity.IsAuthenticated)
            //{
            //    Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            //    return null;
            //}

            return await HttpHelper.ReadRequestAsync<Activity>(Request).ConfigureAwait(false);
        }
    }
}
