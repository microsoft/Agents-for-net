// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Proactive
{
    public interface IProactiveAgent
    {
        // TODO: createConversation

        Task SendActivityAsync(IChannelAdapter adapter, string conversationId, IActivity activity, CancellationToken cancellationToken = default);
        Task SendActivityAsync(IChannelAdapter adapter, ConversationReferenceRecord record, IActivity activity, CancellationToken cancellationToken = default);
        Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, RouteHandler handler, CancellationToken cancellationToken = default);
        Task ContinueConversationAsync(IChannelAdapter adapter, ClaimsIdentity identity, ConversationReference reference, RouteHandler handler, CancellationToken cancellationToken = default);
    }
}
