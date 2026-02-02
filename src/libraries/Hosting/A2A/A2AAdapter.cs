// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

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
/// <see cref="A2AServiceExtensions.MapA2AJsonRpc(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder, bool, string)"/>
/// </remarks>
public class A2AAdapter : ChannelAdapter, IA2AHttpAdapter
{
    private readonly TaskManager _taskManager;
    private readonly ILogger<A2AAdapter> _logger;
    internal static readonly AsyncLocal<A2ARequestContext> A2AContext = new();

    public A2AAdapter(IActivityTaskQueue activityTaskQueue, IStorage storage, ILogger<A2AAdapter> logger = null) : base(logger)
    {
        _logger = logger ?? NullLogger<A2AAdapter>.Instance;

        _taskManager = new TaskManager(taskStore: new InMemoryTaskStore()) // new StorageTaskStore(storage))
        {
            OnTaskCreated = ExecuteAgentTaskAsync,
            OnTaskUpdated = ExecuteAgentTaskAsync
        };

        OnTurnError = (turnContext, exception) =>
        {
            _logger.LogError(exception, "A2AAdapter.OnTurnError: An error occurred during turn processing.");
            return Task.CompletedTask;
        };
    }

    /// <inheritdoc/>
    public Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
    {
        // Default to JsonRpc
        return ProcessJsonRpcAsync(httpRequest, httpResponse, agent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IResult> ProcessJsonRpcAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
    {
        A2AContext.Value = new A2ARequestContext()
        {
            Agent = agent,
            Identity = HttpHelper.GetClaimsIdentity(httpRequest),
        };
        return await A2AJsonRpcProcessor.ProcessRequestAsync(_taskManager, httpRequest, cancellationToken);
    }

    public Task<IResult> ProcessHttpJsonAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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
                            new HttpAuthSecurityScheme("bearer")
                        }
                    },
            DefaultInputModes = ["application/json"],
            DefaultOutputModes = ["application/json"],
            Skills = [],
            Capabilities = new AgentCapabilities()
            {
                Streaming = false,
            },
            AdditionalInterfaces =
            [
                new AgentInterface()
                    {
                        Transport = AgentTransport.JsonRpc,
                        Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{messagePrefix}/"
                    }
            ],
            PreferredTransport = AgentTransport.JsonRpc,
        };

        // AgentApplication should implement IAgentCardHandler to set agent specific values.  But if
        // it doesn't, the default card will be used.
        if (agent is IAgentCardHandler agentCardHandler)
        {
            agentCard = await agentCardHandler.GetAgentCard(agentCard);
        }

        var json = ProtocolJsonSerializer.ToJson(agentCard);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("AgentCard: {AgentCard}", json);
        }

        httpResponse.ContentType = "application/json";
        await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
        await httpResponse.Body.FlushAsync(cancellationToken);
    }

    public async Task ExecuteAgentTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        // TODO This does not appear to work with A2A SSE. They are doing Task.Run before this call and we get null back here.
        var agentContext = A2AContext.Value;
        AssertionHelpers.ThrowIfNull(agentContext, nameof(agentContext));

        var userMessage = task.History!.Last();

        var activity = A2AActivity.ActivityFromMessage(agentContext.RequestId.ToString(), task, userMessage);
        if (activity == null || !activity.Validate(ValidationContext.Channel | ValidationContext.Receiver))
        {
            //!!!
            //httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        if (task.IsTerminal())
        {
            //!!!
            //JsonRpcResponse response = JsonRpcResponse.UnsupportedOperationResponse(jsonRpcRequest.Id, $"Task '{taskId}' is in a terminal state");
            //await A2AResponseHandler.WriteResponseAsync(httpResponse, jsonRpcRequest.Id, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
            return;
        }

        _ = await ProcessActivityAsync(agentContext.Identity, activity, agentContext.Agent.OnTurnAsync, cancellationToken);
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
        foreach (var activity in activities)
        {
            var entity = activity.GetStreamingEntity();
            if (entity != null)
            {
                await OnStreamingResponse(turnContext, activity, entity, cancellationToken).ConfigureAwait(false);
            }
            else if (activity.IsType(ActivityTypes.Message))
            {
                await OnMessageResponse(turnContext, activity, cancellationToken).ConfigureAwait(false);
            }
            else if (activity.IsType(ActivityTypes.EndOfConversation))
            {
                await OnEndOfConversationResponse(turnContext, activity, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("A2AResponseHandler.OnResponse: Unhandled Activity Type: {ActivityType}", activity.Type);
            }
        }
        return [];
    }
    #endregion

    private async Task OnMessageResponse(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (AgentMessage)turnContext.Activity.ChannelData;
        await _taskManager.UpdateStatusAsync(incomingMessage.TaskId, activity.GetA2ATaskState(), A2AActivity.CreateMessage(incomingMessage.ContextId, incomingMessage.TaskId, activity), false, cancellationToken);
    }

    private async Task OnStreamingResponse(ITurnContext turnContext, IActivity activity, StreamInfo entity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (AgentMessage)turnContext.Activity.ChannelData;

        //var isLastChunk = entity.StreamType == StreamTypes.Final;
        var isInformative = entity.StreamType == StreamTypes.Informative;

        if (isInformative)
        {
            // Informative is a Status update with a Message
            await _taskManager.UpdateStatusAsync(incomingMessage.TaskId, TaskState.Working, A2AActivity.CreateMessage(incomingMessage.ContextId, incomingMessage.TaskId, activity), false, cancellationToken);
        }
        else
        {
            // This is using entity.StreamId for the artifactId.
            var artifact = A2AActivity.CreateArtifact(activity, artifactId: entity.StreamId);
            await _taskManager.ReturnArtifactAsync(incomingMessage.TaskId, artifact, cancellationToken);
        }
    }

    private async Task OnEndOfConversationResponse(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (AgentMessage)turnContext.Activity.ChannelData;

        // Set optional EOC Value as an Artifact.
        if (activity.Value != null)
        {
            var artifact = A2AActivity.CreateArtifactFromObject(
                activity.Value,
                name: "Result",
                description: "Task completion result",
                mediaType: "application/json");

            await _taskManager.ReturnArtifactAsync(incomingMessage.TaskId, artifact, cancellationToken);
        }

        // Upate status to terminal.  Status event sent in ResponseEnd
        TaskState taskState = activity.Code switch
        {
            EndOfConversationCodes.Error => TaskState.Failed,
            EndOfConversationCodes.UserCancelled => TaskState.Canceled,
            _ => TaskState.Completed,
        };

        // ResponseEnd sends status
        IActivity statusMessage = null;
        if (activity.HasA2AMessageContent())
        {
            // Clone to avoid altering input Activity
            statusMessage = ProtocolJsonSerializer.CloneTo<IActivity>(activity);

            // Value was set as Artifact on Task
            statusMessage.Value = null;
        }

        await _taskManager.UpdateStatusAsync(incomingMessage.TaskId, taskState, A2AActivity.CreateMessage(incomingMessage.ContextId, incomingMessage.TaskId, statusMessage), true, cancellationToken);
    }
}

internal class A2ARequestContext
{
    public IAgent Agent { get; set; }
    public ClaimsIdentity Identity { get; set; }
    public JsonRpcId RequestId { get; set; }
}