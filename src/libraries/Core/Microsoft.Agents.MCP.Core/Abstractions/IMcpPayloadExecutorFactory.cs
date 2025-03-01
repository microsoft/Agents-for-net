namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpPayloadExecutorFactory
{
    IMcpPayloadHandler GetNotificationExecutor(string name);
    IMcpPayloadHandler GetMethodExecutor(string name);
}