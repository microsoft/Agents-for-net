// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using A2AProtocolAgentCard = A2A.AgentCard;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Extensions.A2A.AgentCard;
using Microsoft.Agents.Extensions.A2A.Authorization;
using Microsoft.Agents.Extensions.A2A.Errors;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Pipeline;

/// <summary>
/// Implements A2A request processing for the extension's registered endpoints.
/// </summary>
/// <remarks>
/// The extension creates this adapter through its service registration. It is not a consumer
/// customization or replacement point.
/// </remarks>
[ChannelAdapter(Channels.A2A)]
internal class A2AAdapter : ChannelAdapter, IA2AHttpAdapter
{
    private readonly ITaskStore _taskStore;
    private readonly ChannelEventNotifier _a2aNotifier;
    private static readonly ConcurrentDictionary<string, AgentRequestContext> _a2aAgentContext = new();
    private static readonly A2AServerOptions _a2aServerOptions = new();
    private static readonly string _assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<A2AServer> _a2aServerLogger;
    private readonly string _agentCardLastModified = DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture);
    private readonly string _agentCardCacheControl;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes an adapter with an in-memory A2A task store.
    /// </summary>
    /// <param name="storage">The storage provider retained for compatibility with agent host registration.</param>
    /// <param name="loggerFactory">The factory used to create adapter loggers.</param>
    /// <param name="a2aNotifier">The optional notifier for A2A channel events.</param>
    /// <param name="options">The optional A2A adapter options.</param>
    /// <param name="configuration">The optional configuration used to compose Agent Cards.</param>
    public A2AAdapter(
        IStorage storage,
        ILoggerFactory loggerFactory,
        ChannelEventNotifier a2aNotifier = null,
        A2AAdapterOptions options = null,
        IConfiguration configuration = null)
        : this(new InMemoryTaskStore(), loggerFactory, a2aNotifier, options, configuration)
    {
    }

    /// <summary>
    /// Initializes an adapter with the specified A2A task store.
    /// </summary>
    /// <param name="taskStore">The store used to persist A2A tasks.</param>
    /// <param name="loggerFactory">The factory used to create adapter loggers.</param>
    /// <param name="a2aNotifier">The optional notifier for A2A channel events.</param>
    /// <param name="options">The optional A2A adapter options.</param>
    /// <param name="configuration">The optional configuration used to compose Agent Cards.</param>
    /// <exception cref="ArgumentNullException"><paramref name="taskStore"/> is <see langword="null"/>.</exception>
    public A2AAdapter(
        ITaskStore taskStore,
        ILoggerFactory loggerFactory,
        ChannelEventNotifier a2aNotifier = null,
        A2AAdapterOptions options = null,
        IConfiguration configuration = null)
        : base(loggerFactory.CreateLogger<A2AAdapter>())
    {
        AssertionHelpers.ThrowIfNull(taskStore, nameof(taskStore));

        var adapterOptions = options
            ?? configuration?.GetSection(nameof(A2AAdapterOptions)).Get<A2AAdapterOptions>()
            ?? new A2AAdapterOptions();
        if (adapterOptions.AgentCardCacheMaxAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                adapterOptions.AgentCardCacheMaxAge,
                "Agent Card cache max-age cannot be negative.");
        }

        _loggerFactory = loggerFactory;
        _taskStore = taskStore;
        _a2aNotifier = a2aNotifier ?? new ChannelEventNotifier();
        _a2aServerLogger = loggerFactory.CreateLogger<A2AServer>();
        _agentCardCacheControl = $"public, max-age={(long)Math.Ceiling(adapterOptions.AgentCardCacheMaxAge.TotalSeconds)}";
        _configuration = configuration;

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
        var agentContext = CreateAgentRequestContext(httpRequest, agent);
        ApplyActivatedExtensions(agentContext, httpResponse);
        var server = GetA2AServerForAgent(agentContext);
        return await A2AJsonRpcProcessor.ProcessRequestAsync(
            server,
            httpRequest,
            cancellationToken,
            (requestId, method, parameters, ct) => ProcessExtensionJsonRpcAsync(agentContext, requestId, method, parameters, ct)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string pathPrefix, CancellationToken cancellationToken = default)
    {
        var agentCard = new A2AProtocolAgentCard()
        {
            Name = nameof(A2AAdapter),
            Description = "Agents SDK A2A",
            Version = _assemblyVersion,
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
                ExtendedAgentCard = false,
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

        agentCard = await new A2AAgentCardComposer(_configuration).ComposeAsync(agentCard, agent).ConfigureAwait(false);

        var json = ProtocolJsonSerializer.ToJson(agentCard);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("AgentCard: {AgentCard}", json);
        }

        var jsonBytes = Encoding.UTF8.GetBytes(json);

        httpResponse.ContentType = "application/json";
        httpResponse.Headers.CacheControl = _agentCardCacheControl;
        httpResponse.Headers.ETag = $"\"{Convert.ToHexString(SHA256.HashData(jsonBytes))}\"";
        httpResponse.Headers.LastModified = _agentCardLastModified;
        await httpResponse.Body.WriteAsync(jsonBytes, cancellationToken).ConfigureAwait(false);
        await httpResponse.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    #region HTTP Endpoints
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
        var agentContext = CreateAgentRequestContext(httpRequest, agent);
        ApplyActivatedExtensions(agentContext, response);
        return A2AHttpProcessor.SendMessageAsync(GetA2AServerForAgent(agentContext), Logger, sendParams, cancellationToken);
    }

    Task<IResult> IA2AHttpAdapter.ResumeAuthAsync(
        HttpRequest httpRequest,
        HttpResponse response,
        IAgent agent,
        string taskId,
        ResumeAuthRequest request,
        CancellationToken cancellationToken)
        => ResumeAuthAsync(httpRequest, response, agent, taskId, request, cancellationToken);

    private Task<IResult> ResumeAuthAsync(
        HttpRequest httpRequest,
        HttpResponse response,
        IAgent agent,
        string taskId,
        ResumeAuthRequest request,
        CancellationToken cancellationToken)
    {
        request.TaskId = taskId;
        var agentContext = CreateAgentRequestContext(httpRequest, agent);
        ApplyActivatedExtensions(agentContext, response);
        return A2AHttpProcessor.ResumeAuthAsync(
            Logger,
            async ct =>
            {
                try
                {
                    await SetResumeAuthorizationAsync(agentContext, httpRequest, request, ct).ConfigureAwait(false);
                    return await ResumeTaskAsync(agentContext, request, ct).ConfigureAwait(false);
                }
                finally
                {
                    RemoveAgentContext(agentContext);
                }
            },
            cancellationToken);
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
    #endregion

    #region Agent Turn Processing
    private AgentRequestContext CreateAgentRequestContext(HttpRequest httpRequest, IAgent agent, bool cache = true)
    {
        var agentContext = new AgentRequestContext(httpRequest, this, agent, Logger);
        return cache ? _a2aAgentContext.GetOrAdd(agentContext.RequestId, agentContext) : agentContext;
    }

    private A2AServerWithoutExtendedAgentCard GetA2AServerForAgent(AgentRequestContext agentContext)
    {
        return new A2AServerWithoutExtendedAgentCard(agentContext, _taskStore, _a2aNotifier, _a2aServerLogger, _a2aServerOptions);
    }

    private sealed class A2AServerWithoutExtendedAgentCard(
        IAgentHandler handler,
        ITaskStore taskStore,
        ChannelEventNotifier notifier,
        ILogger<A2AServer> logger,
        A2AServerOptions options)
        : A2AServer(handler, taskStore, notifier, logger, options)
    {
        public override Task<A2AProtocolAgentCard> GetExtendedAgentCardAsync(
            GetExtendedAgentCardRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new A2AException("Extended agent cards are not supported.", A2AErrorCode.UnsupportedOperation);
        }
    }

    internal async Task ExecuteAgentTurnAsync(AgentRequestContext agentContext, RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var activity = A2AActivity.ActivityFromMessage(agentContext.RequestId, context.TaskId, context.Message);
        if (activity == null || !activity.Validate(ValidationContext.Channel | ValidationContext.Receiver))
        {
            Logger.LogError("Invalid Activity for RequestId={RequestId}, TaskId={TaskId}", agentContext.RequestId, context.TaskId);
            throw new A2AException($"Invalid Activity for RequestId={agentContext.RequestId}", A2AErrorCode.InternalError);
        }

        using var loggerScope = Logger.BeginScope(new Dictionary<string, object>
        {
            ["AgentType"] = agentContext.Agent.GetType().Name,
            ["RequestId"] = agentContext.RequestId,
            ["ConversationId"] = activity.Conversation?.Id,
            ["TaskId"] = context.TaskId
        });

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Log.LogRequest(Logger, context.TaskId, ProtocolJsonSerializer.ToJson(activity));
        }

        try
        {
            _ = await ProcessActivityWithA2AAuthenticationAsync(
                agentContext.Identity,
                agentContext.Authentication,
                activity,
                agentContext.Agent.OnTurnAsync,
                context,
                eventQueue,
                agentContext.ExtensionRequest,
                agentContext.ResumeAuthorization,
                agentContext,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RemoveAgentContext(agentContext);
        }
    }

    internal async Task ExecuteAgentCancelTaskAsync(string requestId, ClaimsIdentity identity, A2ARequestAuthentication authentication, IAgent agent, RequestContext context, CancellationToken cancellationToken)
    {
        using var loggerScope = Logger.BeginScope(new Dictionary<string, object>
        {
            ["AgentType"] = agent.GetType().Name,
            ["RequestId"] = requestId,
            ["TaskId"] = context.TaskId
        });

        var eoc = new Activity()
        {
            Type = ActivityTypes.EndOfConversation,
            Code = EndOfConversationCodes.UserCancelled,
            ChannelId = Channels.A2A,
            Conversation = new ConversationAccount() { Id = context.TaskId },
            Recipient = new ChannelAccount { Id = "assistant", Role = RoleTypes.Agent },
            From = new ChannelAccount { Id = context.TaskId, Role = RoleTypes.User }
        };

        try
        {
            _ = await ProcessActivityWithA2AAuthenticationAsync(
                identity,
                authentication,
                eoc,
                agent.OnTurnAsync,
                context,
                null,
                null,
                null,
                null,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _a2aAgentContext.TryRemove(requestId, out _);
        }
    }
    #endregion

    #region ChannelAdapter
    /// <inheritdoc/>
    public override Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        if (_a2aAgentContext.TryGetValue(activity.RequestId, out var agentContext))
        {
            return ProcessActivityWithA2AAuthenticationAsync(
                claimsIdentity,
                agentContext.Authentication,
                activity,
                callback,
                agentContext.CurrentContext,
                agentContext.EventQueue,
                agentContext.ExtensionRequest,
                agentContext.ResumeAuthorization,
                agentContext,
                cancellationToken);
        }

        return ProcessActivityWithA2AAsync(claimsIdentity, activity, callback, null, null, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task ProcessProactiveAsync(
        ClaimsIdentity claimsIdentity,
        IActivity continuationActivity,
        string audience,
        AgentCallbackHandler callback,
        CancellationToken cancellationToken)
    {
        if (_a2aAgentContext.TryGetValue(continuationActivity.RequestId, out var agentContext))
        {
            await ProcessActivityWithA2AAuthenticationAsync(
                claimsIdentity,
                agentContext.Authentication,
                continuationActivity,
                callback,
                agentContext.CurrentContext,
                agentContext.EventQueue,
                agentContext.ExtensionRequest,
                agentContext.ResumeAuthorization,
                agentContext,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await base.ProcessProactiveAsync(claimsIdentity, continuationActivity, audience, callback, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes an activity while attaching A2A request services to its turn context.
    /// </summary>
    /// <remarks>
    /// The request context and event queue are attached only for this turn, making
    /// <see cref="A2AClient"/> available to A2A route handlers during same-turn processing.
    /// </remarks>
    /// <param name="claimsIdentity">The identity associated with the incoming activity.</param>
    /// <param name="activity">The activity to process.</param>
    /// <param name="callback">The callback that invokes the agent pipeline.</param>
    /// <param name="a2aContext">The A2A request context to attach to the turn.</param>
    /// <param name="a2aEventQueue">The A2A event queue to attach to the turn.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>A task that resolves to the invoke response after the agent pipeline completes.</returns>
    public Task<InvokeResponse> ProcessActivityWithA2AAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, RequestContext a2aContext, AgentEventQueue a2aEventQueue, CancellationToken cancellationToken)
    {
        return ProcessActivityWithA2AAuthenticationAsync(claimsIdentity, null, activity, callback, a2aContext, a2aEventQueue, null, null, null, cancellationToken);
    }

    private async Task<InvokeResponse> ProcessActivityWithA2AAuthenticationAsync(
        ClaimsIdentity claimsIdentity,
        A2ARequestAuthentication authentication,
        IActivity activity,
        AgentCallbackHandler callback,
        RequestContext a2aContext,
        AgentEventQueue a2aEventQueue,
        A2AProtocolExtensionRequest extensionRequest,
        InTaskAuthorizationContext resumeAuthorization,
        AgentRequestContext agentRequestContext,
        CancellationToken cancellationToken)
    {
        var context = new TurnContext(this, activity, claimsIdentity);
        context.Services.Set<ITaskStore>(_taskStore);
        if (authentication != null)
        {
            context.Services.Set(authentication);
        }
        if (a2aContext != null)
        {
            context.Services.Set(a2aContext);
        }
        if (a2aEventQueue != null)
        {
            context.Services.Set(a2aEventQueue);
        }
        if (extensionRequest != null)
        {
            context.Services.Set(extensionRequest);
        }
        if (resumeAuthorization != null)
        {
            context.Services.Set(resumeAuthorization);
        }
        if (agentRequestContext != null)
        {
            context.Services.Set(agentRequestContext);
        }
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        return null;
    }

    internal static void RegisterContinuation(string requestId, AgentRequestContext context)
    {
        _a2aAgentContext[requestId] = context;
    }

    private static void RemoveAgentContext(AgentRequestContext context)
    {
        foreach (var entry in _a2aAgentContext.Where(entry => ReferenceEquals(entry.Value, context)).ToList())
        {
            _a2aAgentContext.TryRemove(entry.Key, out _);
        }
    }

    private static void ApplyActivatedExtensions(AgentRequestContext context, HttpResponse response)
    {
        if (context.ExtensionRequest.IsActivated(InTaskAuthorizationExtension.Uri))
        {
            response.Headers[A2AProtocolExtensionRequest.HeaderName] = InTaskAuthorizationExtension.Uri;
        }
    }

    private static async Task<JsonRpcResponseResult> ProcessExtensionJsonRpcAsync(
        AgentRequestContext agentContext,
        JsonRpcId requestId,
        string method,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(method, InTaskAuthorizationExtension.ResumeAuthOperation, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var request = parameters?.Deserialize<ResumeAuthRequest>(A2AJsonUtilities.DefaultOptions)
                ?? throw new A2AException("Invalid resumeAuth parameters.", A2AErrorCode.InvalidParams);
            await SetResumeAuthorizationAsync(agentContext, agentContext.HttpRequest, request, cancellationToken).ConfigureAwait(false);
            var result = await agentContext.Adapter.ResumeTaskAsync(agentContext, request, cancellationToken).ConfigureAwait(false);
            return new JsonRpcResponseResult(JsonRpcResponse.CreateJsonRpcResponse(requestId, result));
        }
        finally
        {
            RemoveAgentContext(agentContext);
        }
    }

    private async Task<AgentTask> ResumeTaskAsync(
        AgentRequestContext agentContext,
        ResumeAuthRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _taskStore.GetTaskAsync(request.TaskId, cancellationToken).ConfigureAwait(false)
            ?? throw new A2AException($"Task '{request.TaskId}' was not found.", A2AErrorCode.TaskNotFound);
        if (task.Status?.State != TaskState.AuthRequired
            || !string.Equals(task.ContextId, request.ContextId, StringComparison.Ordinal))
        {
            throw new A2AException("The resumeAuth request does not match an authorization-required task.", A2AErrorCode.InvalidParams);
        }

        var message = CreateResumeMessage(request).Message;
        var requestContext = new RequestContext
        {
            Message = message,
            Task = task,
            TaskId = task.Id,
            ContextId = task.ContextId,
            ClientProvidedContextId = true,
            StreamingResponse = false,
        };
        var eventQueue = new AgentEventQueue();
        var execution = ExecuteAndCompleteAsync(agentContext, requestContext, eventQueue, cancellationToken);
        var producedEvent = false;

        try
        {
            await foreach (var response in eventQueue.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                producedEvent = true;
                task = TaskProjection.Apply(task, response);
            }
        }
        finally
        {
            await execution.ConfigureAwait(false);
        }

        if (!producedEvent)
        {
            throw new A2AException("Agent handler did not produce any response events.", A2AErrorCode.InvalidAgentResponse);
        }
        if (task.Status.State == TaskState.Working)
        {
            task.Status.State = TaskState.Completed;
            task.Status.Timestamp = DateTimeOffset.UtcNow;
        }

        await _taskStore.SaveTaskAsync(task.Id, task, cancellationToken).ConfigureAwait(false);
        return task;
    }

    private static async Task ExecuteAndCompleteAsync(
        AgentRequestContext agentContext,
        RequestContext requestContext,
        AgentEventQueue eventQueue,
        CancellationToken cancellationToken)
    {
        try
        {
            await agentContext.ExecuteAsync(requestContext, eventQueue, cancellationToken).ConfigureAwait(false);
            eventQueue.Complete();
        }
        catch (Exception ex)
        {
            eventQueue.Complete(ex);
            throw;
        }
    }

    private static async Task SetResumeAuthorizationAsync(
        AgentRequestContext context,
        HttpRequest request,
        ResumeAuthRequest resumeRequest,
        CancellationToken cancellationToken)
    {
        if (!context.ExtensionRequest.IsActivated(InTaskAuthorizationExtension.Uri))
        {
            throw new A2AException("The in-task authorization extension must be activated.", A2AErrorCode.UnsupportedOperation);
        }
        if (string.IsNullOrWhiteSpace(resumeRequest.TaskId)
            || string.IsNullOrWhiteSpace(resumeRequest.ContextId)
            || string.IsNullOrWhiteSpace(resumeRequest.AuthorizationRequestId))
        {
            throw new A2AException(
                "resumeAuth requires taskId, contextId, and authorizationRequestId.",
                A2AErrorCode.InvalidParams);
        }

        var header = request.Headers[InTaskAuthorizationExtension.CredentialHeader];
        if (header.Count != 1
            || !AuthenticationHeaderValue.TryParse(header[0], out var credential)
            || !string.Equals(credential.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(credential.Parameter))
        {
            throw new A2AException("A2A-InTask-Authorization must contain one Bearer credential.", A2AErrorCode.InvalidRequest);
        }

        context.ResumeAuthorization = new InTaskAuthorizationContext
        {
            TaskId = resumeRequest.TaskId,
            ContextId = resumeRequest.ContextId,
            AuthorizationRequestId = resumeRequest.AuthorizationRequestId,
            AccessToken = credential.Parameter,
            CredentialValidated = await ValidateInTaskCredentialAsync(
                context,
                request,
                credential.Parameter,
                cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<bool> ValidateInTaskCredentialAsync(
        AgentRequestContext context,
        HttpRequest request,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.Equals(context.Authentication.AccessToken, accessToken, StringComparison.Ordinal))
        {
            return true;
        }

        var services = request.HttpContext.Features.Get<IServiceProvidersFeature>()?.RequestServices;
        var authentication = services?.GetService<IAuthenticationService>();
        var schemes = services?.GetService<IAuthenticationSchemeProvider>();
        var scheme = schemes == null
            ? null
            : await schemes.GetDefaultAuthenticateSchemeAsync().ConfigureAwait(false);
        if (authentication == null || scheme == null)
        {
            return false;
        }

        var validationContext = new DefaultHttpContext
        {
            RequestServices = services,
        };
        validationContext.Request.Scheme = request.Scheme;
        validationContext.Request.Host = request.Host;
        validationContext.Request.PathBase = request.PathBase;
        validationContext.Request.Path = request.Path;
        validationContext.Request.QueryString = request.QueryString;
        validationContext.Request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken).ToString();

        var result = await authentication.AuthenticateAsync(validationContext, scheme.Name).ConfigureAwait(false);
        return result.Succeeded;
    }

    private static SendMessageRequest CreateResumeMessage(ResumeAuthRequest request)
    {
        return new SendMessageRequest
        {
            Message = new Message
            {
                TaskId = request.TaskId,
                ContextId = request.ContextId,
                Extensions = [InTaskAuthorizationExtension.Uri],
                Parts = [new Part { Data = JsonSerializer.SerializeToElement(new { resumeAuth = true }) }],
            },
        };
    }

    /// <inheritdoc/>
    public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        if (!_a2aAgentContext.TryGetValue(turnContext.Activity.RequestId, out var agentContext))
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AgentRequestContextMissing,
                null,
                turnContext.Activity.RequestId);
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
    public A2ARequestAuthentication Authentication { get; }
    public AgentEventQueue EventQueue { get; private set; }
    public ILogger Logger { get; }
    public HttpRequest HttpRequest { get; }
    public A2AProtocolExtensionRequest ExtensionRequest { get; }
    public InTaskAuthorizationContext ResumeAuthorization { get; set; }
    public RequestContext CurrentContext { get; private set; }

    public AgentRequestContext(HttpRequest httpRequest, A2AAdapter adapter, IAgent agent, ILogger logger)
    {
        Adapter = adapter;
        HttpRequest = httpRequest;
        ExtensionRequest = A2AProtocolExtensionRequest.Create(httpRequest);
        Agent = agent;
        Authentication = A2ARequestAuthentication.Create(httpRequest);
        Identity = Authentication.Identity;
        RequestId = httpRequest.HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();
        Logger = logger;
    }

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        EventQueue = eventQueue;
        CurrentContext = context;

        if (!context.IsContinuation)
        {
            var taskUpdater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
            await taskUpdater.SubmitAsync(cancellationToken).ConfigureAwait(false);
        }

        await Adapter.ExecuteAgentTurnAsync(this, context, eventQueue, cancellationToken);
    }

    public async Task CancelAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
        await updater.CancelAsync(cancellationToken).ConfigureAwait(false);
        await Adapter.ExecuteAgentCancelTaskAsync(RequestId, Identity, Authentication, Agent, context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        foreach (var activity in activities)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Log.LogResponse(Logger, turnContext.Activity.Conversation.Id, ProtocolJsonSerializer.ToJson(activity));
            }

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

    private static Message GetIncomingMessage(ITurnContext turnContext)
        => A2AActivity.GetMessage(turnContext.Activity)
            ?? throw new InvalidOperationException("The A2A message is unavailable.");

    private async Task OnMessageResponse(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = GetIncomingMessage(turnContext);
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
    }

    private async Task OnStreamingResponse(ITurnContext turnContext, IActivity activity, StreamInfo entity, CancellationToken cancellationToken = default)
    {
        var incomingMessage = GetIncomingMessage(turnContext);
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
        var incomingMessage = GetIncomingMessage(turnContext);

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
    }
}

static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "A2A turn with requestId '{RequestId}', and Activity: {Activity}")]
    public static partial void LogRequest(ILogger logger, string requestId, string activity);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "A2A Response with requestId '{RequestId}', and Activity: {Activity}")]
    public static partial void LogResponse(ILogger logger, string requestId, string activity);
}
