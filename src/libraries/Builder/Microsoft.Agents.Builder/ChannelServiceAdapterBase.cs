// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Connector;
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
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(activities, nameof(activities));

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
                    if (!await StreamedResponseAsync(turnContext.Activity, activity, cancellationToken).ConfigureAwait(false))
                    {
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
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            var connectorClient = turnContext.Services.Get<IConnectorClient>();
            return await connectorClient.Conversations.UpdateActivityAsync(activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            var connectorClient = turnContext.Services.Get<IConnectorClient>();
            await connectorClient.Conversations.DeleteActivityAsync(reference.Conversation.Id, reference.ActivityId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<ConversationReference> CreateConversationAsync(ITurnContext turnContext, ConversationParameters conversationParameters, string channelId = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(conversationParameters, nameof(conversationParameters));

            // Make the actual create conversation call using the connector.
            var createConversationResult = await turnContext.Services.Get<IConnectorClient>().Conversations.CreateConversationAsync(conversationParameters, cancellationToken).ConfigureAwait(false);

            return new ConversationReference
            {
                ActivityId = createConversationResult.ActivityId,
                User = turnContext.Activity.From,
                Agent = conversationParameters.Agent,
                Conversation = new ConversationAccount()
                {
                    Id = createConversationResult.Id,
                    TenantId = conversationParameters.TenantId ?? turnContext.Activity.Conversation.TenantId
                },
                ChannelId = channelId ?? turnContext.Activity.ChannelId,
                Locale = turnContext.Activity.Locale,
                ServiceUrl = turnContext.Activity.ServiceUrl,
            };
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            return ProcessProactiveAsync(claimsIdentity, reference.GetContinuationActivity(), callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent agent, CancellationToken cancellationToken = default)
        {
            return ProcessProactiveAsync(claimsIdentity, continuationActivity, agent.OnTurnAsync, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            ValidateContinuationActivity(continuationActivity);

            var audience = AgentClaims.GetTokenAudience(claimsIdentity);
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

        /// <summary>
        /// This is a helper to create the ClaimsIdentity structure from an appId that will be added to the TurnContext.
        /// It is intended for use in proactive and named-pipe scenarios.
        /// </summary>
        /// <param name="agentAppId">The Agent's application id.</param>
        /// <returns>A <see cref="ClaimsIdentity"/> with the audience and appId claims set to the appId.</returns>
        protected static ClaimsIdentity CreateClaimsIdentity(string agentAppId)
        {
            agentAppId ??= string.Empty;

            // Hand craft Claims Identity.
            return new ClaimsIdentity(
            [
                // Adding claims for both Emulator and Channel.
                new(AuthenticationConstants.AudienceClaim, agentAppId),
                new(AuthenticationConstants.AppIdClaim, agentAppId),
            ]);
        }

        protected virtual Task<bool> StreamedResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        private TurnContext CreateTurnContext(IActivity activity, ClaimsIdentity claimsIdentity, IConnectorClient connectorClient, IUserTokenClient userTokenClient, AgentCallbackHandler callback)
        {
            var turnContext = new TurnContext(this, activity);

            turnContext.Identity = claimsIdentity;
            if (connectorClient != null)
                turnContext.Services.Set(connectorClient);
            turnContext.Services.Set(userTokenClient);
            turnContext.Services.Set(ChannelServiceFactory);

            return turnContext;
        }

        private static void ValidateContinuationActivity(IActivity continuationActivity)
        {
            _ = continuationActivity ?? throw new ArgumentNullException(nameof(continuationActivity));
            _ = continuationActivity.Conversation ?? throw new ArgumentException("The continuation Activity should contain a Conversation value.");
            _ = continuationActivity.ServiceUrl ?? throw new ArgumentException("The continuation Activity should contain a ServiceUrl value.");
        }

        private static InvokeResponse ProcessTurnResults(TurnContext turnContext)
        {
            // Handle ExpectedReplies scenarios where the all the activities have been buffered and sent back at once in an invoke response.
            if (turnContext.Activity.DeliveryMode == DeliveryModes.ExpectReplies)
            {
                return new InvokeResponse { Status = (int)HttpStatusCode.OK, Body = new ExpectedReplies(turnContext.BufferedReplyActivities) };
            }

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
