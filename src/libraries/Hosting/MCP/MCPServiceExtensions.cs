// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.Hosting.AspNetCore;

namespace Microsoft.Agents.Hosting.MCP
{
    public static class MCPServiceExtensions
    {
        public static void AddMCPAdapter(this IServiceCollection services)
        {
            services.AddAsyncAdapterSupport();

            services.AddSingleton<MCPAdapter>();
            services.AddSingleton<IMCPHttpAdapter>(sp => sp.GetService<MCPAdapter>());
        }

        public static IEndpointConventionBuilder MapMCP(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string pattern = "/mcp")
        {
            var mcpGroup = endpoints.MapGroup(pattern);
            if (requireAuth)
            {
                mcpGroup.RequireAuthorization();
            }
            else
            {
                mcpGroup.AllowAnonymous();
            }

            mcpGroup.MapPost(
                "",
                async (HttpRequest request, HttpResponse response, IMCPHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
                {
                    await adapter.ProcessAsync(request, response, agent, pattern, cancellationToken).ConfigureAwait(false);
                })
                    .WithMetadata(new AcceptsMetadata(["application/json"]))
                    .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
                    .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

            return mcpGroup;
        }
    }
}
