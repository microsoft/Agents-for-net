// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Text;
using System.Linq;
using System.Text.Json;
using System;
using Newtonsoft.Json.Schema.Generation;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.Hosting.MCP
{
    public class MCPAdapter(IActivityTaskQueue activityTaskQueue) : ChannelAdapter, IMCPHttpAdapter
    {
        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string routePattern, CancellationToken cancellationToken = default)
        {
            if (httpRequest.Method != HttpMethods.Post)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else
            {
                var rpcRequest = await MCPProtocolConverter.ReadRequestAsync<JsonRpcRequest>(httpRequest);

                if (rpcRequest.Method.Equals("initialize"))
                {
                    //TODO: forward to IAgent?
                    System.Diagnostics.Trace.WriteLine("MCP: initialize");
                    await ProcessInitializeRequestAsync(rpcRequest, httpRequest, httpResponse, agent, routePattern, cancellationToken).ConfigureAwait(false);
                }
                else if (rpcRequest.Method.Equals("notifications/initialized"))
                {
                    System.Diagnostics.Trace.WriteLine("MCP: notifications/initialized");
                }
                else if (rpcRequest.Method.Equals("tools/list"))
                {
                    //TODO: forward to IAgent?
                    System.Diagnostics.Trace.WriteLine("MCP: tools/list");

                    JSchemaGenerator generator = new();
                    var inputSchema = JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<object>(generator.Generate(typeof(ChatMessage)).ToString()));

                    var tools = new ListToolsResult()
                    {
                        Tools = [
                            new Tool()
                            {
                                Name = "message",
                                InputSchema = inputSchema,
                            }
                        ]
                    };

                    var rpcResponse = new JsonRpcResponse()
                    {
                        Id = rpcRequest.Id,
                        Result = JsonSerializer.SerializeToNode(tools)
                    };

                    httpResponse.ContentType = "application/json";
                    var json = MCPProtocolConverter.ToJson(rpcResponse);
                    await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
                    await httpResponse.Body.FlushAsync(cancellationToken);
                }
                else if (rpcRequest.Method.Equals("tools/call"))
                {
                    System.Diagnostics.Trace.WriteLine("MCP: tools/call");

                    //TODO: verify request.Id

                    if (!httpRequest.Headers.TryGetValue("mcp-session-id", out var sessionId))
                    {
                        sessionId = Guid.NewGuid().ToString("N");
                    }

                    var activity = MCPProtocolConverter.CreateActivityFromRequest(rpcRequest, sessionId);

                    await ProcessStreamedAsync(activity, HttpHelper.GetIdentity(httpRequest), httpResponse, agent, new MCPStreamedResponseWriter(), cancellationToken);
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"MCP: {rpcRequest.Method}");
                }
            }
        }

        private async Task ProcessStreamedAsync(IActivity activity, ClaimsIdentity identity, HttpResponse httpResponse, IAgent agent, MCPStreamedResponseWriter writer, CancellationToken cancellationToken = default)
        {
            if (activity == null || !activity.Validate([ValidationContext.Channel, ValidationContext.Receiver]) || activity.DeliveryMode != DeliveryModes.Stream)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            InvokeResponse invokeResponse = null;

            // Queue the activity to be processed by the ActivityBackgroundService, and stop SynchronousRequestHandler when the
            // turn is done.
            activityTaskQueue.QueueBackgroundActivity(identity, activity, onComplete: (response) =>
            {
                StreamedResponseHandler.CompleteHandlerForConversation(activity.Conversation.Id);
                invokeResponse = response;
            });

            await writer.StreamBegin(httpResponse).ConfigureAwait(false);

            // block until turn is complete
            await StreamedResponseHandler.HandleResponsesAsync(activity.Conversation.Id, async (activity) =>
            {
                await writer.WriteActivity(httpResponse, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            await writer.StreamEnd(httpResponse, invokeResponse, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static async Task ProcessInitializeRequestAsync(JsonRpcRequest rpcRequest, HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string messagePrefix, CancellationToken cancellationToken = default)
        {
            var result = new InitializeResult()
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ServerCapabilities()
                {

                },
                ServerInfo = new Implementation()
                {
                    Name = "EmptyAgent",
                    Version = "1.0.0",
                }
            };

            var rpcResponse = new JsonRpcResponse()
            {
                Id = rpcRequest.Id,
                Result = JsonSerializer.SerializeToNode(result)
            };

            var json = MCPProtocolConverter.ToJson(rpcResponse);

            if (httpRequest.Headers.Accept.Contains("text/event-stream"))
            {
                httpResponse.ContentType = "text/event-stream";
                json = string.Format(MCPStreamedResponseWriter.MessageTemplate, json);
            }
            else
            {
                httpResponse.ContentType = "application/json";
            }

            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            await StreamedResponseHandler.SendActivitiesAsync(turnContext.Activity.Conversation.Id, activities, cancellationToken);
            return [];
        }
    }
}
