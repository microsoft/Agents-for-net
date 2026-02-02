// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore.A2A;
using System.Reflection;
using System.Threading.Tasks;

namespace A2AAgent;

public class MyAgent : AgentApplication, IAgentCardHandler
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
    }

    public Task<AgentCard> GetAgentCard(AgentCard initialCard)
    {
        initialCard.Name = "A2ATCKAgent";
        initialCard.Description = "Used when running the A2A TCK";
        initialCard.Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        return Task.FromResult(initialCard);
    }
}
