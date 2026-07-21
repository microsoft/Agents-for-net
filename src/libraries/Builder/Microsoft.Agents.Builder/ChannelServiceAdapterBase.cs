// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.Telemetry.Adapter.Scopes;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// An adapter that implements the Activity Protocol and can be hosted in different cloud environments both public and private.
    /// </summary>
    /// <remarks>
    /// ChannelServiceAdapterBase is designed for interacting with a "channel service" that uses an IConnectorClient by way of
    /// the IChannelServiceClientFactory to send and receive activities.  This is the case for Azure Bot Service, and other SDK Agents.  
    /// If your adapter needs to interact with a channel service like this, you can inherit from ChannelServiceAdapterBase and get a 
    /// lot of functionality for free, including handling incoming activities, sending outgoing activities, and creating conversations.
    /// Otherwise, subclass the ChannelAdapter for more control over how activities are sent and received.
    /// </remarks>
    /// <param name="channelServiceClientFactory">The IChannelServiceClientFactory to use for creating IConnectorClient and IUserTokenClient instances.</param>
    /// <param name="logger">The ILogger implementation this adapter should use.</param>
    /// <param name="serviceProvider">Optional service provider used to instantiate per-channel <see cref="IStreamingResponseFactory"/> implementations discovered via <see cref="StreamingResponseFactoryAttribute"/>, to assign a channel-specific <see cref="IStreamingResponse"/> to each turn.</param>
    public abstract class ChannelServiceAdapterBase(
        IChannelServiceClientFactory channelServiceClientFactory,
        ILogger logger = null,
        IServiceProvider serviceProvider = null) : ChannelAdapter(logger)
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        // Per-channel cache of resolved factories, so each factory is instantiated at most once per adapter.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IStreamingResponseFactory> _streamingResponseFactories =
            new(StringComparer.OrdinalIgnoreCase);

        static ChannelServiceAdapterBase()
        {
            // Register the AssemblyLoad handler early (at first adapter type use, i.e. host startup) so that
            // extension assemblies loaded later - including during activity deserialization - are scanned for
            // their [StreamingResponseFactory] implementations before the pipeline runs.
            StreamingResponseFactoryCatalog.EnsureInitialized();
        }

        /// <summary>
        /// Gets the <see cref="Microsoft.Agents.Builder.IChannelServiceClientFactory" /> instance for this adapter.
        /// </summary>
        /// <value>
        /// The <see cref="Microsoft.Agents.Builder.IChannelServiceClientFactory" /> instance for this adapter.
        /// </value>
        protected IChannelServiceClientFactory ChannelServiceFactory { get; private set; } = channelServiceClientFactory ?? throw new ArgumentNullException(nameof(channelServiceClientFactory));

        /// <inheritdoc/>
        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            _ = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _ = activities ?? throw new ArgumentNullException(nameof(activities));

            if (activities.Length == 0)
            {
                throw new ArgumentException("Expecting one or more activities, but the array was empty.", nameof(activities));
            }

            using var telemetryScope = new ScopeSendActivities(activities);

            var responses = new ResourceResponse[activities.Length];

            for (var index = 0; index < activities.Length; index++)
            {
                var activity = activities[index];

                activity.Id = null;
                var response = default(ResourceResponse);

                if (activity.Type == ActivityTypes.InvokeResponse)
                {
                    turnContext.StackState.Set(InvokeResponseKey, activity);
                }
                else if (activity.Type == ActivityTypes.Trace && activity.ChannelId != Channels.Emulator)
                {
                    // no-op
                }
                else
                {
                    if (!await HostResponseAsync(turnContext.Activity, activity, cancellationToken).ConfigureAwait(false))
                    {
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            ChannelServiceAdapterLog.LogTurnResponse(Logger, activity.RequestId, ProtocolJsonSerializer.ToJson(activity));
                        }

                        // Respond via ConnectorClient
                        if (!string.IsNullOrWhiteSpace(activity.ReplyToId))
                        {
                            var connectorClient = turnContext.Services.Get<IConnectorClient>();
                            response = await connectorClient.Conversations.ReplyToActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            var connectorClient = turnContext.Services.Get<IConnectorClient>();
                            response = await connectorClient.Conversations.SendToConversationAsync(activity, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                response ??= new ResourceResponse(activity.Id ?? string.Empty);

                responses[index] = response;
            }

            return responses;
        }

        /// <inheritdoc/>
        public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
        {
            _ = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _ = activity ?? throw new ArgumentNullException(nameof(activity));

            using var telemetryScope = new ScopeUpdateActivity(activity);

            var connectorClient = turnContext.Services.Get<IConnectorClient>();
            return connectorClient.Conversations.UpdateActivityAsync(activity, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            _ = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _ = reference ?? throw new ArgumentNullException(nameof(reference));

            using var telemetryScope = new ScopeDeleteActivity(reference.GetContinuationActivity());

            var connectorClient = turnContext.Services.Get<IConnectorClient>();
            return connectorClient.Conversations.DeleteActivityAsync(reference.Conversation.Id, reference.ActivityId, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task CreateConversationAsync(string agentAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(conversationParameters, nameof(conversationParameters));
            AssertionHelpers.ThrowIfNull(callback, nameof(callback));

            // Create a ClaimsIdentity, to create the connector and for adding to the turn context.
            var createOptions = CreateConversationOptionsBuilder.Create(agentAppId, channelId, serviceUrl: serviceUrl, parameters: conversationParameters)
                .WithAudience(audience)
                .WithUser((conversationParameters.Members?.Count > 0 ? conversationParameters.Members[0] : new ChannelAccount(agentAppId, role: RoleTypes.User)))
                .Build();
            return CreateConversationAsync(createOptions.Identity, createOptions.ChannelId, createOptions.ServiceUrl, createOptions.Audience, createOptions.Parameters, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<ConversationReference> CreateConversationAsync(ClaimsIdentity identity, string channelId, string serviceUrl, string audience, ConversationParameters parameters, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(parameters, nameof(parameters));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));

            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(identity);

            var reference = ConversationReferenceBuilder.Create(identity.GetIncomingAudience(), channelId, serviceUrl)
                .WithUser(parameters.Members?.Count > 0 ? parameters.Members[0] : new ChannelAccount(identity.GetIncomingAudience(), role: RoleTypes.User))
                .Build();

            // Create the initial TurnContext with the create conversation activity, so that we can create the connector client
            // with the correct context and then make the create conversation call.
            var createActivity = reference.GetCreateContinuationActivity(channelData: parameters.ChannelData);
            using var context = new TurnContext(this, createActivity, identity);

            // Create the connector client to use for outbound requests.
            using var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(context, audience, null, useAnonymousAuthCallback, cancellationToken).ConfigureAwait(false);

            // Make the actual create conversation call using the connector.
            var createConversationResult = await connectorClient.Conversations.CreateConversationAsync(parameters, cancellationToken).ConfigureAwait(false);

            // Update the TurnContext with the results from the create conversation call.
            context.Activity.Conversation = new ConversationAccount(id: createConversationResult.Id, tenantId: parameters.TenantId);

            if (callback != null)
            {
                // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
                using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(identity, useAnonymous: useAnonymousAuthCallback, cancellationToken: cancellationToken).ConfigureAwait(false);

                // Create a turn context and run the pipeline.

                SetTurnContextServices(context, connectorClient, userTokenClient);

                // Run the pipeline.
                await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
            }

            return createActivity.GetConversationReference();
        }

        public override Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent agent, CancellationToken cancellationToken, string audience = null)
        {
            return ProcessProactiveAsync(claimsIdentity, continuationActivity, audience, agent.OnTurnAsync, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
            AssertionHelpers.ThrowIfNull(callback, nameof(callback));

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                ChannelServiceAdapterLog.LogProcessProactive(Logger, ProtocolJsonSerializer.ToJson(continuationActivity));
            }

            ValidateContinuationActivity(continuationActivity);

            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(claimsIdentity);

            // Create a turn context and clients
            using var context = new TurnContext(this, continuationActivity, claimsIdentity);

            // Create the connector client to use for outbound requests.
            using var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(
                context,
                audience,
                useAnonymous: useAnonymousAuthCallback,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, useAnonymous: useAnonymousAuthCallback, cancellationToken: cancellationToken).ConfigureAwait(false);

            SetTurnContextServices(context, connectorClient, userTokenClient);

            // Run the pipeline.
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                ChannelServiceAdapterLog.LogProcessActivity(Logger, activity.RequestId, callback.Target?.ToString() ?? callback.Method.Name, ProtocolJsonSerializer.ToJson(activity));
            }

            if (AgentClaims.IsAgentClaim(claimsIdentity))
            {
                activity.CallerId = $"{CallerIdConstants.AgentPrefix}{AgentClaims.GetOutgoingAppIdClaim(claimsIdentity)}";
            }
            else
            {
                //activity.CallerId = ???
            }

            // If auth is disabled, and we don't have any
            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(claimsIdentity);
            if (useAnonymousAuthCallback)
            {
                if (IsTrustedLocalTransport(activity))
                {
                    ChannelServiceAdapterLog.LogAnonymousAccessTrustedTransport(Logger, activity.ChannelId);
                }
                else
                {
                    ChannelServiceAdapterLog.LogAnonymousAccess(Logger, activity.ChannelId);
                }
            }

            // Create a turn context and clients
            using var context = new TurnContext(this, activity, claimsIdentity);

            // Create the connector client to use for outbound requests.
            using IConnectorClient connectorClient =
                ResolveIfConnectorClientIsNeeded(activity)  // if Delivery Mode == ExpectReplies, we don't need a connector client.
                    ? await ChannelServiceFactory.CreateConnectorClientAsync(
                        context,
                        useAnonymous: useAnonymousAuthCallback,
                        cancellationToken: cancellationToken).ConfigureAwait(false)
                    : null;

            // Create a UserTokenClient instance for OAuth flow.
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, useAnonymous: useAnonymousAuthCallback, cancellationToken: cancellationToken).ConfigureAwait(false);

            SetTurnContextServices(context, connectorClient, userTokenClient);

            // Run the pipeline.
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);

            // If there are any results they will have been left on the TurnContext. 
            return ProcessTurnResults(context);
        }

        protected virtual Task<bool> HostResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            // ChannelServiceAdapterBase can't handle Stream or ExpectReplies.  Keep SendActivities from trying to send via ConnectorClient.
            return Task.FromResult(incomingActivity?.DeliveryMode == DeliveryModes.Stream || incomingActivity?.DeliveryMode == DeliveryModes.ExpectReplies);
        }

        private void SetTurnContextServices(TurnContext turnContext, IConnectorClient connectorClient, IUserTokenClient userTokenClient)
        {
            if (connectorClient != null)
                turnContext.Services.Set(connectorClient);
            if (userTokenClient != null)
                turnContext.Services.Set(userTokenClient);
            turnContext.Services.Set(ChannelServiceFactory);

            ApplyStreamingResponseFactory(turnContext);
        }

        /// <summary>
        /// Assigns a channel-specific <see cref="IStreamingResponse"/> to the turn when a factory is registered
        /// for the incoming channel (via <see cref="StreamingResponseFactoryAttribute"/>).  When no factory is
        /// registered, the default <see cref="StreamingResponse"/> is used (created lazily by
        /// <see cref="TurnContext"/>).
        /// </summary>
        private void ApplyStreamingResponseFactory(TurnContext turnContext)
        {
            var channel = turnContext.Activity?.ChannelId?.Channel;
            if (_serviceProvider == null || channel == null)
            {
                return;
            }

            var factory = _streamingResponseFactories.GetOrAdd(channel, CreateStreamingResponseFactory);
            if (factory == null)
            {
                return;
            }

            try
            {
                turnContext.SetStreamingResponse(factory.Create(turnContext));
            }
            catch (Exception ex)
            {
                // A misbehaving factory must not fail the turn; fall back to the default StreamingResponse.
                ChannelServiceAdapterLog.LogStreamingResponseFactoryError(Logger, ex, channel);
            }
        }

        private IStreamingResponseFactory CreateStreamingResponseFactory(string channel)
        {
            if (!StreamingResponseFactoryCatalog.TryGetFactoryType(channel, out var factoryType))
            {
                return null;
            }

            try
            {
                // Instantiate the factory from the service provider so its dependencies (e.g. IHttpClientFactory,
                // IConfiguration) are injected.  Cached per channel by the caller.
                return (IStreamingResponseFactory)ActivatorUtilities.CreateInstance(_serviceProvider, factoryType);
            }
            catch (Exception ex)
            {
                // Optional, reflection/source-gen discovered factories may fail to instantiate (missing DI
                // registrations, invalid type, etc.).  Cache the null result and fall back to the default.
                ChannelServiceAdapterLog.LogStreamingResponseFactoryError(Logger, ex, channel);
                return null;
            }
        }

        private static void ValidateContinuationActivity(IActivity continuationActivity)
        {
            _ = continuationActivity ?? throw new ArgumentNullException(nameof(continuationActivity));
            _ = continuationActivity.Conversation ?? throw Core.Errors.ExceptionHelper.GenerateException<ArgumentNullException>(ErrorHelper.ProactiveInvalidConversationAccount, null);
        }

        /// <summary>
        /// Determines whether a connector client is needed based on the delivery mode and service URL of the given activity.
        /// </summary>
        /// <param name="activity">The activity to evaluate.</param>
        /// <returns>
        /// <c>true</c> if a connector client is needed; otherwise, <c>false</c>.
        /// A connector client is required if the activity's delivery mode is not "ExpectReplies" or "Stream" 
        /// and the service URL is not null or empty.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="activity"/> is null.</exception>
        private static bool ResolveIfConnectorClientIsNeeded(IActivity activity)
        {
            Microsoft.Agents.Core.AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            switch (activity.DeliveryMode)
            {
                case DeliveryModes.ExpectReplies:
                case DeliveryModes.Stream:
                    if (string.IsNullOrEmpty(activity.ServiceUrl))
                        return false;
                    break;
                default:
                    break;
            }
            return true;
        }

        // Activities arriving over the local-process named pipe transport carry a well-known
        // urn:botframework:namedpipe* service URL set by the hosting library. Anonymous access
        // is the expected mode for that ingress, so we log Information rather than Warning to
        // avoid noisy false alarms.
        private static bool IsTrustedLocalTransport(IActivity activity)
        {
            return TransportConstants.IsNamedPipeServiceUrl(activity?.ServiceUrl);
        }
    }
}
