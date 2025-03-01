using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.MCP.Server.Methods.Tools;
using Microsoft.Agents.MCP.Server.Methods.Logging;
using Microsoft.Agents.MCP.Server.Methods.Initialize;
using Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall;
using Microsoft.Agents.MCP.Server.Methods.Tools.ToolsList;
using Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Agents.MCP.Core.Handlers.SharedHandlers.Cancellation;
using Microsoft.Agents.MCP.Core.Handlers.SharedHandlers.Ping;
using Microsoft.Agents.MCP.Core.DependencyInjection;

namespace Microsoft.Agents.MCP.Server.DependencyInjection;

public static class ServiceConfigurationRegistrationExtensions
{
    public static void AddToolExecutor<T>(this IServiceCollection collection) where T : McpToolExecutorBase
    {
        collection.AddKeyedSingleton<IMcpToolExecutor, T>(ServiceCollectionToolExecutorFactory.DefaultToolKey);
    }

    public static void AddDefaultServerExecutors(this IServiceCollection collection)
    {
        collection.AddPayloadExecutor<InvokeToolHandler>();
        collection.AddPayloadExecutor<ListToolsHandler>();
        collection.AddPayloadExecutor<InitializeHandler>();
        collection.AddPayloadExecutor<SetLogLevelHandler>();
        collection.AddPayloadExecutor<InitializedNotificationHandler>();
        collection.AddPayloadExecutor<CancellationNotificationHandler>();
        collection.AddPayloadExecutor<PingHandler>();
    }


    public static void AddDefaultOperationFactory(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IOperationExecutorFactory, ServiceCollectionToolExecutorFactory>();
    }
}
