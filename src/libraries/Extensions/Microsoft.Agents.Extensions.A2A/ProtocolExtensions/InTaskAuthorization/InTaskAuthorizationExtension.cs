// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;

/// <summary>
/// Defines the Agents SDK A2A in-task authorization protocol extension.
/// </summary>
internal sealed class InTaskAuthorizationExtension : IA2AProtocolExtension
{
    /// <summary>
    /// The versioned extension identifier.
    /// </summary>
    public const string Uri = "https://schemas.microsoft.com/agents/a2a/extensions/in-task-authorization/v1";

    /// <summary>
    /// The JSON-RPC operation used to resume an interrupted authorization flow.
    /// </summary>
    public const string ResumeAuthOperation = "resumeAuth";

    /// <summary>
    /// The Activity event name used to route a resume request through AgentApplication.
    /// </summary>
    public const string ResumeAuthEventName = Uri + "/resumeAuth";

    /// <summary>
    /// The request header carrying the delegated access token for a resume request.
    /// </summary>
    public const string TokenHeader = "x-a2a-intask-authorization";

    string IA2AProtocolExtension.Uri => Uri;

    AgentExtension IA2AProtocolExtension.CreateAgentCardExtension()
    {
        return new AgentExtension
        {
            Uri = Uri,
            Description = "Resolves task-scoped OAuth authorization requests.",
            Required = false,
            Params = JsonSerializer.SerializeToElement(new
            {
                operations = new
                {
                    jsonRpc = ResumeAuthOperation,
                    httpJson = "POST /tasks/{taskId}:resumeAuth",
                },
            }),
        };
    }
}
