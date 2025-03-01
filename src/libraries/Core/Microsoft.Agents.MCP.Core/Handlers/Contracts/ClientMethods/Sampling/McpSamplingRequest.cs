using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.ClientMethods.Sampling;

public class McpSamplingRequest : McpRequest<SamplingParameters>
{
    public static readonly string MethodName = "sampling/createMessage";

    [SetsRequiredMembers]
    public McpSamplingRequest(SamplingParameters parameters)
    {
        Method = MethodName;
        Parameters = parameters;
    }
}

public class SamplingParameters
{
    public string? SystemPrompt { get; set; }
}