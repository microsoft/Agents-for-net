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

/// <summary>
/// Adapter for handling A2A requests.
/// </summary>
/// <remarks>
/// Register Adapter and map endpoints in startup using:
/// <code>
///    builder.Services.AddA2AAdapter();
/// 
///    app.MapA2A();
/// </code>
/// <see cref="A2AServiceExtensions.AddA2AAdapter(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// <see cref="A2AServiceExtensions.MapA2A(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder, bool, string)"/>
/// </remarks>
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

    /// <inheritdoc/>
    public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonRpcRequest = await A2AResponseHandler.ReadRequestAsync<JsonRpcRequest>(httpRequest);
            var identity = HttpHelper.GetClaimsIdentity(httpRequest);

            // Turn Begin
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Turn Begin: {Request}", A2AConverter.ToJson(jsonRpcRequest));
            }

            if (httpRequest.Method == HttpMethods.Post)
            {
                if (   jsonRpcRequest.Method.Equals(A2AMethods.MessageStream) 
                    || jsonRpcRequest.Method.Equals(A2AMethods.TasksResubscribe) 
                    || jsonRpcRequest.Method.Equals(A2AMethods.MessageSend))
                {
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
                    await ProcessTaskCancelAsync(jsonRpcRequest, httpResponse, identity, agent, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    JsonRpcError response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.MethodNotFound, $"{jsonRpcRequest.Method} not supported");
                    await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }

            // Turn done
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Turn End: {RequestId}", jsonRpcRequest.Id);
            }
        }
        catch(OperationCanceledException)
        {
            _logger.LogDebug("ProcessAsync: OperationCanceledException");
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

    /// <inheritdoc/>
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

    private async Task ProcessMessageAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, ClaimsIdentity identity, IAgent agent, CancellationToken cancellationToken = default)
    {
        // Convert to Activity 
        bool isStreaming = !jsonRpcRequest.Method.Equals(A2AMethods.MessageSend);
        var sendParams = A2AConverter.MessageSendParamsFromRequest(jsonRpcRequest);
        var (activity, contextId, taskId, message) = A2AConverter.ActivityFromRequest(jsonRpcRequest, sendParams: sendParams, isStreaming: isStreaming);
        if (activity == null || !activity.Validate([ValidationContext.Channel, ValidationContext.Receiver]))
        {
            httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // Create/update Task
        var incoming = await _taskStore.CreateOrContinueTaskAsync(contextId, taskId, message: message, cancellationToken: cancellationToken).ConfigureAwait(false);
        activity.ChannelData = incoming.Task;

        if (incoming.Task.IsTerminal())
        {
            JsonRpcError response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.UnsupportedOperationError, $"Task '{taskId}' is in a terminal state");
            await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
            return;
        }

        var writer = new A2AResponseHandler(_taskStore, jsonRpcRequest.Id, incoming.Task, sendParams, isStreaming, _logger);

        InvokeResponse invokeResponse = null;
        
        _responseQueue.StartHandlerForRequest(activity.RequestId);
        await writer.ResponseBegin(httpResponse, cancellationToken).ConfigureAwait(false);

        // Queue the activity to be processed by the ActivityBackgroundService, and stop ChannelResponseQueue when the
        // turn is done.
        _activityTaskQueue.QueueBackgroundActivity(identity, this, activity, agentType: agent.GetType(), onComplete: (response) =>
        {
            invokeResponse = response;

            // Stops response handling and waits for HandleResponsesAsync to finish
            _responseQueue.CompleteHandlerForRequest(activity.RequestId);
            return Task.CompletedTask;
        });

        // Block until turn is complete. This is triggered by CompleteHandlerForRequest and all responses read.
        // MessageSendParams.Blocking is ignored.
        await _responseQueue.HandleResponsesAsync(activity.RequestId, async (activity) =>
        {
            await writer.OnResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessTaskGetAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, bool streamed, CancellationToken cancellationToken)
    {
        object response;

        var queryParams = A2AConverter.ReadParams<TaskQueryParams>(jsonRpcRequest);
        var task = await _taskStore.GetTaskAsync(queryParams.Id, cancellationToken).ConfigureAwait(false);

        if (task == null)
        {
            response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.TaskNotFoundError, $"Task '{queryParams.Id}' not found.");
        }
        else 
        {
            task = task.WithHistoryTrimmedTo(queryParams.HistoryLength);
            response = A2AConverter.CreateResponse(jsonRpcRequest.Id, task);
        }

        await A2AResponseHandler.WriteResponseAsync(httpResponse, response, streamed, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessTaskCancelAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, ClaimsIdentity identity, IAgent agent, CancellationToken cancellationToken)
    {
        object response;

        var queryParams = A2AConverter.ReadParams<TaskIdParams>(jsonRpcRequest);
        var task = await _taskStore.GetTaskAsync(queryParams.Id, cancellationToken).ConfigureAwait(false);

        if (task == null)
        {
            response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.TaskNotFoundError, $"Task '{queryParams.Id}' not found.");
        }
        else if (task.IsTerminal())
        {
            response = A2AConverter.CreateErrorResponse(jsonRpcRequest, A2AErrors.TaskNotCancelableError, $"Task '{queryParams.Id}' is in a terminal state.");
        }
        else
        {
            // Send EndOfConversation to agent
            var eoc = new Activity()
            {
                Type = ActivityTypes.EndOfConversation,
                ChannelId = Channels.A2A,
                Conversation = new ConversationAccount()
                {
                    Id = queryParams.Id
                },
                Recipient = new ChannelAccount
                {
                    Id = "assistant",
                    Role = RoleTypes.Agent,
                },
                From = new ChannelAccount
                {
                    Id = A2AConverter.DefaultUserId,
                    Role = RoleTypes.User,
                },
                Code = EndOfConversationCodes.UserCancelled
            };

            // Note that we're not setting up to handle responses.  May need to rethink this.
            await ProcessActivityAsync(identity, eoc, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);

            // Update task
            task.Status.State = TaskState.Canceled;
            task.Status.Timestamp = DateTimeOffset.UtcNow;
            await _taskStore.UpdateTaskAsync(task, cancellationToken).ConfigureAwait(false);

            response = A2AConverter.CreateResponse(jsonRpcRequest.Id, task);
        }

        await A2AResponseHandler.WriteResponseAsync(httpResponse, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
    }

    #region ChannelAdapter
    public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        var context = new TurnContext(this, activity, claimsIdentity);
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <inheritdoc/>
    public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        await _responseQueue.SendActivitiesAsync(turnContext.Activity.RequestId, activities, cancellationToken);
        return [];
    }
    #endregion
}
