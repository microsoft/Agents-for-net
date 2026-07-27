// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A;
using System.Threading;
using System.Threading.Tasks;

namespace A2AAgent;

[Agent(name: "MyAgent", description: "Agent with A2A Sample")]
[A2AExtension]
[AgentInterface(AgentTransportProtocol.ActivityProtocol, "/api/messages")]
[AgentInterface(A2AAgentTransportProtocol.JsonRpc, "/a2a")]
[AgentInterface(A2AAgentTransportProtocol.HttpJson, "/a2a")]
[A2ASkill(name: "Echo", description: "Echos messages back", tags: "a2a, sample, echo")]
[A2ASkill(name: "MultiTurn", description: "Simulate a multi-turn conversation.  Send -multi to start, end to stop", tags: "a2a, sample, multi-turn")]
[A2ASkill(name: "StreamingResponse", description: "Simulates a StreamingResponse.  Send -stream to start", tags: "a2a, sample, streaming-response")]
public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    private const string MultiTurnCountKey = "MultiTurnCount";

    [A2AMessageRoute("-stream")]
    private async Task OnStreamAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        turnContext.StreamingResponse.EnableGeneratedByAILabel = true;
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Please wait while I process your request.", cancellationToken);
        turnContext.StreamingResponse.QueueTextChunk("a quick");
        await Task.Delay(250);
        turnContext.StreamingResponse.QueueTextChunk(" brown fox ");
        await Task.Delay(250);
        turnContext.StreamingResponse.QueueTextChunk("jumped over something[1]");
        await Task.Delay(250);

        turnContext.StreamingResponse.AddCitations([new Citation("1", "title", "https://example.com/fox-jump")]);
        await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);

        var eoc = new Activity()
        {
            Type = ActivityTypes.EndOfConversation,
            Code = EndOfConversationCodes.CompletedSuccessfully,
        };
        await turnContext.SendActivityAsync(eoc, cancellationToken: cancellationToken);
    }

    // Received an A2A Message
    [A2AMessageRoute]
    private async Task OnMessageAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // ConversationState is associated with the A2A Task.
        var turnCount = turnState.Conversation.GetValue<int>(MultiTurnCountKey);
        if (turnCount > 0)
        {
            await OnMultiTurnAsync(turnContext, turnState, cancellationToken);
            return;
        }

        // SDK always creates an AgentTask in A2A. Simple one-shot message with no expectation of multi-turn should
        // just be sent as EOC with Activity.Text in order to complete the A2A Task. Othewise, there is no
        // way to convey to A2A that the Task is complete.
        var activity = new Activity()
        {
            Text = $"You said: {turnContext.Activity.Text}",
            Type = ActivityTypes.EndOfConversation,
        };
        await turnContext.SendActivityAsync(activity, cancellationToken: cancellationToken);
    }

    [A2AMessageRoute("-a2a")]
    private async Task OnA2ADirectAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var message = new A2A.Message()
        {
            Role = Role.Agent,
            TaskId = turnContext.Client.RequestContext.TaskId,
            ContextId = turnContext.Client.RequestContext.ContextId,
            Parts = [new Part() { Text = "This is an A2A message" }]
        };

        await turnContext.Client.EventQueue.EnqueueMessageAsync(message, cancellationToken);
    }

    // Received for A2A "tasks/cancel"
    [EndOfConversationRoute]
    private Task OnEndOfConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // No need for conversation state anymore
        turnState.Conversation.ClearState();
        return Task.CompletedTask;
    }

    [A2AMessageRoute("-multi")]
    private async Task OnMultiTurnAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var turnCount = turnState.Conversation.GetValue<int>(MultiTurnCountKey) + 1;
        turnState.Conversation.SetValue(MultiTurnCountKey, turnCount);

        if (turnContext.Activity.Text.Equals("end", System.StringComparison.OrdinalIgnoreCase))
        {
            // Send EOC to complete the A2A Task.
            var eoc = new Activity()
            {
                Type = ActivityTypes.EndOfConversation,
                Text = $"All done after {turnCount} turns.",
                Code = EndOfConversationCodes.CompletedSuccessfully,  // recommended, A2AAdapter will default to "completed"
                Value = new { turnCount }
            };

            await turnContext.SendActivityAsync(eoc, cancellationToken: cancellationToken);

            // No need for conversation state anymore
            turnState.Conversation.ClearState();
        }
        else
        {
            // Hosting.A2A requires ExpectingInput for multi-turn. 
            var activity = MessageFactory.Text($"You said: {turnContext.Activity.Text} (turn {turnCount})", inputHint: InputHints.ExpectingInput);
            await turnContext.SendActivityAsync(activity, cancellationToken: cancellationToken);
        }
    }
}
