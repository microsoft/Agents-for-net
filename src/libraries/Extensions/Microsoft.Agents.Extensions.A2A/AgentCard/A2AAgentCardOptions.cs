// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.A2A.AgentCard;

/// <summary>
/// Application-owned descriptive values used to compose an A2A Agent Card.
/// </summary>
public sealed class A2AAgentCardOptions
{
    /// <summary>
    /// Gets or sets the agent name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the agent description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the URL of the agent documentation.
    /// </summary>
    public string DocumentationUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL of the agent icon.
    /// </summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the organization that provides the agent.
    /// </summary>
    public AgentProvider Provider { get; set; }

    /// <summary>
    /// Gets the reusable Agent Card security schemes by name.
    /// </summary>
    public IDictionary<string, SecurityScheme> SecuritySchemes { get; } =
        new Dictionary<string, SecurityScheme>();
}
