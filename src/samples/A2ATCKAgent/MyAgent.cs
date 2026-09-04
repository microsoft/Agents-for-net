// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.A2A;
using System.Text;
using System.Text.Json;
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

        var inMessage = turnContext.Activity.ChannelData;
        var updater = new TaskUpdater(turnContext.Client.EventQueue, inMessage.TaskId!, inMessage.ContextId!);

        if (inMessage?.MessageId.StartsWith("tck-artifact-text") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([Part.FromText("Generated text content")], cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-artifact-file-url") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([new Part { Url = "https://example.com/output.txt", MediaType = "text/plain", Filename = "output.txt" }], cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-artifact-file") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([new Part { Raw = Encoding.UTF8.GetBytes("tck"), MediaType = "text/plain", Filename = "output.txt" }], cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-artifact-data") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([new Part { Data = JsonSerializer.Deserialize<JsonElement>("{\"key\": \"value\", \"count\": 42}") }], cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-stream-artifact-chunked") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([Part.FromText("chunk-1 ")], append: true, cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([Part.FromText("chunk-2")], append: true, lastChunk: true, cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-input-required") == true)
        {
            await updater.RequireInputAsync(null!, cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-complete-task") == true)
        {
            await updater.CompleteAsync(updater.NewAgentMessage("Hello from TCK"), cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-message-response") == true)
        {
            await turnContext.Client.EventQueue.EnqueueMessageAsync(updater.NewAgentMessage("Direct message response"), cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-stream-ordering-001") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([Part.FromText("Ordered output")], cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-stream-001") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([Part.FromText("Stream hello from TCK")], cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-stream-002") == true)
        {
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else if (inMessage?.MessageId.StartsWith("tck-stream-003") == true)
        {
            await updater.StartWorkAsync(cancellationToken: cancellationToken);
            await updater.AddArtifactAsync([Part.FromText("Stream task lifecycle")], cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }
        else
        {
            await turnContext.Client.EventQueue.EnqueueMessageAsync(updater.NewAgentMessage($"Unhandled messageId prefix: {inMessage!.MessageId}"), cancellationToken);
        }
    }
}
