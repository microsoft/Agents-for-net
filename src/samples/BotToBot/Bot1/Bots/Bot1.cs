// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Client;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.BotBuilder.Compat;
using System.Linq;

namespace Microsoft.Agents.Samples.Bots
{
    /// <summary>
    /// Sample RootBot.
    /// </summary>
    /// <remarks>
    /// This demonstrates send activities to another bot (Channe), and how to implement the IChannelResponseHandler on 
    /// the bot.  See BotFrameworkSkillHandler for the Bot Framework handler.
    /// </remarks>
    public class Bot1 : ActivityHandler, IChannelApiHandler
    {
        public static readonly string ActiveSkillPropertyName = $"{typeof(Bot1).FullName}.ActiveSkillProperty";
        private readonly IChannelAdapter _adapter;
        private readonly IConversationIdFactory _conversationIdFactory;
        private readonly IChannelHost _channelHost;

        // NOTE: For this sample, this is tracked in memory.  Definitely not a production thing.
        private static bool _activeBotClient = false;
        private readonly IChannelInfo _targetSkill;

        public Bot1(IChannelAdapter adapter, IChannelHost channelHost, IConversationIdFactory conversationIdFactory, IConfiguration configuration)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _channelHost = channelHost ?? throw new ArgumentNullException(nameof(channelHost));
            _conversationIdFactory = conversationIdFactory ?? throw new ArgumentNullException(nameof(conversationIdFactory));

            ArgumentNullException.ThrowIfNull(configuration);

            // We use a single channel in this example.
            var targetSkillId = "EchoSkillBot";
            _channelHost.Channels.TryGetValue(targetSkillId, out _targetSkill);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // Forward all activities except EndOfConversation to the skill.
            if (turnContext.Activity.Type != ActivityTypes.EndOfConversation)
            {
                // Try to get the active skill
                if (_activeBotClient)
                {
                    // Send the activity to the skill
                    await SendToBot(turnContext, _targetSkill, cancellationToken);
                    return;
                }
            }

            await base.OnTurnAsync(turnContext, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text.Contains("agent"))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Got it, connecting you to the agent..."), cancellationToken);

                // Save active skill in state
                _activeBotClient = true;

                // Send the activity to the skill
                await SendToBot(turnContext, _targetSkill, cancellationToken);
                return;
            }

