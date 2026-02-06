// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class EndpointExtensions
    {
        // TODO: need to support more than a single IProactiveAgent
        public static IEndpointConventionBuilder MapProactive(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string pattern = "/proactive") 
        {
            var routeGroup = endpoints.MapGroup(pattern);
            if (requireAuth)
            {
                routeGroup.RequireAuthorization();
            }
            else
            {
                routeGroup.AllowAnonymous();
            }

            routeGroup.MapPost(
                "/sendMessage",
                async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IProactiveAgent agent, CancellationToken cancellationToken) =>
                {
                    // TODO: call IProactiveAgent
                    // request -> IProactiveAgent -> AgentApplication.Proactive.SendActivity
                })
                .WithMetadata(new AcceptsMetadata(["application/json"]));

            routeGroup.MapPost(
                "/createConversation",
                async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IProactiveAgent agent, CancellationToken cancellationToken) =>
                {
                    // TODO: call IProactiveAgent
                    // request -> IProactiveAgent -> AgentApplication.Proactive.CreateConversation
                })
                .WithMetadata(new AcceptsMetadata(["application/json"]));

            return routeGroup;
        }
    }
}