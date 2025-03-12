using Microsoft.Agents.Mcp.Core;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Session;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Mcp.Core.DependencyInjection;

public static class ServiceConfigurationRegistrationExtensions
{
    public static void AddModelContextProtocolHandlers(this IServiceCollection collection)
    {
        collection.AddSingleton<IMcpProcessor, McpProcessor>();
        collection.AddSingleton<IMcpHandler, McpHandler>();
        collection.AddSingleton<IMcpPayloadFactory, McpPayloadFactory>();
    }

    public static void AddPayloadExecutor<T>(this IServiceCollection collection) where T : McpPayloadHandlerBase
    {
        collection.AddKeyedSingleton<IMcpPayloadHandler, T>(ServiceCollectionPayloadExecutorFactory.DefaultKey);
    }

    public static void AddDefaultPayloadExecutionFactory(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMcpPayloadExecutorFactory, ServiceCollectionPayloadExecutorFactory>();
    }

    public static void AddDefaultPayloadResolver(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMcpPayloadResolver, ServiceCollectionPayloadExecutorFactory>();
    }

    public static void AddMemorySessionManager(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMcpSessionManager, MemoryMcpSessionManager>();
    }
    public static void AddTransportManager(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ITransportManager, TransportManager>();
    }
}