            // just respond
            await turnContext.SendActivityAsync(MessageFactory.Text("Say \"agent\" and I'll patch you through"), cancellationToken);
        }

        protected override async Task OnEndOfConversationActivityAsync(ITurnContext<IEndOfConversationActivity> turnContext, CancellationToken cancellationToken)
        {
            // forget skill invocation
            _activeBotClient = false;

            // Show status message, text and value returned by the skill
            var eocActivityMessage = $"Received {ActivityTypes.EndOfConversation}.\n\nCode: {turnContext.Activity.Code}";
            if (!string.IsNullOrWhiteSpace(turnContext.Activity.Text))
            {
                eocActivityMessage += $"\n\nText: {turnContext.Activity.Text}";
            }

            if (turnContext.Activity?.Value != null)
            {
                eocActivityMessage += $"\n\nValue: {ProtocolJsonSerializer.ToJson(turnContext.Activity?.Value)}";
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(eocActivityMessage), cancellationToken);

            // We are back at the root
            await turnContext.SendActivityAsync(MessageFactory.Text("Back in the root bot. Say \"agent\" and I'll patch you through"), cancellationToken);
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
            // Create a conversationId to interact with the skill and send the activity
            var options = new ConversationIdFactoryOptions
            {
                FromBotOAuthScope = BotClaims.GetTokenScopes(turnContext.Identity).First(),
                FromBotId = _channelHost.HostAppId,
                Activity = turnContext.Activity,
                Bot = targetChannel
            };
            var botConversationId = await _conversationIdFactory.CreateConversationIdAsync(options, cancellationToken);

            using var channel = _channelHost.GetChannel(targetChannel);

            // route the activity to the skill
            var response = await channel.PostActivityAsync(targetChannel.AppId, targetChannel.ResourceUrl, targetChannel.Endpoint, _channelHost.HostEndpoint, botConversationId, turnContext.Activity, cancellationToken);

            // Check response status
            if (!(response.Status >= 200 && response.Status <= 299))
            {
                throw new HttpRequestException($"Error invoking the bot id: \"{targetChannel.Id}\" at \"{targetChannel.Endpoint}\" (status is {response.Status}). \r\n {response.Body}");
            }
        }

        //
        // IChannelResponseHandler
        //
        public async Task<ResourceResponse> OnSendActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, Activity activity, CancellationToken cancellationToken = default)
        {
            return await ProcessActivityAsync(claimsIdentity, conversationId, null, activity, cancellationToken).ConfigureAwait(false);
        }
        public async Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, Activity activity, CancellationToken cancellationToken = default)
        {
            return await ProcessActivityAsync(claimsIdentity, conversationId, null, activity, cancellationToken).ConfigureAwait(false);
        }
        public async Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, Activity activity, CancellationToken cancellationToken = default)
        {
            return await ProcessActivityAsync(claimsIdentity, conversationId, activityId, activity, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ResourceResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string replyToActivityId, Activity activity, CancellationToken cancellationToken)
        {
            var botConversationReference = await _conversationIdFactory.GetBotConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);

            ResourceResponse resourceResponse = null;
            var callback = new BotCallbackHandler(async (turnContext, ct) =>
            {
                activity.ApplyConversationReference(botConversationReference.ConversationReference);
                turnContext.Activity.Id = replyToActivityId;
                turnContext.Activity.CallerId = $"{CallerIdConstants.BotToBotPrefix}{BotClaims.GetOutgoingAppId(claimsIdentity)}";

                if (activity.Type == ActivityTypes.EndOfConversation)
                {
                    await _conversationIdFactory.DeleteConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);

                    ApplyActivityToTurnContext(turnContext, activity);
                    await OnTurnAsync(turnContext, ct).ConfigureAwait(false);
                }
                else
                {
                    resourceResponse = await turnContext.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                }
            });

            await _adapter.ContinueConversationAsync(claimsIdentity, botConversationReference.ConversationReference, botConversationReference.OAuthScope, callback, cancellationToken).ConfigureAwait(false);

            return resourceResponse ?? new ResourceResponse()
            {
                Id = (Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture))
            };
        }
        private static void ApplyActivityToTurnContext(ITurnContext turnContext, Activity activity)
        {
            turnContext.Activity.ChannelData = activity.ChannelData;
            turnContext.Activity.Code = activity.Code;
            turnContext.Activity.Entities = activity.Entities;
            turnContext.Activity.Locale = activity.Locale;
            turnContext.Activity.LocalTimestamp = activity.LocalTimestamp;
            turnContext.Activity.Name = activity.Name;
            turnContext.Activity.Properties = activity.Properties;
            turnContext.Activity.RelatesTo = activity.RelatesTo;
            turnContext.Activity.ReplyToId = activity.ReplyToId;
            turnContext.Activity.Timestamp = activity.Timestamp;
            turnContext.Activity.Text = activity.Text;
            turnContext.Activity.Type = activity.Type;
            turnContext.Activity.Value = activity.Value;
        }

        public Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, Activity activity, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task OnDeleteActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetActivityMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ConversationResourceResponse> OnCreateConversationAsync(ClaimsIdentity claimsIdentity, ConversationParameters parameters, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ConversationsResult> OnGetConversationsAsync(ClaimsIdentity claimsIdentity, string conversationId, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetConversationMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ChannelAccount> OnGetConversationMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PagedMembersResult> OnGetConversationPagedMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, int? pageSize = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task OnDeleteConversationMemberAsync(ClaimsIdentity claimsIdentity, string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> OnSendConversationHistoryAsync(ClaimsIdentity claimsIdentity, string conversationId, Transcript transcript, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> OnUploadAttachmentAsync(ClaimsIdentity claimsIdentity, string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
