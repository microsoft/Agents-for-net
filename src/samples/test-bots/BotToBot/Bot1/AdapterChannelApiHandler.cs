using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Agents.Connector.Types;
using System;

namespace Bot1
{
    public class AdapterChannelApiHandler : IChannelApiHandler
    {
        private readonly IChannelAdapter _adapter;
        private readonly IBot _bot;

        public AdapterChannelApiHandler(IChannelAdapter adapter, IBot bot)
        {
            _adapter = adapter;
            _bot = bot;
        }

        public async Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, Activity activity, CancellationToken cancellationToken = default)
        {
            await _adapter.ProcessActivityAsync(claimsIdentity, activity, _bot.OnTurnAsync, cancellationToken);

            // This is technically wrong.  We just don't have a way to return the real Activity Id.
            return new ResourceResponse() {  Id = Guid.NewGuid().ToString()  };
        }

        public Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, Activity activity, CancellationToken cancellationToken = default)
        {
            return OnSendToConversationAsync(claimsIdentity, conversationId, activity, cancellationToken);
        }

        public Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, Activity activity, CancellationToken cancellationToken = default)
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
