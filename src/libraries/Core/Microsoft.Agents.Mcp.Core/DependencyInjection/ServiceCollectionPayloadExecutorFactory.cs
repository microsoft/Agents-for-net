using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Text.Json;

namespace Microsoft.Agents.Mcp.Core.DependencyInjection;

public class ServiceCollectionPayloadExecutorFactory : IMcpPayloadExecutorFactory, IMcpPayloadResolver
{
    internal static string DefaultKey = nameof(DefaultKey);
    private ImmutableDictionary<string, IMcpPayloadHandler> _dictionary;

    public ServiceCollectionPayloadExecutorFactory(IServiceProvider serviceProvider, ILogger<ServiceCollectionPayloadExecutorFactory> logger)
    {
        var services = serviceProvider.GetKeyedServices<IMcpPayloadHandler>(DefaultKey);
        var dictionary = new Dictionary<string, IMcpPayloadHandler>();
        foreach (var service in services)
        {
            if (!dictionary.TryAdd(service.Method, service))
            {
                logger.LogWarning("Duplicate service for method {Method}", service.Method);
            }
        }
        _dictionary = dictionary.ToImmutableDictionary();
    }

    public McpPayload CreateMethodRequestPayload(string? id, string method, JsonElement? parameters)
    {
        if (_dictionary.TryGetValue(method, out var handler))
        {
            var result = handler.CreatePayload(id, method, parameters);
            if(result is not McpRequest request)
            {
                throw new InvalidOperationException($"Handler for method {method} did not return a request payload");
            }

            return request;
        }

        throw new InvalidOperationException($"Unknown method");
    }

    public McpPayload CreateNotificationPayload(string method, JsonElement? parameters)
    {
        if (_dictionary.TryGetValue(method, out var handler))
        {
            var result = handler.CreatePayload(null, method, parameters);
            if (result is not McpNotification notification)
            {
                throw new InvalidOperationException($"Handler for method {method} did not return a notification payload");
            }

            return notification;
        }

        throw new InvalidOperationException($"Unknown method");
    }

    public IMcpPayloadHandler GetMethodExecutor(string name) => _dictionary[name];

    public IMcpPayloadHandler GetNotificationExecutor(string name) => _dictionary[name];
}