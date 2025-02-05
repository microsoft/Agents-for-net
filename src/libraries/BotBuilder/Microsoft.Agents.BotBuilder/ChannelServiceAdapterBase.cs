// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.BotBuilder
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

            Logger.LogInformation($"SendActivitiesAsync for {activities.Length} activities.");

            var responses = new ResourceResponse[activities.Length];

            for (var index = 0; index < activities.Length; index++)
            {
                var activity = activities[index];

                activity.Id = null;
                var response = default(ResourceResponse);

                Logger.LogInformation($"Sending activity.  ReplyToId: {activity.ReplyToId}");

                if (activity.Type == ActivityTypes.Delay)
                {
                    var delayMs = Convert.ToInt32(activity.Value, CultureInfo.InvariantCulture);
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
                else if (activity.Type == ActivityTypes.InvokeResponse)
                {
                    turnContext.TurnState.Add(TurnStateKeys.InvokeResponseKey, activity);
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
                            var connectorClient = turnContext.TurnState.Get<IConnectorClient>();
                            response = await connectorClient.Conversations.ReplyToActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            var connectorClient = turnContext.TurnState.Get<IConnectorClient>();
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

            Logger.LogInformation($"UpdateActivityAsync ActivityId: {activity.Id}");

            var connectorClient = turnContext.TurnState.Get<IConnectorClient>();
            return await connectorClient.Conversations.UpdateActivityAsync(activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            _ = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _ = reference ?? throw new ArgumentNullException(nameof(reference));

            Logger.LogInformation($"DeleteActivityAsync Conversation Id: {reference.Conversation.Id}, ActivityId: {reference.ActivityId}");

            var connectorClient = turnContext.TurnState.Get<IConnectorClient>();
            await connectorClient.Conversations.DeleteActivityAsync(reference.Conversation.Id, reference.ActivityId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(string botAppId, ConversationReference reference, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            _ = reference ?? throw new ArgumentNullException(nameof(reference));

            var claims = CreateClaimsIdentity(botAppId);
            return ProcessProactiveAsync(CreateClaimsIdentity(botAppId), reference.GetContinuationActivity(), BotClaims.GetTokenAudience(claims.Claims), callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            _ = reference ?? throw new ArgumentNullException(nameof(reference));

            return ProcessProactiveAsync(claimsIdentity, reference.GetContinuationActivity(), BotClaims.GetTokenAudience(claimsIdentity.Claims), callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            _ = claimsIdentity ?? throw new ArgumentNullException(nameof(claimsIdentity));
            _ = reference ?? throw new ArgumentNullException(nameof(reference));
            _ = callback ?? throw new ArgumentNullException(nameof(callback));

            return ProcessProactiveAsync(claimsIdentity, reference.GetContinuationActivity(), audience, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(string botAppId, IActivity continuationActivity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            _ = callback ?? throw new ArgumentNullException(nameof(callback));
            ValidateContinuationActivity(continuationActivity);

            var claims = CreateClaimsIdentity(botAppId);
            return ProcessProactiveAsync(claims, continuationActivity, BotClaims.GetTokenAudience(claims.Claims), callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            _ = claimsIdentity ?? throw new ArgumentNullException(nameof(claimsIdentity));
            _ = callback ?? throw new ArgumentNullException(nameof(callback));
            ValidateContinuationActivity(continuationActivity);

            return ProcessProactiveAsync(claimsIdentity, continuationActivity, null, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            _ = claimsIdentity ?? throw new ArgumentNullException(nameof(claimsIdentity));
            _ = callback ?? throw new ArgumentNullException(nameof(callback));
            ValidateContinuationActivity(continuationActivity);

            return ProcessProactiveAsync(claimsIdentity, continuationActivity, audience, callback, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task CreateConversationAsync(string botAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(serviceUrl))
            {
                throw new ArgumentNullException(nameof(serviceUrl));
            }

            _ = conversationParameters ?? throw new ArgumentNullException(nameof(conversationParameters));
            _ = callback ?? throw new ArgumentNullException(nameof(callback));

            Logger.LogInformation($"CreateConversationAsync for channel: {channelId}");

            // Create a ClaimsIdentity, to create the connector and for adding to the turn context.
            var claimsIdentity = CreateClaimsIdentity(botAppId);
            claimsIdentity.AddClaim(new Claim(AuthenticationConstants.ServiceUrlClaim, serviceUrl));

            // Create the connector client to use for outbound requests.
            using (var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(claimsIdentity, serviceUrl, audience, cancellationToken).ConfigureAwait(false))
            {
                // Make the actual create conversation call using the connector.
                var createConversationResult = await connectorClient.Conversations.CreateConversationAsync(conversationParameters, cancellationToken).ConfigureAwait(false);

                // Create the create activity to communicate the results to the application.
                var createActivity = CreateCreateActivity(createConversationResult, channelId, serviceUrl, conversationParameters);

                // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
                using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken).ConfigureAwait(false);

                // Create a turn context and run the pipeline.
                using var context = CreateTurnContext(createActivity, claimsIdentity, null, connectorClient, userTokenClient, callback);

                // Run the pipeline.
                await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The implementation for continue conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">The continuation <see cref="Activity"/> used to create the <see cref="ITurnContext" />.</param>
        /// <param name="audience">The audience for the call.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected async Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            Logger.LogInformation($"ProcessProactiveAsync for Conversation Id: {continuationActivity.Conversation.Id}");

            // Create the connector client to use for outbound requests.
            using var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(claimsIdentity, continuationActivity.ServiceUrl, audience, cancellationToken).ConfigureAwait(false);

            // Create a UserTokenClient instance for the application to use. (For example, in the OAuthPrompt.)
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken).ConfigureAwait(false);

            // Create a turn context and run the pipeline.
            using var context = CreateTurnContext(continuationActivity, claimsIdentity, audience, connectorClient, userTokenClient, callback);

            // Run the pipeline.
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            Logger.LogInformation($"ProcessActivityAsync");
            IList<string> scopes = null;

            string outgoingAudience;
            if (BotClaims.IsBotClaim(claimsIdentity.Claims))
            {
                outgoingAudience = BotClaims.GetTokenAudience(claimsIdentity.Claims);
                scopes = [$"{BotClaims.GetOutgoingAppId(claimsIdentity.Claims)}/.default"];
                activity.CallerId = $"{CallerIdConstants.BotToBotPrefix}{BotClaims.GetOutgoingAppId(claimsIdentity.Claims)}";
            }
            else
            {
                outgoingAudience = AuthenticationConstants.BotFrameworkScope;
                //activity.CallerId = ???
            }

            bool useAnonymousAuthCallback = false; 
            if (!claimsIdentity.IsAuthenticated && activity.ChannelId == Channels.Emulator)
                useAnonymousAuthCallback = true;

            // Create the connector client to use for outbound requests.
            using var connectorClient = await ChannelServiceFactory.CreateConnectorClientAsync(
                claimsIdentity, 
                activity.ServiceUrl, 
                outgoingAudience, 
                cancellationToken, 
                scopes: scopes,
                useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

            // Create a UserTokenClient instance for OAuth flow.
            using var userTokenClient = await ChannelServiceFactory.CreateUserTokenClientAsync(claimsIdentity, cancellationToken, useAnonymous: useAnonymousAuthCallback).ConfigureAwait(false);

            // Create a turn context and run the pipeline.
            using var context = CreateTurnContext(activity, claimsIdentity, outgoingAudience, connectorClient, userTokenClient, callback);

            // Run the pipeline.
            await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);

            // If there are any results they will have been left on the TurnContext. 
            return ProcessTurnResults(context);
        }

        /// <summary>
        /// This is a helper to create the ClaimsIdentity structure from an appId that will be added to the TurnContext.
        /// It is intended for use in proactive and named-pipe scenarios.
        /// </summary>
        /// <param name="botAppId">The bot's application id.</param>
        /// <returns>A <see cref="ClaimsIdentity"/> with the audience and appId claims set to the appId.</returns>
        protected ClaimsIdentity CreateClaimsIdentity(string botAppId)
        {
            botAppId ??= string.Empty;

            // Hand craft Claims Identity.
            return new ClaimsIdentity(
            [
                // Adding claims for both Emulator and Channel.
                new(AuthenticationConstants.AudienceClaim, botAppId),
                new(AuthenticationConstants.AppIdClaim, botAppId),
            ]);
        }

        protected virtual Task<bool> StreamedResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        private static Activity CreateCreateActivity(ConversationResourceResponse createConversationResult, string channelId, string serviceUrl, ConversationParameters conversationParameters)
        {
            // Create a conversation update activity to represent the result.
            var activity = Activity.CreateEventActivity();
            activity.Name = ActivityEventNames.CreateConversation;
            activity.ChannelId = channelId;
            activity.ServiceUrl = serviceUrl;
            activity.Id = createConversationResult.ActivityId ?? Guid.NewGuid().ToString("n");
            activity.Conversation = new ConversationAccount(id: createConversationResult.Id, tenantId: conversationParameters.TenantId);
            activity.ChannelData = conversationParameters.ChannelData;
            activity.Recipient = conversationParameters.Bot;
            return (Activity)activity;
        }

        private TurnContext CreateTurnContext(IActivity activity, ClaimsIdentity claimsIdentity, string oauthScope, IConnectorClient connectorClient, IUserTokenClient userTokenClient, BotCallbackHandler callback)
        {
            var turnContext = new TurnContext(this, activity);
            turnContext.TurnState.Add<IIdentity>(TurnStateKeys.BotIdentityKey, claimsIdentity);
            turnContext.TurnState.Add(connectorClient);
            turnContext.TurnState.Add(userTokenClient);
            turnContext.TurnState.Add(callback);
            turnContext.TurnState.Add(ChannelServiceFactory);
            turnContext.TurnState.Set(TurnStateKeys.OAuthScopeKey, oauthScope); // in non-skills scenarios the oauth scope value here will be null, so use Set

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
                var expectedReplies = new ExpectedReplies(turnContext.BufferedReplyActivities);

                var activityInvokeResponse = turnContext.TurnState.Get<Activity>(TurnStateKeys.InvokeResponseKey);
                if (activityInvokeResponse != null)
                {
                    expectedReplies.Body = ProtocolJsonSerializer.ToObject<InvokeResponse>(activityInvokeResponse.Value).Body;
                }

                return new InvokeResponse { Status = (int)HttpStatusCode.OK, Body = expectedReplies };
            }

            // Handle Invoke scenarios where the Bot will return a specific body and return code.
            if (turnContext.Activity.Type == ActivityTypes.Invoke)
            {
                var activityInvokeResponse = turnContext.TurnState.Get<Activity>(TurnStateKeys.InvokeResponseKey);
                if (activityInvokeResponse == null)
                {
                    return new InvokeResponse { Status = (int)HttpStatusCode.NotImplemented };
                }

                return (InvokeResponse)activityInvokeResponse.Value;
            }

            // No body to return.
            return null;
        }
    }
}
