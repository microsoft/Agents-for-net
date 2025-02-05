// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Bot1
{
    /// <summary>
    /// Sample Agent that performs a multi-turn conversation with another Agent.
    /// </summary>
    public class HostBot(IChannelHost channelHost, IConversationIdFactory conversationIdFactory) : ActivityHandler
    {
        private readonly IConversationIdFactory _conversationIdFactory = conversationIdFactory ?? throw new ArgumentNullException(nameof(conversationIdFactory));
        private readonly IChannelHost _channelHost = channelHost ?? throw new ArgumentNullException(nameof(channelHost));
        private const string Bot2Alias = "EchoBot";

        /// <inheritdoc/>
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            // Show a welcome message with instructions
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // Get active conversationId being used for the other bot.  If null, a conversation hasn't been started.
            var channelConversationId = await GetOrCreateConversationId(turnContext, cancellationToken);

            if (channelConversationId == null)
            {
                // Respond with instructions
                await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
            }
            else
            {
                var channel = _channelHost.GetChannel(Bot2Alias);

                // Forward whatever C2 sent to the channel until a result is returned.
                var result = await channel.SendActivityForResultAsync<object>(
                    channelConversationId,
                    turnContext.Activity,
                    async (activity) =>
                    {
                        // Just repeat message to C2
                        await turnContext.SendActivityAsync(MessageFactory.Text($"({channel.DisplayName}) {activity.Text}"), cancellationToken);
                    },
                    cancellationToken);

                // In this sample, the Bot2 will send an EndOfConversation with a result when "end" is sent.
                if (result != null)
                {
                    var resultMessage = $"The channel returned:\n\n: {ProtocolJsonSerializer.ToJson(result)}";
                    await turnContext.SendActivityAsync(MessageFactory.Text(resultMessage), cancellationToken);

                    // Remove the channels conversation reference
                    await _conversationIdFactory.DeleteConversationReferenceAsync(channelConversationId, cancellationToken);

                    // Done with calling the remote Agent.
                    await turnContext.SendActivityAsync(MessageFactory.Text("Back in the root bot. Say \"agent\" and I'll patch you through"), cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task OnEndOfConversationActivityAsync(ITurnContext<IEndOfConversationActivity> turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Received EndOfConversation from ChannelService"), cancellationToken);

            // C2-side is ending the conversation.  Send an EndOfConversation to Bot2.
            var channelConversationId = await _conversationIdFactory.GetBotConversationIdAsync(turnContext, _channelHost.HostAppId, Bot2Alias, cancellationToken);
            if (channelConversationId != null)
            {
                await _channelHost.GetChannel(Bot2Alias).SendActivityAsync(
                    channelConversationId,
                    Activity.CreateEndOfConversationActivity(), 
                    cancellationToken);

                // This conversation is over.
                await _conversationIdFactory.DeleteConversationReferenceAsync(channelConversationId, cancellationToken);
            }
        }

        private async Task<string> GetOrCreateConversationId(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // Get active conversationId being used for the other bot.  If null, a conversation hasn't been started.
            var channelConversationId = await _conversationIdFactory.GetBotConversationIdAsync(turnContext, _channelHost.HostAppId, Bot2Alias, cancellationToken);

            // If C2 sends "agent", start sending to Bot2.
            if (channelConversationId == null && turnContext.Activity.Text.Contains("agent"))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Got it, connecting you to the agent..."), cancellationToken);

                // Create a new conversationId for the Bot2.  This conversationId should be used for all subsequent messages until a result is returned.
                channelConversationId = await _conversationIdFactory.CreateConversationIdAsync(turnContext, _channelHost.HostAppId, Bot2Alias, cancellationToken);
            }

            return channelConversationId;
        }
    }
}
