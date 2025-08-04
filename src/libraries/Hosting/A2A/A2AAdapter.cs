// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    public class A2AAdapter : ChannelAdapter, IA2AHttpAdapter
    {
        private readonly TaskStore _taskStore;
        private readonly IActivityTaskQueue _activityTaskQueue;
        private readonly ChannelResponseQueue _responseQueue;
        private readonly ILogger<A2AAdapter> _logger;

        public A2AAdapter(IActivityTaskQueue activityTaskQueue, IStorage storage, ILogger<A2AAdapter> logger = null) : base(logger)
        {
            _logger = logger ?? NullLogger<A2AAdapter>.Instance;
            _activityTaskQueue = activityTaskQueue;
            _taskStore = new TaskStore(storage);
            _responseQueue = new ChannelResponseQueue(_logger);
        }

        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
        {
            var jsonRpcRequest = await A2AConverter.ReadRequestAsync<JsonRpcRequest>(httpRequest);

            if (jsonRpcRequest == null)
            {
                JsonRpcResponse response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.ParseError, "Missing JsonRpcRequest");
                await A2AConverter.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (httpRequest.Method == HttpMethods.Get)
            {
                // TBD: Do these ever arrive via GET?  The A2A CLI uses POST for all requests.

                if (jsonRpcRequest.Method.Equals(A2AMethods.TasksGet))
                {
                    await ProcessTaskGetAsync(jsonRpcRequest, httpResponse, false, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    JsonRpcResponse response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.MethodNotFound, $"{jsonRpcRequest.Method} not supported");
                    await A2AConverter.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                if (jsonRpcRequest.Method.Equals(A2AMethods.MessageStream) || jsonRpcRequest.Method.Equals(A2AMethods.TasksResubscribe))
                {
                    var (activity, contextId, taskId, message) = A2AConverter.ActivityFromRequest(jsonRpcRequest, isStreaming: true);

                    // Turn Begin
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Turn Begin: {Request}", A2AConverter.ToJson(jsonRpcRequest));
                    }

                    // Record incoming Message for the Task
                    if (activity.IsType(ActivityTypes.Message) && message != null)
                    {
                        await _taskStore.CreateOrUpdateTaskAsync(contextId, taskId, TaskState.Unknown, cancellationToken).ConfigureAwait(false);
                        await _taskStore.UpdateTaskAsync(message, cancellationToken).ConfigureAwait(false);
                    }

                    await ProcessMessageStreamAsync(
                        activity,
                        HttpHelper.GetClaimsIdentity(httpRequest),
                        httpResponse,
                        agent,
                        new A2AStreamedResponseWriter(_taskStore, jsonRpcRequest.Id.ToString(), contextId, taskId, _logger),
                        cancellationToken).ConfigureAwait(false);

                    // Turn done
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Turn End: RequestId={RequestId}", activity.RequestId);
                    }
                }
                else if (jsonRpcRequest.Method.Equals(A2AMethods.TasksGet))
                {
                    await ProcessTaskGetAsync(jsonRpcRequest, httpResponse, false, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    JsonRpcResponse response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.MethodNotFound, $"{jsonRpcRequest.Method} not supported");
                    await A2AConverter.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string messagePrefix, CancellationToken cancellationToken = default)
        {
            //TODO: Most likely pass to AgentApplication to determine card contents
            var agentCard = new AgentCard()
            {
                Name = "EmptyAgent",
                Description = "Simple Echo Agent",
                Version = "0.2.0",
                Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{messagePrefix}/",
                SecuritySchemes = new Dictionary<string, SecurityScheme>
                {
                    {
                        "jwt",
                        new HTTPAuthSecurityScheme() { Scheme = "bearer" }
                    }
                },
                DefaultInputModes = [],
                DefaultOutputModes = [],
                Skills = [],
                Capabilities = new AgentCapabilities()
                {
                    Streaming = true,
                }
            };

            httpResponse.ContentType = "application/json";
            var json = A2AConverter.ToJson(agentCard);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            var context = new TurnContext(this, activity, claimsIdentity);
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
            return null;
        }

        private async Task ProcessMessageStreamAsync(IActivity activity, ClaimsIdentity identity, HttpResponse httpResponse, IAgent agent, A2AStreamedResponseWriter writer, CancellationToken cancellationToken = default)
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
                await writer.WriteActivity(httpResponse, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task ProcessTaskGetAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, bool streamed, CancellationToken cancellationToken)
        {
            JsonRpcResponse response;

            try
            {
                var queryParams = A2AConverter.ReadParams<TaskQueryParams>(jsonRpcRequest);
                var task = await _taskStore.GetTaskAsync(queryParams.Id, cancellationToken).ConfigureAwait(false);

                response = A2AConverter.CreateResponse(jsonRpcRequest, task);
            }
            catch (KeyNotFoundException)
            {
                response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.TaskNotFoundError, "Task not found.");
            }

            await A2AConverter.WriteResponseAsync(httpResponse, response, streamed, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            await _responseQueue.SendActivitiesAsync(turnContext.Activity.RequestId, activities, cancellationToken);
            return [];
        }
    }
}
