// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.A2A;
using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace A2AAgent;

public class MyAgent : AgentApplication, IAgentCardHandler
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnMessage("-stream", OnStreamAsync);
        OnMessage("-multi", OnMultiTurnAsync);
        OnActivity(ActivityTypes.EndOfConversation, OnEndOfConversationAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnStreamAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        turnContext.StreamingResponse.EnableGeneratedByAILabel = true;
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Please wait while I process your request.", cancellationToken);
        turnContext.StreamingResponse.QueueTextChunk("a quick");
        await Task.Delay(600);
        turnContext.StreamingResponse.QueueTextChunk(" brown fox ");
        await Task.Delay(600);
        turnContext.StreamingResponse.QueueTextChunk("jumped over something[1]");
        await Task.Delay(600);

        turnContext.StreamingResponse.AddCitations([new Citation("1", "title", "https://example.com/fox-jump")]);
        await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
    }

    // Received an A2A Message
    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var multi = turnState.Conversation.GetValue<MultiResult>(nameof(MultiResult));
        if (multi != null)
        {
            await OnMultiTurnAsync(turnContext, turnState, cancellationToken);
            return;
        }

        // For A2A, simple one-shot message with no expectation of multi-turn should just
        // be sent as EOC in order to complete the A2A Task. Othewise, there is no way to
        // convey to A2A that the Task is complete.
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", inputHint: InputHints.IgnoringInput, cancellationToken: cancellationToken);
        var activity = new Activity()
        {
            Type = ActivityTypes.EndOfConversation,
        };
        await turnContext.SendActivityAsync(activity, cancellationToken: cancellationToken);
    }

    // Received for A2A "tasks/cancel"
    private Task OnEndOfConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // No need for conversation state anymore
        turnState.Conversation.ClearState();

        return Task.CompletedTask;
    }

    private async Task OnMultiTurnAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var multi = turnState.Conversation.GetValue(nameof(MultiResult), () => new MultiResult());
        multi.Messages.Add(turnContext.Activity.Text);

        if (turnContext.Activity.Text.Equals("end", System.StringComparison.OrdinalIgnoreCase))
        {
            var eoc = new Activity()
            {
                Type = ActivityTypes.EndOfConversation,
                Text = "Turn done", // optional
                Code = EndOfConversationCodes.CompletedSuccessfully,  // recommended, A2AAdapter will default to "completed"
                Value = multi  // optional result
            };
            await turnContext.SendActivityAsync(eoc, cancellationToken: cancellationToken);

            // No need for conversation state anymore
            turnState.Conversation.ClearState();
        }
        else
        {
            var activity = MessageFactory.Text($"You said: {turnContext.Activity.Text}", inputHint: InputHints.ExpectingInput);
            await turnContext.SendActivityAsync(activity, cancellationToken: cancellationToken);
        }
    }

    public Task<AgentCard> GetAgentCard(AgentCard initialCard)
    {
        initialCard.Name = "A2AAgent";
        initialCard.Description = "Demonstrates A2A functionality in Agent SDK";
        initialCard.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        return Task.FromResult(initialCard);
    }
}

class MultiResult
{
    public List<string> Messages { get; set; } = [];
}
