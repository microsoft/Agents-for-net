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
    private readonly ChannelEventNotifier _a2aNotifier;
    private static readonly ConcurrentDictionary<string, A2AServer> _a2aServers = new();
    private static readonly ConcurrentDictionary<string, AgentRequestContext> _a2aAgentContext = new();
    private readonly ILoggerFactory _loggerFactory;

    public A2AAdapter(IStorage storage, ILoggerFactory loggerFactory, ChannelEventNotifier a2aNotifier = null) : this(new StorageTaskStore(storage), loggerFactory, a2aNotifier)
    {
    }

    public A2AAdapter(ITaskStore taskStore, ILoggerFactory loggerFactory, ChannelEventNotifier a2aNotifier = null) : base(loggerFactory.CreateLogger<A2AAdapter>())
    {
        AssertionHelpers.ThrowIfNull(taskStore, nameof(taskStore));

        _loggerFactory = loggerFactory;
        _taskStore = taskStore;
        _a2aNotifier = a2aNotifier ?? new ChannelEventNotifier();

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
            GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent)),
            httpRequest,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string pathPrefix, CancellationToken cancellationToken = default)
    {
        var agentCard = new AgentCard()
        {
            Name = nameof(A2AAdapter),
            Description = "Agents SDK A2A",
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            SecuritySchemes = new Dictionary<string, SecurityScheme>
                    {
                        {
                            "jwt",
                            new SecurityScheme()
                            {
                                HttpAuthSecurityScheme = new HttpAuthSecurityScheme() { Scheme = "bearer"}
                            }
                        }
                    },
            DefaultInputModes = ["application/json"],
            DefaultOutputModes = ["application/json"],
            Skills = [],
            Capabilities = new AgentCapabilities()
            {
                Streaming = true,
            },
            SupportedInterfaces = [],
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

        var agentInterfaces = agent.GetType().GetCustomAttributes<AgentInterfaceAttribute>();
        if (agentInterfaces == null || !agentInterfaces.Any())
        {
            agentCard.SupportedInterfaces.Add(new AgentInterface()
            {
                ProtocolBinding = A2AAgentTransportProtocol.JsonRpc,
                Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{pathPrefix}/",
                ProtocolVersion = "1.0"
            });
        }
        else
        {
            foreach (var agentInterface in agentInterfaces)
            {
                if (agentInterface.Protocol == A2AAgentTransportProtocol.HttpJson || agentInterface.Protocol == A2AAgentTransportProtocol.JsonRpc)
                {
                    agentCard.SupportedInterfaces.Add(new AgentInterface()
                    {
                        ProtocolBinding = agentInterface.Protocol,
                        Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{agentInterface.Path}/",
                        ProtocolVersion = "1.0"
                    });
                }
            }
        }

        var skills = agent.GetType().GetCustomAttributes<A2ASkillAttribute>();
        if (skills != null && skills.Any())
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

    /// <inheritdoc/>
    public Task<IResult> GetTaskAsync(HttpRequest httpRequest, HttpResponse response, IAgent agent, string id, int? historyLength, string? metadata, CancellationToken cancellationToken)
    {
        return A2AHttpProcessor.GetTaskAsync(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent, false)), Logger, id, historyLength, metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> CancelTaskAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default)
    {
        return A2AHttpProcessor.CancelTaskAsync(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent)), Logger, id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> SendMessageAsync(HttpRequest httpRequest, HttpResponse response, IAgent agent, SendMessageRequest sendParams, CancellationToken cancellationToken = default)
    {
        return A2AHttpProcessor.SendMessageAsync(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent)), Logger, sendParams, cancellationToken);
    }

    /// <inheritdoc/>
    public IResult SendMessageStream(HttpRequest httpRequest, HttpResponse response, IAgent agent, SendMessageRequest sendParams, CancellationToken cancellationToken = default)
    {
        return A2AHttpProcessor.SendMessageStream(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent)), Logger, sendParams, cancellationToken);
    }

    /// <inheritdoc/>
    public IResult SubscribeToTask(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default)
    {
        return A2AHttpProcessor.SubscribeToTask(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent)), Logger, id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> SetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, PushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default)
    {
        return A2AHttpProcessor.CreatePushNotificationConfigRestAsync(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent, false)), Logger, id, pushNotificationConfig, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> GetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, string? notificationConfigId, CancellationToken cancellationToken = default)
    {
        return A2AHttpProcessor.GetPushNotificationConfigRestAsync(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent, false)), Logger, id, notificationConfigId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResult> ListPushNotificationConfigsAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, int? pageSize, string? pageToken, CancellationToken cancellationToken)
    {
        return A2AHttpProcessor.ListPushNotificationConfigRestAsync(GetA2AServerForAgent(CreateAgentRequestContext(httpRequest, agent, false)), Logger, id, pageSize, pageToken, cancellationToken);
    }

    private AgentRequestContext CreateAgentRequestContext(HttpRequest httpRequest, IAgent agent, bool cache = true)
    {
        var agentContext = new AgentRequestContext(httpRequest, this, agent);
        return cache ? _a2aAgentContext.GetOrAdd(agentContext.RequestId, agentContext) : agentContext;
    }

    private A2AServer GetA2AServerForAgent(AgentRequestContext agentContext)
    {
        return new A2AServer(
            agentContext,
            _taskStore,
            _a2aNotifier,
            _loggerFactory.CreateLogger<A2AServer>(),
            new A2AServerOptions());
    }

    #region A2A Agent
    internal async Task ExecuteAgentTurnAsync(string requestId, ClaimsIdentity identity, IAgent agent, RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        using (Logger.BeginScope("ExecuteAgentTurnAsync: Agent={AgentType}, RequestId={RequestId}, TaskId={TaskId}", agent.GetType().Name, requestId, context.TaskId))
        {
            var activity = A2AActivity.ActivityFromMessage(requestId, context.TaskId, context.Message);
            if (activity == null || !activity.Validate(ValidationContext.Channel | ValidationContext.Receiver))
            {
                Logger.LogError("Invalid Activity for RequestId={RequestId}, TaskId={TaskId}", requestId, context.TaskId);
                throw new A2AException($"Invalid Activity for RequestId={requestId}", A2AErrorCode.InternalError);
            }

            Log.WithRequestIdAndBody(Logger, context.TaskId, ProtocolJsonSerializer.ToJson(activity));

            try
            {
                _ = await ProcessActivityAsync(identity, activity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _a2aAgentContext.TryRemove(requestId, out _);
            }
        }
    }

    internal async Task ExecuteAgentCancelTaskAsync(string requestId, ClaimsIdentity identity, IAgent agent, RequestContext context, CancellationToken cancellationToken)
    {
        using (Logger.BeginScope("ExecuteAgentCancelTaskAsync: Agent={AgentType}, RequestId={RequestId}", agent.GetType().Name, requestId))
        {
            var eoc = new Activity()
            {
                Type = ActivityTypes.EndOfConversation,
                Code = EndOfConversationCodes.UserCancelled,
                ChannelId = Channels.A2A,
                Conversation = new ConversationAccount() { Id = context.TaskId },
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
    }
    #endregion

    #region ChannelAdapter
    /// <inheritdoc/>
    public override Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        return ProcessActivityWithA2AAsync(claimsIdentity, activity, callback, null, null, cancellationToken);
    }

    public async Task<InvokeResponse> ProcessActivityWithA2AAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, RequestContext a2aContext, AgentEventQueue a2aEventQueue, CancellationToken cancellationToken)
    {
        var context = new TurnContext(this, activity, claimsIdentity);
        if (a2aContext != null && a2aEventQueue != null)
        {
            context.Services.Set(a2aContext);
            context.Services.Set(a2aEventQueue);
        }
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <inheritdoc/>
    public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        if (!_a2aAgentContext.TryGetValue(turnContext.Activity.RequestId, out var agentContext))
        {
            throw new InvalidOperationException("AgentContext not found");
        }

        return agentContext.SendActivitiesAsync(turnContext, activities, cancellationToken);
    }
    #endregion
}

