// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// An adapter that implements the Activity Protocol and can be hosted in different cloud environments both public and private.
    /// </summary>
    /// <param name="channelServiceClientFactory">The IConnectorFactory to use.</param>
    /// <param name="logger">The ILogger implementation this adapter should use.</param>
    public abstract class ChannelServiceAdapterBase(
        IChannelServiceClientFactory channelServiceClientFactory,
        ILogger logger = null) : ChannelAdapter(logger)
    {
        /// <summary>
        /// Gets the <see cref="IChannelServiceClientFactory" /> instance for this adapter.
        /// </summary>
        /// <value>
        /// The <see cref="IChannelServiceClientFactory" /> instance for this adapter.
        /// </value>
        protected IChannelServiceClientFactory ChannelServiceFactory { get; private set; } = channelServiceClientFactory ?? throw new ArgumentNullException(nameof(channelServiceClientFactory));

        /// <summary>
        /// Gets a <see cref="ILogger" /> to use within this adapter and its subclasses.
        /// </summary>
        /// <value>
        /// The <see cref="ILogger" /> instance for this adapter.
        /// </value>
        protected ILogger Logger { get; private set; } = logger ?? NullLogger.Instance;

        /// <inheritdoc/>
        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            _ = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _ = activities ?? throw new ArgumentNullException(nameof(activities));

            if (activities.Length == 0)
            {
                throw new ArgumentException("Expecting one or more activities, but the array was empty.", nameof(activities));
            }

            var responses = new ResourceResponse[activities.Length];

            for (var index = 0; index < activities.Length; index++)
            {
                var activity = activities[index];

                activity.Id = null;
                var response = default(ResourceResponse);

                if (activity.Type == ActivityType.InvokeResponse)
                {
                    turnContext.StackState.Set(InvokeResponseKey, activity);
                }
                else if (activity.Type == ActivityType.Trace && activity.ChannelId != Channels.Emulator)
                {
                    // no-op
                }
                else
                {
                    if (!await HostResponseAsync(turnContext.Activity, activity, cancellationToken).ConfigureAwait(false))
                    {
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
        public override async Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
        {
            _ = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _ = activity ?? throw new ArgumentNullException(nameof(activity));

            var connectorClient = turnContext.Services.Get<IConnectorClient>();
            return await connectorClient.Conversations.UpdateActivityAsync(activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            _ = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _ = reference ?? throw new ArgumentNullException(nameof(reference));

            var connectorClient = turnContext.Services.Get<IConnectorClient>();
            await connectorClient.Conversations.DeleteActivityAsync(reference.Conversation.Id, reference.ActivityId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(string agentAppId, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(agentAppId, nameof(agentAppId));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            return ProcessProactiveAsync(AgentClaims.CreateIdentity(agentAppId), reference.GetContinuationActivity(), null, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            return ProcessProactiveAsync(claimsIdentity, reference.GetContinuationActivity(), AgentClaims.GetTokenAudience(claimsIdentity), callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(string agentAppId, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(agentAppId, nameof(agentAppId));

            return ProcessProactiveAsync(AgentClaims.CreateIdentity(agentAppId), continuationActivity, null, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            return ProcessProactiveAsync(claimsIdentity, continuationActivity, null, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            return ProcessProactiveAsync(claimsIdentity, reference.GetContinuationActivity(), audience, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            return ProcessProactiveAsync(claimsIdentity, continuationActivity, audience, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task CreateConversationAsync(string agentAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(serviceUrl))
            {
                throw new ArgumentNullException(nameof(serviceUrl));
            }

            _ = conversationParameters ?? throw new ArgumentNullException(nameof(conversationParameters));
            _ = callback ?? throw new ArgumentNullException(nameof(callback));

            // Create a ClaimsIdentity, to create the connector and for adding to the turn context.
            var claimsIdentity = AgentClaims.CreateIdentity(agentAppId);
            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(claimsIdentity);

            // This is really the "From".  User can supply otherwise default to this Agent.
            conversationParameters.Agent ??= new ChannelAccount(id: agentAppId, role: RoleTypes.Agent);

            // Create the connector client to use for outbound requests.
            using (var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(claimsIdentity, serviceUrl, audience, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false))
            {
                // Make the actual create conversation call using the connector.
                var createConversationResult = await connectorClient.Conversations.CreateConversationAsync(conversationParameters, cancellationToken).ConfigureAwait(false);

                // Create the create activity to communicate the results to the application.
                var createActivity = CreateConversationEvent(createConversationResult, channelId, serviceUrl, conversationParameters);

                // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
                using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

                // Create a turn context and run the pipeline.
                using var context = CreateTurnContext(createActivity, claimsIdentity, connectorClient, userTokenClient, callback);

                // Run the pipeline.
                await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
            }
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

            ValidateContinuationActivity(continuationActivity);

            audience = audience ?? AgentClaims.GetTokenAudience(claimsIdentity);
            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(claimsIdentity);

            // Create the connector client to use for outbound requests.
            using var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(claimsIdentity, continuationActivity.ServiceUrl, audience, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

            // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

            // Create a turn context and run the pipeline.
            using var context = CreateTurnContext(continuationActivity, claimsIdentity, connectorClient, userTokenClient, callback);

            // Run the pipeline.
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            Logger.LogInformation($"ProcessActivityAsync");

            if (AgentClaims.IsAgentClaim(claimsIdentity))
            {
                activity.CallerId = $"{CallerIdConstants.AgentPrefix}{AgentClaims.GetOutgoingAppId(claimsIdentity)}";
            }
            else
            {
                //activity.CallerId = ???
            }

            // If auth is disabled, and we don't have any
            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(claimsIdentity);
            if (useAnonymousAuthCallback)
            {
                Logger.LogWarning("Anonymous access is enabled for channel: {ChannelId}.", activity.ChannelId);
            }

            // Create the connector client to use for outbound requests.
            using IConnectorClient connectorClient =
                ResolveIfConnectorClientIsNeeded(activity) ?  // if Delivery Mode == ExpectReplies, we don't need a connector client.
                    await ChannelServiceFactory.CreateConnectorClientAsync(
                    claimsIdentity,
                    activity.ServiceUrl,
                    AgentClaims.GetTokenAudience(claimsIdentity),
                    cancellationToken,
                    scopes: AgentClaims.GetTokenScopes(claimsIdentity),
                    useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false)
                    : null;

            // Create a UserTokenClient instance for OAuth flow.
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

            // Create a turn context and run the pipeline.
            using var context = CreateTurnContext(activity, claimsIdentity, connectorClient, userTokenClient, callback);

            // Run the pipeline.
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);

            // If there are any results they will have been left on the TurnContext. 
            return ProcessTurnResults(context);

        }

        [Obsolete("Use HostResponseAsync")]
        protected virtual Task<bool> StreamedResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            return HostResponseAsync(incomingActivity, outActivity, cancellationToken);
        }

        protected virtual Task<bool> HostResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            // ChannelServiceAdapterBase can't handle Stream or ExpectReplies.  Keep SendActivities from trying to send via ConnectorClient.
            return Task.FromResult(incomingActivity?.DeliveryMode == DeliveryModes.Stream || incomingActivity?.DeliveryMode == DeliveryModes.ExpectReplies);
        }

        private static Activity CreateConversationEvent(ConversationResourceResponse createConversationResult, string channelId, string serviceUrl, ConversationParameters conversationParameters)
        {
            // Create a conversation update activity to represent the TurnContext.Activity context.
            var activity = Activity.CreateEventActivity();
            activity.Name = ActivityEventNames.CreateConversation;
            activity.ChannelId = channelId;
            activity.ServiceUrl = serviceUrl;
            activity.Conversation = new ConversationAccount(id: createConversationResult.Id, tenantId: conversationParameters.TenantId);
            activity.ChannelData = conversationParameters.ChannelData;
            activity.Recipient = conversationParameters.Agent;
            activity.From = conversationParameters.Agent;
            activity.Value = createConversationResult;
            return (Activity)activity;
        }

        private TurnContext CreateTurnContext(IActivity activity, ClaimsIdentity claimsIdentity, IConnectorClient connectorClient, IUserTokenClient userTokenClient, AgentCallbackHandler callback)
        {
            var turnContext = new TurnContext(this, activity);

            turnContext.Identity = claimsIdentity;
            if (connectorClient != null)
                turnContext.Services.Set(connectorClient);
            if (userTokenClient != null)
                turnContext.Services.Set(userTokenClient);
            turnContext.Services.Set(ChannelServiceFactory);

            return turnContext;
        }

        private static void ValidateContinuationActivity(IActivity continuationActivity)
        {
            _ = continuationActivity ?? throw new ArgumentNullException(nameof(continuationActivity));
            _ = continuationActivity.Conversation ?? throw new ArgumentException("The continuation Activity should contain a Conversation value.");
        }

        private static InvokeResponse ProcessTurnResults(TurnContext turnContext)
        {
            // Handle Invoke scenarios where the Agent will return a specific body and return code.
            if (turnContext.Activity.Type == ActivityType.Invoke)
            {
                var activityInvokeResponse = turnContext.StackState.Get<Activity>(InvokeResponseKey);
                if (activityInvokeResponse == null)
                {
                    return new InvokeResponse { Status = (int)HttpStatusCode.NotImplemented };
                }

                return (InvokeResponse)activityInvokeResponse.Value;
            }

            // No body to return.
            return null;
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
    }
}
