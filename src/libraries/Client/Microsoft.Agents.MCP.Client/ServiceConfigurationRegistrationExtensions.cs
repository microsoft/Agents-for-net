using Microsoft.Agents.MCP.Client.Methods.Logging;
using Microsoft.Agents.MCP.Client.Methods.Roots;
using Microsoft.Agents.MCP.Client.Methods.Sampling;
using Microsoft.Agents.MCP.Core.Handlers.SharedHandlers.Cancellation;
using Microsoft.Agents.MCP.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.MCP.Client.DependencyInjection;

public static class ServiceConfigurationRegistrationExtensions
{
    public static void AddDefaultClientExecutors(this IServiceCollection collection)
    {
        collection.AddPayloadExecutor<LogNotificationHandler>();
        collection.AddPayloadExecutor<RootsHandler>();
        collection.AddPayloadExecutor<SamplingHandler>();
        collection.AddPayloadExecutor<CancellationNotificationHandler>();
        collection.AddPayloadExecutor<RootsHandler>();
    }
}
