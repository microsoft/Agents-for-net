// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Proactive;

public class ProactiveAgent : AgentApplication
{
    public ProactiveAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Manual way to store a conversation for use in Proactive.  This is for sample purposes only.
        OnMessage("-s", async (turnContext, turnState, cancellationToken) =>
        {
            var id = await Proactive.StoreConversationAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync($"Conversation '{id}' stored", cancellationToken: cancellationToken);
        });

        // ContinueConversation by routing to AgentApplication.  This allows both TurnState and OAuth tokens
        // to be available.
        OnMessage(new Regex("-c.*"), async (turnContext, turnState, cancellationToken) =>
        {
            var split = turnContext.Activity.Text.Split(' ');
            var conversationId = split.Length == 1 ? turnContext.Activity.Conversation.Id : split[1];

            await Proactive.ContinueConversationAsync(turnContext.Adapter, conversationId, cancellationToken);
        });

        // Events can contain an Activity.Value which further describes the event.  This can be used to route to different handlers.
        // In this case, we look for a property "extended" with a value of true to route to a different handler.
        // For example, from POSTing to /proactive/continueconversation/{{id}} with a body of { "extended": true }
        // This is highly dependent on what was passed to Proactive.ContinueConversationAsync(IChannelAdapter, string, IDictionary<string, object>)
        Proactive.AddContinueConversationRoute(OnContinueConversationExtendedAsync, selector: (context, ct) =>
        {
            if (context.Activity.Value == null || context.Activity.ValueType != Microsoft.Agents.Builder.App.Proactive.Proactive.ContinueConversationValueType)
            {
                return Task.FromResult(false);
            }

            var continueProperties = (IDictionary<string, object>)context.Activity.Value;
            if (continueProperties.TryGetValue("extended", out var extValue))
            {
                return Task.FromResult(extValue is JsonElement jsonValueKind ? jsonValueKind.GetBoolean() : (bool)extValue);
            }

            return Task.FromResult(false);
        });

        // This will route all ContinueConversation Events to the same handler, with OAuth token from the "me" handler.
        Proactive.AddContinueConversationRoute(OnContinueConversationAsync, autoSignInHandlers: ["me"]);

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    private async Task OnContinueConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await UserAuthorization.GetTurnTokenAsync(turnContext, cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync($"This is ContinueConversation with token len={token.Length}", cancellationToken: cancellationToken);
    }

    private async Task OnContinueConversationExtendedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"This is ContinueConversationExtended. Value={JsonSerializer.Serialize(turnContext.Activity.Value)}", cancellationToken: cancellationToken);
    }
}
