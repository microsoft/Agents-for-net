// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Graph.Models;
using Microsoft.Teams.Api.Messages;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsAgent;

#pragma warning disable ExperimentalTeamsReactions // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


[TeamsExtension]
public partial class TeamsAgent(AgentApplicationOptions options) : AgentApplication(options)
{

    [TeamsMessageRoute("/reply")]
    public async Task ReplyRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // ReplyAsync sends a quoted reply to the incoming message.
        await turnContext.ReplyAsync("Hello!", cancellationToken);
    }

    [TeamsMessageRoute("/mention")]
    public async Task MentionRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Build a reply that @mentions the sender before appending text.
        var replyActivity = turnContext.Activity.CreateReply()
            .AddMention(turnContext.Activity.From)
            .AddText(", hello.");
        await turnContext.SendActivityAsync(replyActivity, cancellationToken);
    }

    [TeamsMessageRoute("/messageteam")]
    public async Task MessageTeamRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        do
        {
            var currentPage = await TeamsInfo.GetPagedMembersAsync(turnContext, 100, continuationToken!, cancellationToken);
            continuationToken = currentPage.ContinuationToken;

            foreach (var teamMember in currentPage.Members)
            {
                // Create a proactive 1:1 conversation with each member and send a message.
                await turnContext.CreateConversationAsync(teamMember, "This is a proactive message.");
            }
        }
        while (continuationToken != null);

        await turnContext.SendActivityAsync("All messages have been sent.", cancellationToken: cancellationToken);
    }

    [TeamsMessageRoute("/subscribe")]
    public async Task SubscribeRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var subscribedConversations = turnState.User.GetValue<List<string>>("subscribedUsers", () => new List<string>());


        if (!subscribedConversations.Contains(turnContext.Activity.Conversation.Id))
        {
            subscribedConversations.Add(turnContext.Activity.Conversation.Id);
            turnState.User.SetValue("subscribedUsers", subscribedConversations);
            await turnContext.SendActivityAsync("You have been subscribed to proactive messages.", cancellationToken: cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync("You are already subscribed to proactive messages.", cancellationToken: cancellationToken);

        }
    }

    [TeamsMessageRoute("/notify")]
    public async Task ContinueConversationRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var subscribedConversations = turnState.User.GetValue<List<string>>("subscribedUsers", () => new List<string>());

        if (subscribedConversations.Count == 0)
        {
            await turnContext.SendActivityAsync("No conversations are subscribed to proactive messages.", cancellationToken: cancellationToken);
            return;
        }
        await turnContext.SendActivityAsync($"Sending proactive messages to {subscribedConversations.Count} subscribed conversations...", cancellationToken: cancellationToken);

        foreach (var convId in subscribedConversations)
        {
            var proactiveMessage = MessageFactory.Text("This is a notification to a subscribed conversation.");
            // Continue each stored conversation and deliver the notification activity.
            await turnContext.ContinueConversationAsync(convId, proactiveMessage, cancellationToken);
        }
    }

    [TeamsMessageRoute("/react")]
    public async Task ReactRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Demonstrate temporary reactions by adding one, waiting, and then removing it.
        await turnContext.AddReactionAsync(ReactionType.Eyes, cancellationToken: cancellationToken);
        await Task.Delay(5000, cancellationToken);
        await turnContext.DeleteReactionAsync(ReactionType.Eyes, cancellationToken: cancellationToken);
    }

    [MessageRoute("/help")]
    public async Task IDKRoute(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(
            "Commands:\n" +
            "  - /reply       - reply to the user\n" +
            "  - /mention     - mention the user\n" +
            "  - /messageteam - proactively start a new conversation with every member in the conversation\n" +
            "  - /subscribe   - subscribe the conversation to future proactive notifications\n" +
            "  - /notify      - send proactive notifications to all subscribed conversations\n" +
            "  - /react       - react to the user's message\n"
        );
    }

    [ActivityRoute("message", rank: RouteRank.Last)]
    public async Task DefaultRoute(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}");
        await turnContext.SendActivityAsync("Enter \"/help\" to see a list of available commands.");
    }
}