namespace Microsoft.Agents.Mcp.Core.Abstractions;

public interface IMcpPayloadExecutorFactory
{
    IMcpPayloadHandler GetNotificationExecutor(string name);
    IMcpPayloadHandler GetMethodExecutor(string name);
}