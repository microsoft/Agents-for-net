// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2AProtocolAgentCard = A2A.AgentCard;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.AgentCard;

/// <summary>
/// Implement to customize agent-specific <c>AgentCard</c> properties.
/// </summary>
public interface IAgentCardHandler
{
    /// <summary>
    /// Customizes the final <c>AgentCard</c> before it is returned to an A2A client.
    /// </summary>
    /// <remarks>
    /// The extension composes protocol-required values before this handler runs. Customize the
    /// final card rather than replacing required capabilities or authorization metadata.
    /// </remarks>
    /// <param name="hostAgentCard">The A2A Host will create an AgentCard with proper values for most properties.<br/>
    /// AgentApplications will likely want to set: <c>hostAgentCard.Name</c>, <c>hostAgentCard.Description</c>, 
    /// <c>hostAgentCard.Version</c>, and <c>hostAgentCard.Skills</c>.
    /// <para>Changing other properties, specifically around auth and capabilities, should be avoided without guidance
    /// on supported values.</para>
    /// </param>
    /// <returns>The final Agent Card to return to the client.</returns>
    Task<A2AProtocolAgentCard> GetAgentCard(A2AProtocolAgentCard hostAgentCard);
}
