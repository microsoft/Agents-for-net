using Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Agents.MCP.Server.Methods.Tools.ToolsList;
using System.Collections.Immutable;

namespace Microsoft.Agents.MCP.Server.Methods.Tools;

public interface IOperationExecutorFactory
{
    ImmutableArray<ToolDefinition> GetDefinitions();
    IMcpToolExecutor GetExecutor(string name);
}
