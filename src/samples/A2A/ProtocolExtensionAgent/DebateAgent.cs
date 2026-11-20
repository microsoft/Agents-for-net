// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A;
using System.Threading;
using System.Threading.Tasks;

namespace ProtocolExtensionAgent;

[Agent(name: "Protocol extension sample", description: "An echo agent with an optional, contrived debate extension.")]
[AgentInterface(A2AAgentTransportProtocol.JsonRpc, "/a2a")]
[AgentInterface(A2AAgentTransportProtocol.HttpJson, "/a2a")]
[A2AExtension]
public partial class DebateAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [A2ASkill(name: "Echo or debate", description: "Echoes a message, or playfully disagrees when the debate extension is active.", tags: "sample, debate")]
    private async Task OnMessageAsync(
        IA2ATurnContext context, ITurnState state, CancellationToken cancellationToken)
    {
        var active = context.Client.RequestContext.ExtensionActivation?
            .ActivatedExtensions.Contains(DebateExtension.ExtensionUri) == true;
        await context.SendActivityAsync(new Activity
        {
            Type = ActivityTypes.EndOfConversation,
            Text = active
                ? $"Debate mode: I respectfully disagree that {context.Activity.Text}."
                : $"You said: {context.Activity.Text}",
        }, cancellationToken: cancellationToken);
    }
}
