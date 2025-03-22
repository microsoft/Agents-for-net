// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Agents.Connector.Types;
using System;
using Microsoft.Agents.Authentication;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Routes Channel responses to the Adapter incoming pipeline.  This is the same route an Agent normally gets incoming Activities.
    /// </summary>
    /// <remarks>
    /// The default method a Agent responds with is Activity.DeliverMode == `normal`.  This is an asynchronous response via an 
    /// HTTP POST to the endpoints defined by ChannelApiController.
    /// 
    /// <see cref="IChannelApiHandler"/> is all of the Connector API endpoints.  This implementation is just handling
    /// the Send/Reply from the other Agent.
    /// 
    /// This implementation will send a custom Event to the Adapter, and the AgentApplication can add a route for
    /// <see cref="AdapterChannelResponseHandler.ChannelReplyEventName"/>.  The Event Activity.Value will be an instance of <see cref="ChannelReply"/>.
    /// </remarks>
    /// <remarks>
    /// This implementation does not handle any of the other Connector API endpoints.
    /// </remarks>
    internal class AdapterChannelResponseHandler : IChannelApiHandler
    {
        public class ChannelReply
        {
            public AgentConversationReference ChannelConversationReference { get; set; }
            public IActivity Activity { get; set; }
        }

        public const string ChannelReplyEventName = "application/vnd.microsoft.agents.ChannelReply";

        private readonly IChannelAdapter _adapter;
        private readonly IBot _bot;
        private readonly IAgentHost _channelHost;

        public AdapterChannelResponseHandler(IChannelAdapter adapter, IBot bot, IAgentHost channelHost)
        {
            _adapter = adapter;
            _bot = bot;
            _channelHost = channelHost;
        }

        public async Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            var conversationReference = await _channelHost.GetConversationReferenceAsync(conversationId, cancellationToken);
            if (conversationReference == null)
            {
                // Received a conversationId that isn't known.
                // Probably should throw an exception.
                return null;
            }

            // Need to get this over to the calling Agents identity.  We will do it by packaging it in a custom event
            // and the AgentApplication will need to route to a handler.  See ChannelResponses.OnChannelReply.
            var eventActivity = new Activity()
            {
                Type = ActivityTypes.Event,
                Name = ChannelReplyEventName,
                Value = new ChannelReply() { ChannelConversationReference = conversationReference, Activity = activity },
            };
            eventActivity.ApplyConversationReference(conversationReference.ConversationReference, isIncoming: true);

            // We can't use the incoming ClaimsIdentity to send to the Adapter.
            // Perhaps a better way to do this, but what ChannelServiceAdapterBase does in ContinueConversation.
            var hostClaimsIdentity = new ClaimsIdentity(
            [
                new(AuthenticationConstants.AudienceClaim, _channelHost.HostClientId),
                new(AuthenticationConstants.AppIdClaim, _channelHost.HostClientId),
            ]);

            await _adapter.ProcessActivityAsync(hostClaimsIdentity, eventActivity, _bot.OnTurnAsync, cancellationToken);

            // This implementation isn't actually keeping track of Activities, so just make up an ActivityId.
            return new ResourceResponse() { Id = Guid.NewGuid().ToString() };
        }

        public Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            return OnSendToConversationAsync(claimsIdentity, conversationId, activity, cancellationToken);
        }

        public Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task OnDeleteActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetActivityMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ConversationResourceResponse> OnCreateConversationAsync(ClaimsIdentity claimsIdentity, ConversationParameters parameters, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ConversationsResult> OnGetConversationsAsync(ClaimsIdentity claimsIdentity, string conversationId, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetConversationMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ChannelAccount> OnGetConversationMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<PagedMembersResult> OnGetConversationPagedMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, int? pageSize = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task OnDeleteConversationMemberAsync(ClaimsIdentity claimsIdentity, string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ResourceResponse> OnSendConversationHistoryAsync(ClaimsIdentity claimsIdentity, string conversationId, Transcript transcript, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ResourceResponse> OnUploadAttachmentAsync(ClaimsIdentity claimsIdentity, string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
