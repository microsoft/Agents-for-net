// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    public interface IAgentCardHandler
    {
        Task<AgentCard> GetAgentCard(AgentCard initialCard);
    }
}
