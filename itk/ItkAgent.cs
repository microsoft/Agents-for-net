// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using AgentsA2AItk.Protocol;
using Google.Protobuf;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Extensions.A2A.AgentCard;

namespace AgentsA2AItk;

[Agent(name: "Agents SDK ITK", description: "Local A2A interoperability test agent")]
[A2AExtension]
[AgentInterface(A2AAgentTransportProtocol.JsonRpc, "/jsonrpc")]
[AgentInterface(A2AAgentTransportProtocol.HttpJson, "/a2a")]
public partial class ItkAgent : AgentApplication, IAgentCardHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ItkAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory) : base(options)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<AgentCard> GetAgentCard(AgentCard agentCard)
    {
        // The harness is anonymous; advertising a bearer scheme would imply a security boundary it lacks.
        agentCard.SecuritySchemes?.Clear();
        agentCard.Capabilities.Streaming = false;
        agentCard.DefaultInputModes = ["text/plain", "application/x-protobuf"];
        agentCard.DefaultOutputModes = ["text/plain"];
        return Task.FromResult(agentCard);
    }

    [A2ASkill(name: "ITK traversal", description: "Executes local ITK send_message instructions", tags: "itk, acts, test")]
    private async Task OnMessageAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string response;
        if (turnContext.Activity.Text?.StartsWith("tck-complete-task", StringComparison.Ordinal) == true)
        {
            response = turnContext.Activity.Text;
        }
        else
        {
            var message = (turnContext.Activity as IA2AActivity)?.ChannelData;
            var instructionBytes = message?.Parts.FirstOrDefault(part =>
                part.MediaType == "application/x-protobuf" && part.Raw is not null)?.Raw;
            if (instructionBytes is null)
            {
                throw new InvalidOperationException("Expected an ITK application/x-protobuf instruction.");
            }

            var instruction = Instruction.Parser.ParseFrom(instructionBytes);
            response = await ExecuteAsync(instruction, cancellationToken).ConfigureAwait(false);
        }

        await turnContext.SendActivityAsync(new Activity
        {
            Type = ActivityTypes.EndOfConversation,
            Code = EndOfConversationCodes.CompletedSuccessfully,
            Text = response,
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ExecuteAsync(Instruction instruction, CancellationToken cancellationToken)
    {
        switch (instruction.StepCase)
        {
            case Instruction.StepOneofCase.ReturnResponse:
                if (instruction.ReturnResponse.HoldTask)
                {
                    throw new NotSupportedException("ITK hold-task/resubscribe scenarios are not selected.");
                }
                return instruction.ReturnResponse.Response;

            case Instruction.StepOneofCase.Steps:
                var results = new List<string>();
                foreach (var step in instruction.Steps.Instructions)
                {
                    results.Add(await ExecuteAsync(step, cancellationToken).ConfigureAwait(false));
                }
                return string.Concat(results);

            case Instruction.StepOneofCase.CallAgent:
                return await CallAgentAsync(instruction.CallAgent, cancellationToken).ConfigureAwait(false);

            default:
                throw new InvalidOperationException("Unknown ITK instruction step.");
        }
    }

    private async Task<string> CallAgentAsync(CallAgent call, CancellationToken cancellationToken)
    {
        if (call.BehaviorCase != CallAgent.BehaviorOneofCase.SendMessage || call.Streaming)
        {
            throw new NotSupportedException("Only non-streaming send_message is selected for this integration harness.");
        }

        var binding = call.Transport.ToUpperInvariant() switch
        {
            "JSONRPC" => ProtocolBindingNames.JsonRpc,
            "HTTP_JSON" or "HTTP+JSON" => ProtocolBindingNames.HttpJson,
            _ => throw new NotSupportedException($"Unsupported ITK transport: {call.Transport}"),
        };

        if (call.Instruction is null)
        {
            throw new InvalidOperationException("ITK call_agent has no nested instruction.");
        }

        var cardUri = new Uri(call.AgentCardUri, UriKind.Absolute);
        if (!LoopbackHttpHandler.IsAllowed(cardUri))
        {
            throw new InvalidOperationException("ITK peer Agent Card URI must be a loopback HTTP endpoint.");
        }

        var httpClient = _httpClientFactory.CreateClient("itk-peers");
        var resolver = new A2ACardResolver(cardUri, httpClient);
        var card = await resolver.GetAgentCardAsync(cancellationToken).ConfigureAwait(false);
        var client = A2AClientFactory.Create(card, httpClient, new A2AClientOptions
        {
            PreferredBindings = [binding],
        });

        var request = new SendMessageRequest
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [new Part
                {
                    Raw = call.Instruction.ToByteArray(),
                    MediaType = "application/x-protobuf",
                    Filename = "instruction.bin",
                }],
            },
        };
        var result = await client.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
        var reply = result.Message ?? result.Task?.Status?.Message ??
            result.Task?.History?.LastOrDefault(message => message.Role == Role.Agent);
        if (reply is null)
        {
            throw new InvalidOperationException("Peer returned no agent message.");
        }

        return string.Concat(reply.Parts.Select(part => part.Text));
    }
}
