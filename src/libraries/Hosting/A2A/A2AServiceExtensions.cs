// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.Protocol;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.Hosting.A2A.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using System.Text;
using System.Threading;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Hosting.A2A
{
    public static class A2AServiceExtensions
    {
        public static IEndpointConventionBuilder MapA2A(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern = "/a2a")
        {
            var a2aGroup = endpoints.MapGroup(pattern);

            var streamGroup = a2aGroup.MapGroup("")
                .WithDisplayName(b => $"A2A SSE HTTP | {b.DisplayName}")
                .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status404NotFound, typeof(JsonRpcError), contentTypes: ["application/json"]));

            streamGroup.MapPost(
                "/messages",
                async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
                {
                    var jsonRpcRequest = await A2AProtocolConverter.ReadRequestAsync<JsonRpcRequest>(request);

                    if (jsonRpcRequest.Method.Equals("message/stream"))
                    {
                        var (activity, contextId, taskId) = A2AProtocolConverter.CreateActivityFromRequest(jsonRpcRequest, isStreaming: true);
                        await adapter.ProcessAsync(activity, HttpHelper.GetIdentity(request), response, agent, new A2AStreamedResponseWriter(jsonRpcRequest.Id.ToString(), contextId, taskId), cancellationToken);
                    }
                })
                    .WithMetadata(new AcceptsMetadata(["application/json"]))
                    .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
                    .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

            // AgentCard
            a2aGroup.MapGet("/.well-known/agent.json", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
            {
                System.Diagnostics.Trace.WriteLine("/.well-known/agent.json");

                var agentCard = new AgentCard()
                {
                    Name = "EmptyAgent",
                    Description = "Simple Echo Agent",
                    Version = "0.2.0",
                    Url = $"{request.Scheme}://{request.Host.Value}{pattern}/messages",
                    DefaultInputModes = [],
                    DefaultOutputModes = [],
                    Skills = [],
                    Capabilities = new AgentCapabilities()
                    {
                        Streaming = true,
                    }
                };

                response.ContentType = "application/json";
                await response.Body.WriteAsync(Encoding.UTF8.GetBytes(A2AProtocolConverter.ToJson(agentCard)), cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            })
                .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(JsonRpcError), contentTypes: ["application/json"]));

            return a2aGroup;
        }
    }
}
