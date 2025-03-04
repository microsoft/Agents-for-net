using Microsoft.Agents.Mcp.Client.Methods.Logging;
using Microsoft.Agents.Mcp.Client.Methods.Roots;
using Microsoft.Agents.Mcp.Client.Methods.Sampling;
using Microsoft.Agents.Mcp.Core.Handlers.SharedHandlers.Cancellation;
using Microsoft.Agents.Mcp.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Mcp.Client.DependencyInjection;

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
