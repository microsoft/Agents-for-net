// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Client;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Compat;
using System.Linq;

namespace Microsoft.Agents.Samples.Bots
{
    /// <summary>
    /// Sample RootBot.
    /// </summary>
    public class HostBot : ActivityHandler
    {
        public static readonly string ActiveSkillPropertyName = $"{typeof(HostBot).FullName}.ActiveSkillProperty";
        private readonly IConversationIdFactory _conversationIdFactory;
        private readonly IChannelHost _channelHost;

        // NOTE: For this sample, this is tracked in memory.  Definitely not a production thing.
        private static string _skillConversationId;
        private readonly IChannelInfo _targetSkill;

        public HostBot(IChannelHost channelHost, IConversationIdFactory conversationIdFactory)
        {
            _channelHost = channelHost ?? throw new ArgumentNullException(nameof(channelHost));
            _conversationIdFactory = conversationIdFactory ?? throw new ArgumentNullException(nameof(conversationIdFactory));

            // We use a single channel in this example.
            var targetSkillId = "EchoSkillBot";
            _channelHost.Channels.TryGetValue(targetSkillId, out _targetSkill);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // In this POC, Bot2 responses are sent to the Adapter by AdapterChannelApiHandler.  If
            // it's from another bot, handle this as a bot response and not an incoming message.
            if (turnContext.Activity.From.Role == "skill")
            {
                await HandleBotResponseAsync(turnContext, cancellationToken);
                return;
            }

            // Forward all activities to Bot2 if active.
            if (!string.IsNullOrEmpty(_skillConversationId))
            {
                await SendToBot(turnContext, _targetSkill, cancellationToken);
                return;
            }

            await base.OnTurnAsync(turnContext, cancellationToken);
        }

        private async Task HandleBotResponseAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var botConversationReference = await _conversationIdFactory.GetBotConversationReferenceAsync(turnContext.Activity.Conversation.Id, cancellationToken);
            if (botConversationReference != null)
            {
                if (botConversationReference.BotName == "EchoSkillBot")
                {
                    await HandleEchoBotResponseAsync(turnContext, botConversationReference, cancellationToken);
                }
            }
        }

        private async Task HandleEchoBotResponseAsync(ITurnContext turnContext, BotConversationReference conversationReference, CancellationToken cancellationToken)
        {
            var botResponseActivity = turnContext.Activity.Clone();

            var callback = new BotCallbackHandler(async (turnContext, ct) =>
            {
                if (botResponseActivity.Type == ActivityTypes.EndOfConversation)
                {
                    // forget skill invocation
                    _skillConversationId = null;

                    // Show status message, text and value returned by the skill
                    var eocActivityMessage = $"Received {ActivityTypes.EndOfConversation}.\n\nCode: {botResponseActivity.Code}";
                    if (!string.IsNullOrWhiteSpace(turnContext.Activity.Text))
                    {
                        eocActivityMessage += $"\n\nText: {botResponseActivity.Text}";
                    }

                    if (turnContext.Activity?.Value != null)
                    {
                        eocActivityMessage += $"\n\nValue: {ProtocolJsonSerializer.ToJson(botResponseActivity?.Value)}";
                    }

                    await turnContext.SendActivityAsync(MessageFactory.Text(eocActivityMessage), cancellationToken);

                    // We are back at the root
                    await turnContext.SendActivityAsync(MessageFactory.Text("Back in the root bot. Say \"agent\" and I'll patch you through"), cancellationToken);
                }
                else
                {
                    // Just send the Bot2 response to ABS
                    await turnContext.SendActivityAsync(MessageFactory.Text(botResponseActivity.Text), cancellationToken);
                }
            });

            await turnContext.Adapter.ContinueConversationAsync(_channelHost.HostAppId, conversationReference.ConversationReference, callback, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text.Contains("agent"))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Got it, connecting you to the agent..."), cancellationToken);

                // Send the activity to the skill
                await SendToBot(turnContext, _targetSkill, cancellationToken);

                return;
            }

            // C2 didn't say "agent" so respond with instructions
            await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
                }
            }
        }

        private async Task SendToBot(ITurnContext turnContext, IChannelInfo targetChannel, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_skillConversationId))
            {
                // Create a conversationId to interact with the skill and send the activity
                var options = new ConversationIdFactoryOptions
                {
                    FromBotOAuthScope = BotClaims.GetTokenScopes(turnContext.Identity)?.First(),
                    FromBotId = _channelHost.HostAppId,
                    Activity = turnContext.Activity,
                    Bot = targetChannel
                };
                _skillConversationId = await _conversationIdFactory.CreateConversationIdAsync(options, cancellationToken);
            }

            using var channel = _channelHost.GetChannel(targetChannel);

            // route the activity to the skill
            var response = await channel.PostActivityAsync(targetChannel.AppId, targetChannel.ResourceUrl, targetChannel.Endpoint, _channelHost.HostEndpoint, _skillConversationId, turnContext.Activity, cancellationToken);

            // Check response status
            if (!(response.Status >= 200 && response.Status <= 299))
            {
                throw new HttpRequestException($"Error invoking the bot id: \"{targetChannel.Id}\" at \"{targetChannel.Endpoint}\" (status is {response.Status}). \r\n {response.Body}");
            }
        }
    }
}