class AgentRequestContext : IAgentHandler
{
    public string RequestId { get; }
    public A2AAdapter Adapter { get; } 
    public IAgent Agent { get; }
    public ClaimsIdentity Identity { get; }
    public AgentEventQueue EventQueue { get; private set; }

    public AgentRequestContext(HttpRequest httpRequest, A2AAdapter adapter, IAgent agent)
    {
        Adapter = adapter;
        Agent = agent;
        Identity = HttpHelper.GetClaimsIdentity(httpRequest);
        RequestId = httpRequest.HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();
    }

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        EventQueue = eventQueue;

        if (!context.IsContinuation)
        {
            var taskUpdater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
            await taskUpdater.SubmitAsync(cancellationToken).ConfigureAwait(false);
        }

        await Adapter.ExecuteAgentTurnAsync(RequestId, Identity, Agent, context, eventQueue, cancellationToken);
    }

    public async Task CancelAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
        await updater.CancelAsync(cancellationToken).ConfigureAwait(false);
        await Adapter.ExecuteAgentCancelTaskAsync(RequestId, Identity, Agent, context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
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
                Adapter.Logger.LogDebug("A2AResponseHandler.OnResponse: Unhandled Activity Type: {ActivityType}", activity.Type);
            }
        }

        return [];
    }

    private async Task OnMessageResponse(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (Message)turnContext.Activity.ChannelData;
        var state = activity.GetA2ATaskState();
        var response = A2AActivity.MessageFromActivity(incomingMessage.ContextId, incomingMessage.TaskId, activity);

        await EventQueue.EnqueueStatusUpdateAsync(new TaskStatusUpdateEvent
        {
            TaskId = incomingMessage.TaskId,
            ContextId = incomingMessage.ContextId,
            Status = new ()
            {
                State = state,
                Timestamp = DateTimeOffset.UtcNow,
                Message = response,
            },
        }, cancellationToken);
        EventQueue.Complete();
    }

    private async Task OnStreamingResponse(ITurnContext turnContext, IActivity activity, StreamInfo entity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (Message)turnContext.Activity.ChannelData;
        var isInformative = entity.StreamType == StreamTypes.Informative;

        if (isInformative)
        {
            // Informative is a Status update with a Message
            await EventQueue.EnqueueStatusUpdateAsync(new TaskStatusUpdateEvent
            {
                TaskId = incomingMessage.TaskId,
                ContextId = incomingMessage.ContextId,
                Status = new()
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow,
                    Message = A2AActivity.MessageFromActivity(incomingMessage.ContextId, incomingMessage.TaskId, activity),
                },
            }, cancellationToken).ConfigureAwait(false);
            EventQueue.Complete();
        }
        else
        {
            // This is using entity.StreamId for the artifactId.
            var artifact = A2AActivity.CreateArtifact(activity, artifactId: entity.StreamId);

            await EventQueue.EnqueueArtifactUpdateAsync(new TaskArtifactUpdateEvent
            {
                TaskId = incomingMessage.TaskId,
                ContextId = incomingMessage.ContextId,
                Artifact = artifact,
                Append = false,
                LastChunk = true,
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task OnEndOfConversationResponse(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = (Message)turnContext.Activity.ChannelData;

        // Set optional EOC Value as an Artifact.
        if (activity.Value != null)
        {
            var artifact = A2AActivity.CreateArtifactFromObject(
                activity.Value,
                name: "Result",
                description: "Task completion result",
                mediaType: "application/json");

            await EventQueue.EnqueueArtifactUpdateAsync(new TaskArtifactUpdateEvent
            {
                TaskId = incomingMessage.TaskId,
                ContextId = incomingMessage.ContextId,
                Artifact = artifact,
                Append = false,
                LastChunk = true,
            }, cancellationToken).ConfigureAwait(false);
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
        var response = A2AActivity.MessageFromActivity(incomingMessage.ContextId, incomingMessage.TaskId, statusMessage);

        await EventQueue.EnqueueStatusUpdateAsync(new TaskStatusUpdateEvent
        {
            TaskId = incomingMessage.TaskId,
            ContextId = incomingMessage.ContextId,
            Status = new()
            {
                State = taskState,
                Timestamp = DateTimeOffset.UtcNow,
                Message = response,
            },
        }, cancellationToken).ConfigureAwait(false);
        EventQueue.Complete();
    }
}

static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "A2A turn with requestId '{RequestId}', and Activity: {Activity}")]
    public static partial void WithRequestIdAndBody(ILogger logger, string requestId, string activity);
}
