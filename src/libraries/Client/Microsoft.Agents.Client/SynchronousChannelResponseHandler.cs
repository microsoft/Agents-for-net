// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Connector.Types;

namespace Microsoft.Agents.Client
{
    public class SynchronousChannelResponseHandler : IChannelApiHandler
    {
        private static readonly ConcurrentDictionary<string, Channel<IActivity>> _conversations = new();

        public SynchronousChannelResponseHandler() 
        {
        }

        public async Task HandleResponses(string channelConversationId, Action<IActivity> action, CancellationToken cancellationToken)
        {
            var channel = _conversations.GetOrAdd(channelConversationId, Channel.CreateUnbounded<IActivity>());

            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                var activity = await channel.Reader.ReadAsync(cancellationToken);
                action(activity);
            }

            _conversations.Remove(channelConversationId, out _);
        }

        public void CompleteHandlerForConversation(string channelConversationId)
        {
            if (_conversations.TryGetValue(channelConversationId, out var channel))
            {
                channel.Writer.Complete();

                // TODO: need to cleanup up completed Channels at some point.
            }
        }

        public async Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, IActivity activity, CancellationToken cancellationToken)
        {
            var channel = _conversations.GetOrAdd(activity.Conversation.Id, Channel.CreateUnbounded<IActivity>());

            activity.Id = Guid.NewGuid().ToString();
            var response = new ResourceResponse()
            {
                Id = activity.Id,
            };

            if (activity.Type == "event" && activity.Name == "endOfTurn")
            {
                // No more responses are expected.
                channel.Writer.Complete();
            }
            else
            {
                // Write the Activity to the Channel.  It is consumed on the other side via HandleResponses.
                await channel.Writer.WriteAsync(activity, cancellationToken);
            }

            return response;
        }
        public async Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken)
        {
            return await OnSendToConversationAsync(claimsIdentity, conversationId, activity, cancellationToken).ConfigureAwait(false);
        }

        public Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task OnDeleteActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetActivityMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ConversationResourceResponse> OnCreateConversationAsync(ClaimsIdentity claimsIdentity, ConversationParameters parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ConversationsResult> OnGetConversationsAsync(ClaimsIdentity claimsIdentity, string conversationId, string continuationToken, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetConversationMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ChannelAccount> OnGetConversationMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<PagedMembersResult> OnGetConversationPagedMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, int? pageSize, string continuationToken, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task OnDeleteConversationMemberAsync(ClaimsIdentity claimsIdentity, string conversationId, string memberId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> OnSendConversationHistoryAsync(ClaimsIdentity claimsIdentity, string conversationId, Transcript transcript, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> OnUploadAttachmentAsync(ClaimsIdentity claimsIdentity, string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
