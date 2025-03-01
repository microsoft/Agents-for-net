using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.ClientMethods.Roots;


public class RootsResult
{
    public static readonly RootsResult Instance = new();
}