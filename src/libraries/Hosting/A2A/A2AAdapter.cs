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
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

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
        try
        {
            var jsonRpcRequest = await A2AResponseHandler.ReadRequestAsync<JsonRpcRequest>(httpRequest);

            // Turn Begin
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Turn Begin: {Request}", A2AConverter.ToJson(jsonRpcRequest));
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
                    JsonRpcError response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.MethodNotFound, $"{jsonRpcRequest.Method} not supported");
                    await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                if (   jsonRpcRequest.Method.Equals(A2AMethods.MessageStream) 
                    || jsonRpcRequest.Method.Equals(A2AMethods.TasksResubscribe) 
                    || jsonRpcRequest.Method.Equals(A2AMethods.MessageSend))
                {
                    var identity = HttpHelper.GetClaimsIdentity(httpRequest);

                    await ProcessMessageAsync(
                        jsonRpcRequest,
                        httpResponse,
                        identity,
                        agent,
                        cancellationToken).ConfigureAwait(false);
                }
                else if (jsonRpcRequest.Method.Equals(A2AMethods.TasksGet))
                {
                    await ProcessTaskGetAsync(jsonRpcRequest, httpResponse, false, cancellationToken).ConfigureAwait(false);
                }
                else if (jsonRpcRequest.Method.Equals(A2AMethods.TasksCancel))
                {
                    // TODO
                    JsonRpcError response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.MethodNotFound, $"{jsonRpcRequest.Method} not supported");
                    await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
                }
                else if (string.IsNullOrWhiteSpace(jsonRpcRequest.Method))
                {
                    JsonRpcError response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.InvalidRequest, $"{jsonRpcRequest.Method} not supported");
                    await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    JsonRpcError response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.MethodNotFound, $"{jsonRpcRequest.Method} not supported");
                    await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
                }
            }

            // Turn done
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Turn End: {RequestId}", jsonRpcRequest.Id);
            }
        }
        catch (A2AException a2aEx)
        {
            _logger.LogError("Request: {ErrorCode}/{Message}", a2aEx.ErrorCode, a2aEx.Message);
            JsonRpcError response = A2AConverter.CreateErrorResponse(null, a2aEx.ErrorCode, a2aEx.Message);
            await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessAsync: {Message}", ex.Message);
            JsonRpcError response = A2AConverter.CreateErrorResponse(null, A2AErrors.InternalError, ex.Message);
            await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessageAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, ClaimsIdentity identity, IAgent agent, CancellationToken cancellationToken = default)
    {
        // Convert to Activity 
        bool isStreaming = !jsonRpcRequest.Method.Equals(A2AMethods.MessageSend);
        var (activity, contextId, taskId, message) = A2AConverter.ActivityFromRequest(jsonRpcRequest, isStreaming: isStreaming);
        if (activity == null || !activity.Validate([ValidationContext.Channel, ValidationContext.Receiver]))
        {
            httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // Create/update Task
        activity.ChannelData = await _taskStore.CreateOrContinueTaskAsync(contextId, taskId, message: message, cancellationToken: cancellationToken).ConfigureAwait(false);

        // TODO: Error if Task is completed

        var writer = new A2AResponseHandler(_taskStore, jsonRpcRequest.Id, contextId, taskId, isStreaming, _logger);

        InvokeResponse invokeResponse = null;
        
        _responseQueue.StartHandlerForRequest(activity.RequestId);
        await writer.ResponseBegin(httpResponse, cancellationToken).ConfigureAwait(false);

        // Queue the activity to be processed by the ActivityBackgroundService, and stop ChannelResponseQueue when the
        // turn is done.
        _activityTaskQueue.QueueBackgroundActivity(identity, this, activity, agentType: agent.GetType(), onComplete: (response) =>
        {
            invokeResponse = response;
            _responseQueue.CompleteHandlerForRequest(activity.RequestId);
            return Task.CompletedTask;
        });

        // Block until turn is complete.
        // MessageSendParams.Blocking is ignored at the moment.  Always blocks.
        await _responseQueue.HandleResponsesAsync(activity.RequestId, async (activity) =>
        {
            await writer.OnResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessTaskGetAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, bool streamed, CancellationToken cancellationToken)
    {
        object response;

        try
        {
            var queryParams = A2AConverter.ReadParams<TaskQueryParams>(jsonRpcRequest);
            var task = await _taskStore.GetTaskAsync(queryParams.Id, cancellationToken).ConfigureAwait(false);

            response = A2AConverter.CreateResponse(jsonRpcRequest.Id, task);
        }
        catch (KeyNotFoundException)
        {
            response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.TaskNotFoundError, "Task not found.");
        }

        await A2AResponseHandler.WriteResponseAsync(httpResponse, response, streamed, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
    }

    public async Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string messagePrefix, CancellationToken cancellationToken = default)
    {
        var agentCard = new AgentCard()
        {
            Name = nameof(A2AAdapter),
            Description = "Agents SDK A2A",
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            ProtocolVersion = "0.3.0",
            Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{messagePrefix}/",
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                {
                    "jwt",
                    new HTTPAuthSecurityScheme() { Scheme = "bearer" }
                }
            },
            DefaultInputModes = ["application/json"],
            DefaultOutputModes = ["application/json"],
            Skills = [],
            Capabilities = new AgentCapabilities()
            {
                Streaming = true,
            },
            AdditionalInterfaces =
            [
                new AgentInterface()
                {
                    Transport = TransportProtocol.JsonRpc,
                    Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{messagePrefix}/"
                }
            ],
            PreferredTransport = TransportProtocol.JsonRpc,
        };

        // AgentApplication should implement IAgentCardHandler to set agent specific values.  But if
        // it doesn't, the default card will be used.
        if (agent is IAgentCardHandler agentCardHandler)
        {
            agentCard = await agentCardHandler.GetAgentCard(agentCard);
        }

        httpResponse.ContentType = "application/json";
        var json = A2AConverter.ToJson(agentCard);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("AgentCard: {RequestId}", json);
        }

        await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
        await httpResponse.Body.FlushAsync(cancellationToken);
    }

    #region ChannelAdapter
    public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        var context = new TurnContext(this, activity, claimsIdentity);
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        return null;
    }

    public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        await _responseQueue.SendActivitiesAsync(turnContext.Activity.RequestId, activities, cancellationToken);
        return [];
    }
    #endregion
}
