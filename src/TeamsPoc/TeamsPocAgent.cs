// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.Messages;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsPoc;

[TeamsExtension]
public partial class TeamsPocAgent(AgentApplicationOptions options) : AgentApplication(options)
{

    [TeamsMessageRoute("/reply")]
    public async Task ReplyRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // quoting helpers
        await turnContext.ReplyAsync("Hello!", cancellationToken);
    }

    [TeamsMessageRoute("/mention")]
    public async Task MentionRoute(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // activity mention helper
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
                // proactive createConversation helper
                await turnContext.SendActivityAsync(teamMember, "This is a proactive message.");
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
            // proactive continueConversation helper
            await turnContext.SendActivityAsync(convId, proactiveMessage, cancellationToken);
        }
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
            "  - /notify      - send proactive notifications to all subscribed conversations\n"
        );
    }

    [ActivityRoute("message", rank: RouteRank.Last)]
    public async Task DefaultRoute(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}");
        await turnContext.SendActivityAsync("Enter \"/help\" to see a list of available commands.");
    }
}
