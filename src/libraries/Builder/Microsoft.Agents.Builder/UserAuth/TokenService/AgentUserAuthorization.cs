// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Handles OAuth for the Agent.
    /// </summary>
    internal class AgentUserAuthorization : IUserAuthorizationFlow
    {
        // OAuthFlow is used here as a path for back-compat since Dialog.OAuthPrompt uses
        // it.  Long term, OAuthPrompt should probably migrate to the higher level classes
        // and this impl refactored.
        private readonly OAuthFlow _flow;

        private readonly OAuthSettings _settings;
        private readonly IStorage _storage;
        private readonly Deduplicate _dedupe;

        /// <summary>
        /// Name of the authentication handler
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The name of OAuth handler</param>
        /// <param name="oauthSettings"></param>
        /// <param name="storage"></param>
        public AgentUserAuthorization(string name, OAuthSettings oauthSettings, IStorage storage)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            _name = name;
            _settings = oauthSettings ?? throw new ArgumentNullException(nameof(oauthSettings));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _flow = new OAuthFlow(oauthSettings);
            _dedupe = new Deduplicate(_settings, _storage);
        }

        /// <inheritdoc/>
        public virtual async Task<bool> IsValidActivity(ITurnContext context, CancellationToken cancellationToken = default)
        {
            // Catch user input in Teams where the flow has timed out.  Otherwise we get stuck in "flow active" forever.
            if (context.Activity.ChannelId == Channels.Msteams && context.Activity.IsType(ActivityTypes.Message))
            {
                var state = await GetFlowStateAsync(context, cancellationToken).ConfigureAwait(false);
                if (state.FlowStarted && OAuthFlow.HasTimedOut(context, state.FlowExpires))
                {
                    throw new AuthException("Authorization flow timed out.", AuthExceptionReason.Timeout);
                }
            }

            var isMatch = context.Activity.IsType(ActivityTypes.Message);

            isMatch |= context.Activity.IsType(ActivityTypes.Invoke) &&
                context.Activity.Name == SignInConstants.VerifyStateOperationName;

            isMatch |= context.Activity.IsType(ActivityTypes.Invoke) &&
                context.Activity.Name == SignInConstants.TokenExchangeOperationName;

            isMatch |= context.Activity.IsType(ActivityTypes.Invoke) &&
                context.Activity.Name == SignInConstants.SignInFailure;

            return isMatch;
        }

        public virtual async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _storage.DeleteAsync([GetStorageKey(turnContext)], cancellationToken).ConfigureAwait(false);
        }

        private async Task<FlowState> GetFlowStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var key = GetStorageKey(turnContext);
            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            return items.TryGetValue(key, out object value) ? (FlowState)value : new FlowState();
        }

        private async Task SaveFlowStateAsync(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            var key = GetStorageKey(turnContext);
            var items = new Dictionary<string, object>()
                {
                    { key, state }
                };
            await _storage.WriteAsync(items, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> OnFlowTurn(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (_settings.EnableSso && !await _dedupe.ProceedWithExchangeAsync(turnContext, cancellationToken).ConfigureAwait(false))
            {
                throw new DuplicateExchangeException();
            }

            var state = await GetFlowStateAsync(turnContext, cancellationToken).ConfigureAwait(false);

            // Handle start or continue of the flow.
            // Either path can throw.  This is intentionally not trapping the exception to give the caller the chance
            // to determine if a retry is applicable.  Otherwise, caller should call ResetState.
            TokenResponse tokenResponse;
            if (!state.FlowStarted)
            {
                // If the user is already signed in, tokenResponse will be non-null
                tokenResponse = await OnGetOrStartFlowAsync(turnContext, state, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // For non-Teams Agents, the user sends the "magic code" that will be used to exchange for a token.
                tokenResponse = await OnContinueFlow(turnContext, state, cancellationToken);
            }

            await SaveFlowStateAsync(turnContext, state, cancellationToken).ConfigureAwait(false);

            return tokenResponse;
        }

        private async Task<TokenResponse> OnGetOrStartFlowAsync(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            // If the user is already signed in, tokenResponse will be non-null
            var tokenResponse = await _flow.BeginFlowAsync(
                turnContext,
                null,
                cancellationToken).ConfigureAwait(false);

            // If a TokenResponse is returned, there was a cached token already.  Otherwise, start the process of getting a new token.
            if (tokenResponse == null)
            {
                var expires = DateTime.UtcNow.AddMilliseconds(_settings.Timeout ?? OAuthSettings.DefaultTimeoutValue.TotalMilliseconds);

                state.FlowStarted = true;
                state.FlowExpires = expires;
            }

            return tokenResponse;
        }

        private async Task<TokenResponse> OnContinueFlow(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            TokenResponse tokenResponse;

            try
            {
                tokenResponse = await _flow.ContinueFlowAsync(turnContext, state.FlowExpires, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new AuthException("Authorization flow timed out.", AuthExceptionReason.Timeout);
            }
            catch (UserCancelledException)
            {
                throw new AuthException("User cancelled authorization", AuthExceptionReason.UserCancelled);
            }

            if (tokenResponse == null)
            {
                if (!OAuthFlow.IsTokenExchangeRequestInvoke(turnContext))
                {
                    state.ContinueCount++;
                    if (state.ContinueCount >= _settings.InvalidSignInRetryMax)
                    {
                        // The only way this happens is if C2 sent a bogus code
                        throw new AuthException("Retry max", AuthExceptionReason.InvalidSignIn);
                    }

                    await turnContext.SendActivityAsync(_settings.InvalidSignInRetryMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                // token still pending.
                return null;
            }

            state.FlowStarted = false;
            return tokenResponse;
        }

        private string GetStorageKey(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");
            return $"oauth/{_name}/{channelId}/{conversationId}/flowState";
        }
    }

    class FlowState
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
        public int ContinueCount = 0;
    }
}
