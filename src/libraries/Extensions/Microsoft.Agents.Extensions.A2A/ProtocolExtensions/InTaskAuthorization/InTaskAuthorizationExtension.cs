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
    /// The header carrying the task-scoped bearer credential.
    /// </summary>
    public const string CredentialHeader = "A2A-InTask-Authorization";

    internal static string GetStateKey(string handlerName, string taskId)
        => $"a2a/inTaskAuthorization/{handlerName}/{taskId}";

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
                credentialHeader = CredentialHeader,
            }),
        };
    }
}
