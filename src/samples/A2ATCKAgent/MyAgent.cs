// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.A2A;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace A2ATCKAgent;

[A2AExtension]
[A2ASkill("TCK", "tck")]
public partial class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        A2AExtension.OnMessage(OnA2AMessageAsync);
    }

    private async Task OnA2AMessageAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Reference: a2a-tck\sut\a2a-python\sut_agent.py

        var message = turnContext.Activity.ChannelData;

        if (message?.MessageId.StartsWith("tck-artifact-text") == true)
        {
            var taskUpdater = new TaskUpdater(turnContext.Client.EventQueue, message.TaskId!, message.ContextId!);
            await taskUpdater.AddArtifactAsync([Part.FromText("Generated text content")], cancellationToken: cancellationToken);
            await taskUpdater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (message?.MessageId.StartsWith("tck-artifact-file") == true)
        {
            var taskUpdater = new TaskUpdater(turnContext.Client.EventQueue, message.TaskId!, message.ContextId!);
            await taskUpdater.AddArtifactAsync([new Part { Raw = Encoding.UTF8.GetBytes("tck"), MediaType = "text/plain", Filename = "output.txt" }], cancellationToken: cancellationToken);
            await taskUpdater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync($"You sent an A2A message with text: '{turnContext.Activity.Text}'", cancellationToken: cancellationToken);
        }
    }
}
