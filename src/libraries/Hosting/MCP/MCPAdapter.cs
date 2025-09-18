// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.MCP
{
    public class MCPAdapter : ChannelAdapter, IMCPHttpAdapter
    {
        private readonly ChannelResponseQueue _responseQueue;
        private readonly IActivityTaskQueue _activityTaskQueue;
        private readonly ILogger<MCPAdapter> _logger;

        public MCPAdapter(IActivityTaskQueue activityTaskQueue, ILogger<MCPAdapter> logger = null) : base(logger)
        {
            _logger = logger ?? NullLogger<MCPAdapter>.Instance;
            _activityTaskQueue = activityTaskQueue;
            _responseQueue = new ChannelResponseQueue(_logger);
        }

        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string routePattern, CancellationToken cancellationToken = default)
        {
            if (httpRequest.Method != HttpMethods.Post)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else
            {
                var rpcRequest = await MCPConverter.ReadRequestAsync<JsonRpcRequest>(httpRequest);

                if (rpcRequest.Method.Equals("initialize"))
                {
                    //TODO: forward to IAgent?
                    _logger.LogDebug("MCP: initialize");
                    await ProcessInitializeRequestAsync(rpcRequest, httpRequest, httpResponse, agent, routePattern, cancellationToken).ConfigureAwait(false);
                }
                else if (rpcRequest.Method.Equals("notifications/initialized"))
                {
                    _logger.LogDebug("MCP: notifications/initialized");
                }
                else if (rpcRequest.Method.Equals("tools/list"))
                {
                    //TODO: forward to AgentApplication
                    _logger.LogDebug("MCP: tools/list");

                    var inputSchema = MCPConverter.GetSchema(typeof(ChatMessage));

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
                    var json = MCPConverter.ToJson(rpcResponse);
                    await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
                    await httpResponse.Body.FlushAsync(cancellationToken);
                }
                else if (rpcRequest.Method.Equals("tools/call"))
                {
                    _logger.LogDebug("MCP: tools/call");

                    //TODO: verify request.Id

                    if (!httpRequest.Headers.TryGetValue("mcp-session-id", out var sessionId))
                    {
                        sessionId = Guid.NewGuid().ToString("N");
                    }

                    var activity = MCPConverter.CreateActivityFromRequest(rpcRequest, sessionId);

                    await ProcessStreamedAsync(activity, HttpHelper.GetClaimsIdentity(httpRequest), httpResponse, agent, new MCPStreamedResponseHandler(), cancellationToken);
                }
                else
                {
                    _logger.LogDebug("MCP: Unhandled `{RequestMethod}`", rpcRequest.Method);
                }
            }
        }

        private async Task ProcessStreamedAsync(IActivity activity, ClaimsIdentity identity, HttpResponse httpResponse, IAgent agent, MCPStreamedResponseHandler writer, CancellationToken cancellationToken = default)
        {
            if (activity == null || !activity.Validate([ValidationContext.Channel, ValidationContext.Receiver]) || activity.DeliveryMode != DeliveryModes.Stream)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            InvokeResponse invokeResponse = null;

            _responseQueue.StartHandlerForRequest(activity.RequestId);
            await writer.ResponseBegin(httpResponse, cancellationToken).ConfigureAwait(false);

            // Queue the activity to be processed by the ActivityBackgroundService, and stop SynchronousRequestHandler when the
            // turn is done.
            _activityTaskQueue.QueueBackgroundActivity(identity, this, activity, agentType: agent.GetType(), onComplete: (response) =>
            {
                invokeResponse = response;
                _responseQueue.CompleteHandlerForRequest(activity.RequestId);
                return Task.CompletedTask;
            });

            // block until turn is complete
            await _responseQueue.HandleResponsesAsync(activity.RequestId, async (activity) =>
            {
                await writer.OnResponse(httpResponse, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken: cancellationToken).ConfigureAwait(false);
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

            var json = MCPConverter.ToJson(rpcResponse);

            if (httpRequest.Headers.Accept.Contains("text/event-stream"))
            {
                httpResponse.ContentType = "text/event-stream";
                json = string.Format(MCPStreamedResponseHandler.MessageTemplate, json);
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
            await _responseQueue.SendActivitiesAsync(turnContext.Activity.RequestId, activities, cancellationToken);
            return [];
        }
    }
}
