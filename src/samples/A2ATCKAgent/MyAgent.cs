// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.A2A;
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

    private Task OnA2AMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        return turnContext.SendActivityAsync($"You sent an A2A message with text: '{turnContext.Activity.Text}'", cancellationToken: cancellationToken);
    }
}
