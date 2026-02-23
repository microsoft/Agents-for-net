// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

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
                            Logger.LogDebug("Turn Response: RequestId={RequestId}, Activity='{Activity}'", activity.RequestId, ProtocolJsonSerializer.ToJson(activity));
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
        public override Task ContinueConversationAsync(string agentAppId, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(agentAppId, nameof(agentAppId));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            return ProcessProactiveAsync(AgentClaims.CreateIdentity(agentAppId), reference.GetContinuationActivity(), null, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            return ProcessProactiveAsync(claimsIdentity, reference.GetContinuationActivity(), AgentClaims.GetTokenAudience(claimsIdentity), callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(string agentAppId, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(agentAppId, nameof(agentAppId));

            return ProcessProactiveAsync(AgentClaims.CreateIdentity(agentAppId), continuationActivity, null, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            return ProcessProactiveAsync(claimsIdentity, continuationActivity, null, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            return ProcessProactiveAsync(claimsIdentity, reference.GetContinuationActivity(), audience, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            return ProcessProactiveAsync(claimsIdentity, continuationActivity, audience, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task CreateConversationAsync(string agentAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(conversationParameters, nameof(conversationParameters));
            AssertionHelpers.ThrowIfNull(callback, nameof(callback));

            // Create a ClaimsIdentity, to create the connector and for adding to the turn context.
            var createRecord = CreateConversationBuilder.Create(agentAppId, channelId, serviceUrl: serviceUrl, parameters: conversationParameters).WithScope(audience).Build();
            return CreateConversationAsync(createRecord.Conversation.Identity, createRecord.Conversation.Reference, createRecord.Parameters, createRecord.Scope, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<ConversationReference> CreateConversationAsync(ClaimsIdentity identity, ConversationReference reference, ConversationParameters parameters, string scope, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));
            AssertionHelpers.ThrowIfNull(parameters, nameof(parameters));

            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(identity);

            // Create the connector client to use for outbound requests.
            using var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(
                identity,
                reference.ServiceUrl,
                scope,
                cancellationToken,
                useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

            // Make the actual create conversation call using the connector.
            var createConversationResult = await connectorClient.Conversations.CreateConversationAsync(parameters, cancellationToken).ConfigureAwait(false);

            // Create the create activity to communicate the results to the application.
            var createActivity = reference.GetCreateContinuationActivity(new ConversationAccount(id: createConversationResult.Id, tenantId: parameters.TenantId), parameters.ChannelData);

            if (callback != null)
            {
                // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
                using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(identity, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

                // Create a turn context and run the pipeline.
                using var context = new TurnContext(this, createActivity, identity);
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
                Logger.LogDebug("ProcessProactive: Activity='{Activity}'", ProtocolJsonSerializer.ToJson(continuationActivity));
            }

            ValidateContinuationActivity(continuationActivity);

            bool useAnonymousAuthCallback = AgentClaims.AllowAnonymous(claimsIdentity);

            // Create a turn context and clients
            using var context = new TurnContext(this, continuationActivity, claimsIdentity);

            // Create the connector client to use for outbound requests.
            using var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(
                context,
                useAnonymous: useAnonymousAuthCallback,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

            SetTurnContextServices(context, connectorClient, userTokenClient);

            // Run the pipeline.
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("ProcessActivity: RequestId={RequestId}, Activity='{Activity}'", activity.RequestId, ProtocolJsonSerializer.ToJson(activity));
            }

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

            // Create a turn context and clients
            using var context = new TurnContext(this, activity, claimsIdentity);

            // Create the connector client to use for outbound requests.
            using IConnectorClient connectorClient =
                ResolveIfConnectorClientIsNeeded(activity)  // if Delivery Mode == ExpectReplies, we don't need a connector client.
                    ? await ChannelServiceFactory.CreateConnectorClientAsync(
                        context,
                        scopes: AgentClaims.GetTokenScopes(claimsIdentity),
                        useAnonymous: useAnonymousAuthCallback,
                        cancellationToken: cancellationToken).ConfigureAwait(false)
                    : null;

            // Create a UserTokenClient instance for OAuth flow.
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

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

        private TurnContext SetTurnContextServices(TurnContext turnContext, IConnectorClient connectorClient, IUserTokenClient userTokenClient)
        {
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
            if (turnContext.Activity.Type == ActivityTypes.Invoke)
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
