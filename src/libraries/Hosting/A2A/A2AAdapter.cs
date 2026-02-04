// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
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
    private readonly ITaskStore _taskStore;
    private static readonly ConcurrentDictionary<string, AgentShim> _a2aAgentContext = new();

    public A2AAdapter(IStorage storage, ILogger<A2AAdapter> logger = null) : base(logger ?? NullLogger<A2AAdapter>.Instance)
    {
        AssertionHelpers.ThrowIfNull(storage, nameof(storage));
        _taskStore = new StorageTaskStore(storage);

        OnTurnError = (turnContext, exception) =>
        {
            Logger.LogError(exception, "A2AAdapter.OnTurnError: An error occurred during turn processing.");
            throw new A2AException($"A2AAdapter.OnTurnError: An error occurred during turn processing: {exception.Message}", exception);
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
        return await A2AJsonRpcProcessor.ProcessRequestAsync(
            httpRequest, 
            (requestId) =>
            {
                var shim = CreateShim(httpRequest, agent, requestId);
                return shim.GetTaskManager();
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IResult> GetTaskAsync(HttpRequest httpRequest, HttpResponse response, IAgent agent, string id, int? historyLength, string? metadata, CancellationToken cancellationToken)
    {
        var shim = CreateShim(httpRequest, agent);
        return A2AHttpProcessor.GetTaskAsync(shim.GetTaskManager(), Logger, id, historyLength, metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> CancelTaskAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default)
    {
        var shim = CreateShim(httpRequest, agent);
        return A2AHttpProcessor.CancelTaskAsync(shim.GetTaskManager(), Logger, id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> SendMessageAsync(HttpRequest httpRequest, HttpResponse response, IAgent agent, MessageSendParams sendParams, CancellationToken cancellationToken = default)
    {
        var shim = CreateShim(httpRequest, agent);
        return A2AHttpProcessor.SendMessageAsync(shim.GetTaskManager(), Logger, sendParams, cancellationToken);
    }

    /// <inheritdoc/>
    public IResult SendMessageStream(HttpRequest httpRequest, HttpResponse response, IAgent agent, MessageSendParams sendParams, CancellationToken cancellationToken = default)
    {
        var shim = CreateShim(httpRequest, agent);
        return A2AHttpProcessor.SendMessageStream(shim.GetTaskManager(), Logger, sendParams, cancellationToken);
    }

    /// <inheritdoc/>
    public IResult SubscribeToTask(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default)
    {
        var shim = CreateShim(httpRequest, agent);
        return A2AHttpProcessor.SubscribeToTask(shim.GetTaskManager(), Logger, id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> SetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, PushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default)
    {
        var shim = CreateShim(httpRequest, agent);
        return A2AHttpProcessor.SetPushNotificationAsync(shim.GetTaskManager(), Logger, id, pushNotificationConfig, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> GetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, string? notificationConfigId, CancellationToken cancellationToken = default)
    {
        var shim = CreateShim(httpRequest, agent);
        return A2AHttpProcessor.GetPushNotificationAsync(shim.GetTaskManager(), Logger, id, notificationConfigId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string pathPrefix, CancellationToken cancellationToken = default)
    {
        var agentCard = new AgentCard()
        {
            Name = nameof(A2AAdapter),
            Description = "Agents SDK A2A",
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            ProtocolVersion = "0.3.0",
            Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{pathPrefix}/",
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
                Streaming = true,
            },
            AdditionalInterfaces =
            [
                new AgentInterface()
                    {
                        Transport = AgentTransport.JsonRpc,
                        Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{pathPrefix}/"
                    }
            ],
            PreferredTransport = AgentTransport.JsonRpc,
        };

        var agentAttribute = agent.GetType().GetCustomAttribute<AgentAttribute>();
        if (agentAttribute != null)
        {
            if (!string.IsNullOrEmpty(agentAttribute.Name))
            {
                agentCard.Name = agentAttribute.Name;
            }
            if (!string.IsNullOrEmpty(agentAttribute.Description))
            {
                agentCard.Description = agentAttribute.Description;
            }
            if (!string.IsNullOrEmpty(agentAttribute.Version))
            {
                agentCard.Version = agentAttribute.Version;
            }
        }

        var skills = agent.GetType().GetCustomAttributes<A2ASkillAttribute>();
        if (skills != null)
        {
            foreach (var skillAttr in skills)
            {
                var skill = new AgentSkill()
                {
                    Id = skillAttr.Id,
                    Name = skillAttr.Name,
                    Description = skillAttr.Description,
                    Tags = skillAttr.Tags,
                    Examples = skillAttr.Examples,
                    InputModes = skillAttr.InputModes,
                    OutputModes = skillAttr.OutputModes,
                };
                agentCard.Skills.Add(skill);
            }
        }

        // AgentApplication could implement IAgentCardHandler to set agent specific values.  But if
        // it doesn't, the default card will be used.
        if (agent is IAgentCardHandler agentCardHandler)
        {
            agentCard = await agentCardHandler.GetAgentCard(agentCard);
        }

        var json = ProtocolJsonSerializer.ToJson(agentCard);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("AgentCard: {AgentCard}", json);
        }

        httpResponse.ContentType = "application/json";
        await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken).ConfigureAwait(false);
        await httpResponse.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    #region ChannelAdapter
    /// <inheritdoc/>
    public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        var context = new TurnContext(this, activity, claimsIdentity);
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <inheritdoc/>
    public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        var taskManager = _a2aAgentContext[turnContext.Activity.RequestId].GetTaskManager();
        AssertionHelpers.ThrowIfNull(taskManager, nameof(taskManager));

        foreach (var activity in activities)
        {
            var entity = activity.GetStreamingEntity();
            if (entity != null)
            {
                await OnStreamingResponse(taskManager, turnContext, activity, entity, cancellationToken).ConfigureAwait(false);
            }
            else if (activity.IsType(ActivityTypes.Message))
            {
                await OnMessageResponse(taskManager, turnContext, activity, cancellationToken).ConfigureAwait(false);
            }
            else if (activity.IsType(ActivityTypes.EndOfConversation))
            {
                await OnEndOfConversationResponse(taskManager, turnContext, activity, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Logger.LogDebug("A2AResponseHandler.OnResponse: Unhandled Activity Type: {ActivityType}", activity.Type);
            }
        }
        return [];
    }
    #endregion

    private AgentShim CreateShim(HttpRequest httpRequest, IAgent agent, string requestId = null)
    {
        requestId ??= Guid.NewGuid().ToString("N");
        var shim = new AgentShim(requestId, HttpHelper.GetClaimsIdentity(httpRequest), agent, _taskStore, ExecuteAgentTaskAsync, ExecuteAgentTaskCancelAsync);
        _a2aAgentContext[requestId] = shim;
        return shim;
    }

    private async Task ExecuteAgentTaskCancelAsync(string requestId, ClaimsIdentity identity, IAgent agent, ITaskManager taskManager, AgentTask task, CancellationToken cancellationToken)
    {
        var eoc = new Activity()
        {
            Type = ActivityTypes.EndOfConversation,
            Code = EndOfConversationCodes.UserCancelled,
            ChannelId = Channels.A2A,
            Conversation = new ConversationAccount() { Id = task.Id },
            Recipient = new ChannelAccount { Id = "assistant", Role = RoleTypes.Agent },
            From = new ChannelAccount { Id = A2AActivity.DefaultUserId, Role = RoleTypes.User }
        };

        try
        {
            _ = await ProcessActivityAsync(identity, eoc, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _a2aAgentContext.TryRemove(requestId, out _);
        }
    }

    private async Task ExecuteAgentTaskAsync(string requestId, ClaimsIdentity identity, IAgent agent, ITaskManager taskManager, AgentTask task, CancellationToken cancellationToken)
    {
        var activity = A2AActivity.ActivityFromMessage(requestId, task, task.History!.Last());
        if (activity == null || !activity.Validate(ValidationContext.Channel | ValidationContext.Receiver))
        {
            throw new A2AException($"Invalid Activity for RequestId={requestId}", A2AErrorCode.InternalError);
        }

        try
        {
            _ = await ProcessActivityAsync(identity, activity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (taskManager is TaskManagerWrapper wrapper)
            {
                wrapper.CloseStream(task.Id);
            }

            _a2aAgentContext.TryRemove(requestId, out _);
        }
    }

    private static async Task OnMessageResponse(ITaskManager taskManager, ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (AgentMessage)turnContext.Activity.ChannelData;
        var state = activity.GetA2ATaskState();
        var response = A2AActivity.CreateMessage(incomingMessage.ContextId, incomingMessage.TaskId, activity);
        await taskManager.UpdateStatusAsync(incomingMessage.TaskId, state, response, false, cancellationToken).ConfigureAwait(false);
    }

    private static async Task OnStreamingResponse(ITaskManager taskManager, ITurnContext turnContext, IActivity activity, StreamInfo entity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (AgentMessage)turnContext.Activity.ChannelData;

        //var isLastChunk = entity.StreamType == StreamTypes.Final;
        var isInformative = entity.StreamType == StreamTypes.Informative;

        if (isInformative)
        {
            // Informative is a Status update with a Message
            await taskManager.UpdateStatusAsync(incomingMessage.TaskId, TaskState.Working, A2AActivity.CreateMessage(incomingMessage.ContextId, incomingMessage.TaskId, activity), false, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // This is using entity.StreamId for the artifactId.
            var artifact = A2AActivity.CreateArtifact(activity, artifactId: entity.StreamId);
            await taskManager.ReturnArtifactAsync(incomingMessage.TaskId, artifact, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task OnEndOfConversationResponse(ITaskManager taskManager, ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
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

            await taskManager.ReturnArtifactAsync(incomingMessage.TaskId, artifact, cancellationToken).ConfigureAwait(false);
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
        var response = A2AActivity.CreateMessage(incomingMessage.ContextId, incomingMessage.TaskId, statusMessage);

        await taskManager.UpdateStatusAsync(incomingMessage.TaskId, taskState, response, true, cancellationToken).ConfigureAwait(false);
    }
}
